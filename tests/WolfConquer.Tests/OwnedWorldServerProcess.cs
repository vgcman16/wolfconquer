using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed class OwnedWorldServerProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly Channel<string> output = Channel.CreateUnbounded<string>();
    private readonly ConcurrentQueue<string> lines = new();
    private Task stdout = Task.CompletedTask, stderr = Task.CompletedTask;
    private bool started, disposed;
    internal int Port { get; private set; }
    internal bool Stopped => !started || process.HasExited;
    internal bool HasExited => started && process.HasExited;
    internal int ExitCode => process.ExitCode;
    internal string[] Lines => lines.ToArray();
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    internal OwnedWorldServerProcess(LegacyOwnedAdmissionRuntimeFixture data, bool encrypted, bool configured, bool listen)
        : this(data.Users, data.Root, data.ConfigPath, encrypted, configured, listen) { }

    internal OwnedWorldServerProcess(LegacyOwnedAdmissionOptions options, string ownedRoot,
        string configurationPath, bool encrypted)
        : this(options.UsersRoot, ownedRoot, configurationPath, encrypted, configured: true, listen: true) { }

    private OwnedWorldServerProcess(string usersRoot, string ownedRoot, string configurationPath,
        bool encrypted, bool configured, bool listen)
    {
        process = TestProjectProcess.Create(FindRepositoryRoot(), "WolfConquer.WorldServer");
        // Clear ambient routes/options/secrets so no original saves, SQL or live
        // log paths can be inherited by this authored child.
        foreach (var key in process.StartInfo.Environment.Keys.Where(key => key.StartsWith("WOLFCONQUER_", StringComparison.OrdinalIgnoreCase)).ToArray())
            process.StartInfo.Environment.Remove(key);
        var env = process.StartInfo.Environment;
        env[CharacterSnapshotOptions.UsersRootEnvironmentVariable] = usersRoot;
        env[WorldDataOptions.SeedJsonEnvironmentVariable] = Path.Combine(ownedRoot, "seed");
        env[ConquerProtocolProfileConfiguration.EnvironmentVariable] = nameof(ConquerProtocolProfile.VisibleDx8D13);
        env[LegacyLocalServerIdentity.ServerIdEnvironmentVariable] = "2";
        env["WOLFCONQUER_OWNED_ADMISSION_CONFIG"] = configured ? configurationPath : "";
        // These are synthetic test handoff inputs, not account credentials. Keep
        // Program's genuine existing service/config path enabled in both modes.
        env[AccountHandoffOptions.StoreRootEnvironmentVariable] = Path.Combine(ownedRoot, "handoff");
        env[AccountHandoffOptions.SigningSecretEnvironmentVariable] = "owned-host-synthetic-handoff-secret";
        env["WOLFCONQUER_RUNTIME_CLOCK_START_UTC"] = "2026-10-26T12:00:00Z";
        if (listen) process.StartInfo.ArgumentList.Add("--listen");
        process.StartInfo.ArgumentList.Add("--address");
        process.StartInfo.ArgumentList.Add("127.0.0.1");
        process.StartInfo.ArgumentList.Add("--port");
        process.StartInfo.ArgumentList.Add("0");
        if (encrypted) process.StartInfo.ArgumentList.Add("--legacy-game-wire");
    }

    internal async Task StartListeningAsync()
    {
        Start();
        var line = await ReadUntilAsync(value => value.StartsWith("Listening on ", StringComparison.Ordinal));
        var split = line.LastIndexOf(':');
        Assert.True(split >= 0, line);
        Assert.True(int.TryParse(line[(split + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var port), line);
        Assert.InRange(port, 1, ushort.MaxValue);
        Port = port;
    }

    internal async Task StartToExitAsync()
    {
        Start();
        using var timeout = new CancellationTokenSource(Timeout);
        await process.WaitForExitAsync(timeout.Token);
        await Task.WhenAll(stdout, stderr).WaitAsync(timeout.Token);
    }

    private void Start()
    {
        if (started) throw new InvalidOperationException("Owned child was already started.");
        Assert.True(process.Start(), "Could not start the already-built WorldServer.");
        started = true;
        stdout = PumpAsync(process.StandardOutput, publish: true);
        stderr = PumpAsync(process.StandardError, publish: false);
    }

    private async Task PumpAsync(StreamReader reader, bool publish)
    {
        try
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                lines.Enqueue(line);
                if (publish) await output.Writer.WriteAsync(line);
            }
            if (publish) output.Writer.TryComplete();
        }
        catch (Exception error)
        {
            if (publish) output.Writer.TryComplete(error);
            throw;
        }
    }

    internal async Task<string> ReadUntilAsync(Func<string, bool> predicate)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        try
        {
            await foreach (var line in output.Reader.ReadAllAsync(timeout.Token))
                if (predicate(line)) return line;
        }
        catch (OperationCanceledException error)
        { throw new TimeoutException("Owned WorldServer output timed out. " + string.Join(Environment.NewLine, Lines), error); }
        throw new IOException("Owned WorldServer exited before required output. " + string.Join(Environment.NewLine, Lines));
    }

    internal async Task StopAsync()
    {
        if (!started) return;
        if (!process.HasExited) process.Kill(entireProcessTree: true);
        using var timeout = new CancellationTokenSource(Timeout);
        await process.WaitForExitAsync(timeout.Token);
        await Task.WhenAll(stdout, stderr).WaitAsync(timeout.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        await StopAsync();
        // Capture the fact before disposing Process; fixture cleanup consults it.
        started = false;
        process.Dispose();
        disposed = true;
    }

    private static string FindRepositoryRoot()
    {
        for (var path = new DirectoryInfo(AppContext.BaseDirectory); path is not null; path = path.Parent)
            if (File.Exists(Path.Combine(path.FullName, "WolfConquer.sln"))) return path.FullName;
        throw new DirectoryNotFoundException("Could not find the owned test repository from built test output.");
    }
}

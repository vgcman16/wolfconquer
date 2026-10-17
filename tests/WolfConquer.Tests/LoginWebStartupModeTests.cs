using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.Web;

namespace WolfConquer.Tests;

[Collection(LoginLoaderConfigurationCollection.Name)]
public sealed class LoginWebStartupModeTests
{
    private static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task DefaultStartupModeReportsUnavailableRoutesAndExits()
    {
        var result = await RunLoginWebAppAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"LoginWeb for {ConquerRuntimeDefaults.ServerName}", result.StandardOutput);
        Assert.Contains($"Host header: {LoginLoaderHttpResponder.LoginLoaderHost}", result.StandardOutput);
        Assert.Contains("Planned listen endpoint: 127.0.0.1:80", result.StandardOutput);
        foreach (var route in LoginLoaderHttpResponder.Routes)
        {
            Assert.Contains($"{route.Path} -> unavailable (file from {route.EnvironmentVariable})", result.StandardOutput);
        }

        Assert.Contains("Pass --listen to start accepting HTTP clients.", result.StandardOutput);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
    }

    [Fact]
    public async Task ConfiguredStartupReportsAvailabilityWithoutDumpingFilesOrContent()
    {
        using var fixture = new LoginLoaderPayloadFixture(configureCurrentProcess: false);
        var result = await RunLoginWebAppAsync(fixture);

        Assert.Equal(0, result.ExitCode);
        foreach (var route in LoginLoaderHttpResponder.Routes)
        {
            Assert.Contains($"{route.Path} -> configured (file from {route.EnvironmentVariable})", result.StandardOutput);
            Assert.DoesNotContain(fixture.FileFor(route.EnvironmentVariable), result.StandardOutput);
        }

        Assert.DoesNotContain(System.Text.Encoding.ASCII.GetString(LoginLoaderPayloadFixture.Version), result.StandardOutput);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
    }

    [Fact]
    public async Task ListenModeServesExplicitSyntheticPayloadFiles()
    {
        using var fixture = new LoginLoaderPayloadFixture(configureCurrentProcess: false);
        await using var process = await StartListeningLoginWebAppAsync(fixture);
        using var client = new HttpClient();
        var baseAddress = $"http://127.0.0.1:{process.Port}";

        foreach (var route in LoginLoaderHttpResponder.Routes)
        {
            using var response = await client.GetAsync(baseAddress + route.Path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(LoginLoaderPayloadFixture.PayloadFor(route.Path), await response.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    public async Task ListenModeWithoutFilesReturnsServiceUnavailable()
    {
        await using var process = await StartListeningLoginWebAppAsync();
        using var client = new HttpClient();
        using var response = await client.GetAsync($"http://127.0.0.1:{process.Port}" + LoginLoaderHttpResponder.VersionPath);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task ListenModeCanStartAutoPatchSocketSink()
    {
        var repositoryRoot = FindRepositoryRoot();
        var process = CreateDotnetRunProcess(repositoryRoot);
        process.StartInfo.ArgumentList.Add("--listen");
        process.StartInfo.ArgumentList.Add("--address");
        process.StartInfo.ArgumentList.Add("127.0.0.1");
        process.StartInfo.ArgumentList.Add("--port");
        process.StartInfo.ArgumentList.Add("0");
        process.StartInfo.ArgumentList.Add("--autopatch-socket-listen");
        process.StartInfo.ArgumentList.Add("--autopatch-socket-address");
        process.StartInfo.ArgumentList.Add("127.0.0.1");
        process.StartInfo.ArgumentList.Add("--autopatch-socket-ports");
        process.StartInfo.ArgumentList.Add("0");

        Assert.True(process.Start(), "Could not start LoginWeb in AutoPatch socket mode.");
        await using var server = new ListeningLoginWebProcess(process);
        var listeningLine = await server.ReadUntilAsync(
            line => line.StartsWith("AutoPatch socket sink listening on ", StringComparison.Ordinal),
            DefaultStartupTimeout);
        var match = Regex.Match(listeningLine, @":(?<port>\d+)$");
        Assert.True(match.Success, $"Could not parse AutoPatch socket listening line: {listeningLine}");
        var port = int.Parse(match.Groups["port"].Value);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port);
        await client.GetStream().WriteAsync(new byte[] { 0x01, 0x02, 0x03 });
        var response = new byte[5];
        var bytesRead = await client.GetStream().ReadAsync(response);

        var closedLine = await server.ReadUntilAsync(
            line => line.Contains("AutoPatch socket closed client", StringComparison.Ordinal),
            DefaultStartupTimeout);

        Assert.Equal(5, bytesRead);
        Assert.Equal("READY"u8.ToArray(), response);
        Assert.Contains("bytes=3", closedLine);
        Assert.Contains("payloadHex=010203", closedLine);
        Assert.Contains("responseHex=5245414459", closedLine);
    }

    [Fact]
    public async Task ListenModeCanOverrideAutoPatchSocketSinkResponse()
    {
        var repositoryRoot = FindRepositoryRoot();
        var process = CreateDotnetRunProcess(repositoryRoot);
        process.StartInfo.Environment[AutoPatchSocketSinkServer.ResponsePayloadEnvironmentVariable] = "FAIL";
        process.StartInfo.ArgumentList.Add("--listen");
        process.StartInfo.ArgumentList.Add("--address");
        process.StartInfo.ArgumentList.Add("127.0.0.1");
        process.StartInfo.ArgumentList.Add("--port");
        process.StartInfo.ArgumentList.Add("0");
        process.StartInfo.ArgumentList.Add("--autopatch-socket-listen");
        process.StartInfo.ArgumentList.Add("--autopatch-socket-address");
        process.StartInfo.ArgumentList.Add("127.0.0.1");
        process.StartInfo.ArgumentList.Add("--autopatch-socket-ports");
        process.StartInfo.ArgumentList.Add("0");

        Assert.True(process.Start(), "Could not start LoginWeb in AutoPatch socket mode.");
        await using var server = new ListeningLoginWebProcess(process);
        var listeningLine = await server.ReadUntilAsync(
            line => line.StartsWith("AutoPatch socket sink listening on ", StringComparison.Ordinal),
            DefaultStartupTimeout);
        var match = Regex.Match(listeningLine, @":(?<port>\d+)$");
        Assert.True(match.Success, $"Could not parse AutoPatch socket listening line: {listeningLine}");
        var port = int.Parse(match.Groups["port"].Value);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port);
        await client.GetStream().WriteAsync(new byte[] { 0x37, 0x38, 0x39, 0x39 });
        var response = new byte[4];
        var bytesRead = await client.GetStream().ReadAsync(response);

        var closedLine = await server.ReadUntilAsync(
            line => line.Contains("AutoPatch socket closed client", StringComparison.Ordinal),
            DefaultStartupTimeout);

        Assert.Equal(4, bytesRead);
        Assert.Equal("FAIL"u8.ToArray(), response);
        Assert.Contains("bytes=4", closedLine);
        Assert.Contains("payloadHex=37383939", closedLine);
        Assert.Contains("responseHex=4641494c", closedLine);
    }

    private static async Task<ConsoleRunResult> RunLoginWebAppAsync(LoginLoaderPayloadFixture? fixture = null)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = CreateDotnetRunProcess(repositoryRoot);
        fixture?.ConfigureProcess(process);
        Assert.True(process.Start(), "Could not start LoginWeb.");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(DefaultStartupTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"LoginWeb default startup did not exit. stdout: {await outputTask} stderr: {await errorTask}");
        }

        return new ConsoleRunResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static async Task<ListeningLoginWebProcess> StartListeningLoginWebAppAsync(LoginLoaderPayloadFixture? fixture = null)
    {
        var repositoryRoot = FindRepositoryRoot();
        var process = CreateDotnetRunProcess(repositoryRoot);
        process.StartInfo.ArgumentList.Add("--listen");
        process.StartInfo.ArgumentList.Add("--address");
        process.StartInfo.ArgumentList.Add("127.0.0.1");
        process.StartInfo.ArgumentList.Add("--port");
        process.StartInfo.ArgumentList.Add("0");

        fixture?.ConfigureProcess(process);
        Assert.True(process.Start(), "Could not start LoginWeb in listen mode.");
        var server = new ListeningLoginWebProcess(process);
        var listeningLine = await server.ReadUntilAsync(
            line => line.StartsWith("HTTP listening on ", StringComparison.Ordinal),
            DefaultStartupTimeout);
        var match = Regex.Match(listeningLine, @":(?<port>\d+)$");
        Assert.True(match.Success, $"Could not parse LoginWeb listening line: {listeningLine}");
        server.Port = int.Parse(match.Groups["port"].Value);
        return server;
    }

    private static Process CreateDotnetRunProcess(string repositoryRoot)
    {
        var process = TestProjectProcess.Create(repositoryRoot, "WolfConquer.LoginWeb", enableRaisingEvents: true);
        foreach (var route in LoginLoaderHttpResponder.Routes)
        {
            process.StartInfo.Environment.Remove(route.EnvironmentVariable);
        }

        return process;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output directory.");
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class ListeningLoginWebProcess : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly Task<string> _errorTask;

        public ListeningLoginWebProcess(Process process)
        {
            _process = process;
            _errorTask = process.StandardError.ReadToEndAsync();
        }

        public int Port { get; set; }

        public async Task<string> ReadUntilAsync(Func<string, bool> predicate, TimeSpan timeout)
        {
            using var stop = new CancellationTokenSource(timeout);
            while (!stop.IsCancellationRequested)
            {
                var readTask = _process.StandardOutput.ReadLineAsync(stop.Token).AsTask();
                string? line;
                try
                {
                    line = await readTask;
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (line is null)
                {
                    break;
                }

                if (predicate(line))
                {
                    return line;
                }
            }

            var stderr = _errorTask.IsCompleted ? await _errorTask : string.Empty;
            Assert.Fail($"Timed out waiting for LoginWeb process output. stderr: {stderr}");
            throw new UnreachableException();
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }

            await _process.WaitForExitAsync();
            _process.Dispose();
        }
    }
}

using System.Diagnostics;
using System.Text;

namespace WolfConquer.Updates.Source;

// Each operation owns an empty hooks directory/config; no checkout hooks are run.
internal sealed class SourceUpdateGit : IDisposable
{
    private readonly string _temporary = Directory.CreateTempSubdirectory("wolf-source-update-").FullName;
    private readonly string _root;
    private readonly bool _allowLocal;

    internal SourceUpdateGit(string root, bool allowLocal)
    {
        _root = root;
        _allowLocal = allowLocal;
        Directory.CreateDirectory(Path.Combine(_temporary, "hooks"));
        File.WriteAllText(Path.Combine(_temporary, "empty.config"), "");
    }

    internal async Task<GitReply> RunAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = _root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var key in start.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray())
            start.Environment.Remove(key);
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        start.Environment["GIT_CONFIG_SYSTEM"] = Path.Combine(_temporary, "empty.config");
        start.Environment["GIT_CONFIG_GLOBAL"] = Path.Combine(_temporary, "empty.config");
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        start.Environment["GCM_INTERACTIVE"] = "Never";
        foreach (var value in new[]
        {
            "core.hooksPath=" + Path.Combine(_temporary, "hooks"), "core.fsmonitor=false", "credential.helper=",
            "protocol.allow=never", "protocol.https.allow=always", "protocol.ext.allow=never",
            "protocol.file.allow=" + (_allowLocal ? "always" : "never"),
            "http.sslVerify=true", "http.followRedirects=false",
        })
        {
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(value);
        }
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        cancellationToken.ThrowIfCancellationRequested();
        if (!process.Start()) throw new IOException("Git could not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("Git operation timed out.");
        }
        // Never return stderr (which may echo remote/config credentials) to users.
        await stderr.ConfigureAwait(false);
        return new GitReply(process.ExitCode, await stdout.ConfigureAwait(false));
    }

    public void Dispose()
    {
        // Only this freshly created operation directory, never .git or checkout files.
        try { Directory.Delete(_temporary, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

internal sealed record GitReply(int ExitCode, string Output)
{
    internal string RequireSuccess()
    {
        if (ExitCode != 0) throw new IOException("Git operation failed; checkout was not reset or overwritten.");
        return Output.TrimEnd('\r', '\n');
    }
}

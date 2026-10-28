using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceStatusTests
{
    private static async Task<(int ExitCode, string Output, JsonNode? Json)> RunAsync(string parent, params string[] args)
    {
        var start = new ProcessStartInfo(FindPowerShell())
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File",
            Path.Combine(RepositoryRoot(), "tools", "testing", "Get-GameAcceptanceStatus.ps1"), "-EvidenceParent", parent }.Concat(args))
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException primary)
        {
            try { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception cleanup) { throw new AggregateException("Status child and cleanup both failed.", primary, cleanup); }
            throw;
        }
        var text = await output; Assert.True(string.IsNullOrWhiteSpace(await error), await error);
        return (process.ExitCode, text, args.Contains("Table", StringComparer.Ordinal) ? null : JsonNode.Parse(text));
    }
    private static JsonObject Row(JsonNode summary, string scenario) => Assert.Single(summary["rows"]!.AsArray(), row => row!["scenario"]!.GetValue<string>() == scenario)!.AsObject();
    private static void NoLivePass(JsonNode summary)
    {
        Assert.True(summary["readOnly"]!.GetValue<bool>());
        Assert.False(summary["liveGameplayProven"]!.GetValue<bool>()); Assert.False(summary["sourceGateAccepted"]!.GetValue<bool>());
        foreach (var row in summary["rows"]!.AsArray())
        { Assert.False(row!["liveGameplayProven"]!.GetValue<bool>()); Assert.Equal("LiveClientVisualPending", row["visualStatus"]!.GetValue<string>()); }
    }
    private static string FindPowerShell()
    {
        var explicitPath = Environment.GetEnvironmentVariable("WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath)) return Path.GetFullPath(explicitPath);
        foreach (var part in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            var candidate = Path.Combine(part.Trim('"'), OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh");
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        throw new FileNotFoundException("Actual PowerShell executable unavailable.");
    }
    private static string RepositoryRoot([CallerFilePath] string source = "")
    {
        for (var path = new DirectoryInfo(Path.GetDirectoryName(source)!); path is not null; path = path.Parent)
            if (File.Exists(Path.Combine(path.FullName, "WolfConquer.sln"))) return path.FullName;
        throw new DirectoryNotFoundException("Repository root unavailable.");
    }
}

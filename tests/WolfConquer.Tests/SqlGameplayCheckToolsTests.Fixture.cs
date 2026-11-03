using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace WolfConquer.Tests;

// Only inert Plan and pre-launch refusal against owned synthetic files.
public sealed partial class SqlGameplayCheckToolsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf sql plan ;$ " + Guid.NewGuid().ToString("N"));
    private string Script => Path.Combine(root, "tools", "dev", "Invoke-SqlGameplayCheck.ps1");
    private string Project => Path.Combine(root, "tests", "WolfConquer.Tests", "WolfConquer.Tests.csproj");
    private string OtherDirectory => Path.Combine(root, "unrelated cwd");

    public SqlGameplayCheckToolsTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Script)!);
        Directory.CreateDirectory(Path.GetDirectoryName(Project)!);
        Directory.CreateDirectory(OtherDirectory);
        File.Copy(FindScript(), Script);
        File.WriteAllText(Path.Combine(OtherDirectory, "dotnet.cmd"), "@echo off\r\necho invoked>\"%~dp0dotnet-invoked.txt\"\r\nexit /b 87\r\n");
        File.WriteAllText(Project, "<Project><!-- synthetic plan only; never build --></Project>");
        File.WriteAllText(Path.Combine(root, "tools", "dev", "Invoke-OwnedSqlAuthFixture.ps1"), "throw 'Plan must not execute lifecycle'");
        File.WriteAllText(Path.Combine(root, "tools", "dev", "FocusedCheckSupport.psm1"), "throw 'Plan must not import support'");
    }

    private string[] Entries() => Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(root, path)).Order(StringComparer.Ordinal).ToArray();

    private async Task<Result> RunAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo(PowerShellPath())
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = OtherDirectory,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        start.Environment["PATH"] = OtherDirectory + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? "");
        foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", Script }.Concat(arguments))
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw;
        }
        return new(process.ExitCode, await output, await error);
    }

    private static string FindScript([CallerFilePath] string source = "") => FindTool("Invoke-SqlGameplayCheck.ps1", source);

    private static string FindTool(string name, [CallerFilePath] string source = "")
    {
        for (DirectoryInfo? dir = new(Path.GetDirectoryName(source)!); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "WolfConquer.sln")))
                return Path.Combine(dir.FullName, "tools", "dev", name);
        throw new DirectoryNotFoundException("Could not locate repository tools.");
    }

    private static string PowerShellPath()
    {
        var measured = Environment.GetEnvironmentVariable("WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH");
        if (!string.IsNullOrWhiteSpace(measured) && File.Exists(measured)) return Path.GetFullPath(measured);
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var path = Path.Combine(directory.Trim('"'), OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh");
            if (File.Exists(path)) return Path.GetFullPath(path);
        }
        throw new FileNotFoundException("PowerShell 7 is required for local tool tests.");
    }

    public void Dispose()
    {
        var path = Path.GetFullPath(root);
        var temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        Assert.True(string.Equals(temporary, Path.GetDirectoryName(path), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        Assert.StartsWith("wolf sql plan ;$ ", Path.GetFileName(path), StringComparison.Ordinal);
        Directory.Delete(path, recursive: true);
    }

    private sealed record Result(int ExitCode, string Output, string Error);
}






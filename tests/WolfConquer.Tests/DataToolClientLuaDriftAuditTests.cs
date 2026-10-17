using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolClientLuaDriftAuditTests
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    [Fact]
    public async Task DriftSummaryGroupsIniLuaAndDatByTopDirectoryAndReportsSymbolEvidence()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-lua-drift-audit-" + Guid.NewGuid().ToString("N"));
        var currentRoot = Path.Combine(tempRoot, "current");
        var referenceRoot = Path.Combine(tempRoot, "reference");

        try
        {
            await WriteClientTextFileAsync(currentRoot, "ini/start.lua", "same\n");
            await WriteClientTextFileAsync(referenceRoot, "ini/start.lua", "same\n");
            await WriteClientTextFileAsync(currentRoot, "ini/luacfg/app.dat", "aa\nNeedleSymbol call\n");
            await WriteClientTextFileAsync(referenceRoot, "ini/luacfg/app.dat", "aa\nold call\n");
            await WriteClientTextFileAsync(currentRoot, "ini/lua/new.lua", "x=1\nNeedleSymbol()\n");
            await WriteClientTextFileAsync(referenceRoot, "ini/lua/old.lua", "old\n");

            var result = await RunDataToolAsync(
                "client-lua-drift-audit",
                currentRoot,
                "--reference-root",
                referenceRoot,
                "--symbol",
                "NeedleSymbol",
                "--top",
                "3");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Client Lua drift audit:", result.StandardOutput);
            Assert.Contains($"Current: {Path.GetFullPath(currentRoot)}", result.StandardOutput);
            Assert.Contains($"Reference: {Path.GetFullPath(referenceRoot)}", result.StandardOutput);
            Assert.Contains("Scope: ini/**/*.lua, ini/**/*.dat", result.StandardOutput);
            Assert.Contains("Same files: 1", result.StandardOutput);
            Assert.Contains("Different files: 1", result.StandardOutput);
            Assert.Contains("Current-only files: 1", result.StandardOutput);
            Assert.Contains("Reference-only files: 1", result.StandardOutput);
            Assert.Contains("Group summary (top 3 of 3):", result.StandardOutput);
            Assert.Contains("  lua same=0 different=0 current-only=1 reference-only=1", result.StandardOutput);
            Assert.Contains("  luacfg same=0 different=1 current-only=0 reference-only=0", result.StandardOutput);
            Assert.Contains("  <ini-root> same=1 different=0 current-only=0 reference-only=0", result.StandardOutput);
            Assert.Contains("Symbol: NeedleSymbol", result.StandardOutput);
            Assert.Contains("Symbol matches: 2 (showing 2)", result.StandardOutput);
            Assert.Contains("current-only lua/new.lua line=2 offset=4 text=NeedleSymbol()", result.StandardOutput);
            Assert.Contains("different luacfg/app.dat line=2 offset=3 text=NeedleSymbol call", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DotAndOldCompatDirectoriesAreSkipped()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-lua-drift-audit-skip-" + Guid.NewGuid().ToString("N"));
        var currentRoot = Path.Combine(tempRoot, "current");
        var referenceRoot = Path.Combine(tempRoot, "reference");

        try
        {
            Directory.CreateDirectory(Path.Combine(currentRoot, "ini"));
            Directory.CreateDirectory(Path.Combine(referenceRoot, "ini"));
            await WriteClientTextFileAsync(currentRoot, "ini/.backup/ignored-current.lua", "current\n");
            await WriteClientTextFileAsync(currentRoot, "ini/ini.oldcompat-20260707/ignored-current.dat", "current\n");
            await WriteClientTextFileAsync(referenceRoot, "ini/.backup/ignored-reference.lua", "reference\n");
            await WriteClientTextFileAsync(referenceRoot, "ini/ini.oldcompat-20260707/ignored-reference.dat", "reference\n");
            await WriteClientTextFileAsync(currentRoot, "Script/outside.lua", "outside\n");
            await WriteClientTextFileAsync(currentRoot, "ini/readme.txt", "not tracked\n");

            var result = await RunDataToolAsync(
                "client-lua-drift-audit",
                currentRoot,
                $"--reference-root={referenceRoot}",
                "--symbol=outside");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Same files: 0", result.StandardOutput);
            Assert.Contains("Different files: 0", result.StandardOutput);
            Assert.Contains("Current-only files: 0", result.StandardOutput);
            Assert.Contains("Reference-only files: 0", result.StandardOutput);
            Assert.Contains("Group summary (top 0 of 0):", result.StandardOutput);
            Assert.Contains("Symbol matches: 0 (showing 0)", result.StandardOutput);
            Assert.Contains("No startup Lua/resource drift found.", result.StandardOutput);
            Assert.DoesNotContain("ignored-current", result.StandardOutput);
            Assert.DoesNotContain("ignored-reference", result.StandardOutput);
            Assert.DoesNotContain("outside.lua", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task MissingReferenceRootReturnsUsageAndHelpIncludesCommand()
    {
        var missingReference = await RunDataToolAsync("client-lua-drift-audit", Path.GetTempPath());

        Assert.Equal(1, missingReference.ExitCode);
        Assert.Equal(string.Empty, missingReference.StandardOutput);
        Assert.Contains("Missing required --reference-root.", missingReference.StandardError);
        Assert.Contains(
            "Usage: WolfConquer.DataTool client-lua-drift-audit <clientRoot> --reference-root <path> [--symbol <text>] [--top N]",
            missingReference.StandardError);

        var help = await RunDataToolAsync("help");

        Assert.Equal(0, help.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool client-lua-drift-audit <clientRoot> --reference-root <path> [--symbol <text>] [--top N]",
            help.StandardOutput);
        Assert.Equal(string.Empty, help.StandardError);
    }

    private static async Task WriteClientTextFileAsync(string clientRoot, string relativePath, string text)
    {
        var path = Path.Combine(clientRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, text, Utf8NoBom);
    }

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = CreateDataToolProcess(repositoryRoot);
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Could not start DataTool.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"DataTool did not exit. stdout: {await outputTask} stderr: {await errorTask}");
        }

        return new ConsoleRunResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static Process CreateDataToolProcess(string repositoryRoot)
    {
        var copiedDataTool = Path.Combine(AppContext.BaseDirectory, "WolfConquer.DataTool.dll");
        if (!File.Exists(copiedDataTool))
        {
            return TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add(copiedDataTool);
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        return process;
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        var fromOutputDirectory = TryFindRepositoryRoot(AppContext.BaseDirectory);
        if (fromOutputDirectory is not null)
        {
            return fromOutputDirectory;
        }

        var fromWorkingDirectory = TryFindRepositoryRoot(Environment.CurrentDirectory);
        if (fromWorkingDirectory is not null)
        {
            return fromWorkingDirectory;
        }

        var sourceDirectory = Path.GetDirectoryName(sourcePath);
        if (sourceDirectory is not null)
        {
            var fromSourceDirectory = TryFindRepositoryRoot(sourceDirectory);
            if (fromSourceDirectory is not null)
            {
                return fromSourceDirectory;
            }
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output or working directory.");
    }

    private static string? TryFindRepositoryRoot(string startDirectory)
    {
        DirectoryInfo? directory = new(startDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}

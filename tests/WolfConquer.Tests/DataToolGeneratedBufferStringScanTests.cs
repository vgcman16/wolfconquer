using System.Runtime.CompilerServices;
using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolGeneratedBufferStringScanTests
{
    private const string GeneratedBufferStringScanCommandText =
        "WolfConquer.DataTool generated-buffer-string-scan";

    [Fact]
    public async Task HelpMentionsGeneratedBufferStringScanUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(GeneratedBufferStringScanCommandText, result.StandardOutput);
        Assert.Contains("--context-bytes N", result.StandardOutput);
        Assert.Contains("--max-hits N", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task ScanReportsAsciiAndUtf16HitsWithLabelsAndBoundedContext()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var oldPath = Path.Combine(tempRoot, "old-cache.exe");
            var currentPath = Path.Combine(tempRoot, "current-cache.exe");
            await WriteFixtureBytesAsync(
                oldPath,
                Encoding.ASCII.GetBytes("pre-CLuaVM::requirebuffer-post"));
            await WriteFixtureBytesAsync(
                currentPath,
                Encoding.Unicode.GetBytes("xxToStringExyy"));

            var result = await RunDataToolAsync(
                "generated-buffer-string-scan",
                "--context-bytes",
                "4",
                "--max-hits",
                "5",
                "old=" + oldPath,
                "current=" + currentPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Generated buffer string scan:", result.StandardOutput);
            Assert.Contains("Terms: CLuaVM::requirebuffer, ToStringEx, MicroSpotData, basedef.lua, c_require", result.StandardOutput);
            Assert.Contains("Encodings: ascii, utf-16le", result.StandardOutput);
            Assert.Contains("Context bytes: 4", result.StandardOutput);
            Assert.Contains("Max hits: 5", result.StandardOutput);
            Assert.Contains($"File: label=old path={oldPath}", result.StandardOutput);
            Assert.Contains($"File: label=current path={currentPath}", result.StandardOutput);
            Assert.Contains("Match: label=old term=CLuaVM::requirebuffer encoding=ascii count=1 offsets=0x4", result.StandardOutput);
            Assert.Contains("Match: label=current term=ToStringEx encoding=utf-16le count=1 offsets=0x4", result.StandardOutput);
            Assert.Contains(
                "Hit: label=old term=CLuaVM::requirebuffer encoding=ascii offset=0x4 contextStart=0x0",
                result.StandardOutput);
            Assert.Contains("text=\"pre-CLuaVM::requirebuffer-pos\"", result.StandardOutput);
            Assert.Contains(
                "Hit: label=current term=ToStringEx encoding=utf-16le offset=0x4 contextStart=0x0",
                result.StandardOutput);
            Assert.Contains("text=\"xxToStringExyy\"", result.StandardOutput);
            Assert.Contains("Displayed hits: 2 of 2 (max 5)", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CustomTermsAndMaxHitsBoundPrintedHitLines()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var path = Path.Combine(tempRoot, "generated-buffer.bin");
            await WriteFixtureBytesAsync(path, Encoding.ASCII.GetBytes("Needle-one Needle-two ToStringEx"));

            var result = await RunDataToolAsync(
                "generated-buffer-string-scan",
                "--term",
                "Needle",
                "--context-bytes=2",
                "--max-hits=1",
                "probe=" + path);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Terms: Needle", result.StandardOutput);
            Assert.DoesNotContain("term=ToStringEx", result.StandardOutput);
            Assert.Contains("Match: label=probe term=Needle encoding=ascii count=2 offsets=0x0,0xB", result.StandardOutput);
            Assert.Contains("Displayed hits: 1 of 2 (max 1)", result.StandardOutput);
            Assert.Single(
                result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries),
                line => line.StartsWith("Hit:", StringComparison.Ordinal));
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task MissingInputsReturnUsageAndExitOne()
    {
        var result = await RunDataToolAsync("generated-buffer-string-scan");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains("requires at least one input file", result.StandardError);
        Assert.Contains($"Usage: {GeneratedBufferStringScanCommandText}", result.StandardError);
    }

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-generated-buffer-string-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static void DeleteTempRoot(string tempRoot)
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static async Task WriteFixtureBytesAsync(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes);
    }

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
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

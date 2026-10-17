using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolClientFileDriftTests
{
    [Fact]
    public async Task IdenticalSyntheticClientsReturnZeroAndPrintCleanSummary()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-file-drift-clean-" + Guid.NewGuid().ToString("N"));
        var originalClientPath = Path.Combine(tempRoot, "original");
        var patchedClientPath = Path.Combine(tempRoot, "patched");
        var configBytes = Encoding.ASCII.GetBytes("[Config]\r\nValue=1\r\n");
        var binaryBytes = new byte[] { 0x00, 0x10, 0x20 };

        try
        {
            await WriteClientFileAsync(originalClientPath, "ini/config.ini", configBytes);
            await WriteClientFileAsync(patchedClientPath, "ini/config.ini", configBytes);
            await WriteClientFileAsync(originalClientPath, "data/static.bin", binaryBytes);
            await WriteClientFileAsync(patchedClientPath, "data/static.bin", binaryBytes);

            var result = await RunDataToolAsync("client-file-drift", originalClientPath, patchedClientPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client file drift:", result.StandardOutput);
            Assert.Contains($"Original: {Path.GetFullPath(originalClientPath)}", result.StandardOutput);
            Assert.Contains($"Patched: {Path.GetFullPath(patchedClientPath)}", result.StandardOutput);
            Assert.Contains("Scope: All client files", result.StandardOutput);
            Assert.Contains("Include logs/debug/temp: False", result.StandardOutput);
            Assert.Contains("Changed files: 0", result.StandardOutput);
            Assert.Contains("Missing files: 0", result.StandardOutput);
            Assert.Contains("Extra files: 0", result.StandardOutput);
            Assert.Contains("No client file drift found.", result.StandardOutput);
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
    public async Task DriftReturnsTwoAndPrintsChangedMissingAndExtraMetadata()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-file-drift-changed-" + Guid.NewGuid().ToString("N"));
        var originalClientPath = Path.Combine(tempRoot, "original");
        var patchedClientPath = Path.Combine(tempRoot, "patched");
        var changedOriginalBytes = Encoding.ASCII.GetBytes("alpha");
        var changedPatchedBytes = Encoding.ASCII.GetBytes("bravo");
        var missingBytes = new byte[] { 1, 2, 3 };
        var extraBytes = new byte[] { 0xfe, 0xed };

        try
        {
            await WriteClientFileAsync(originalClientPath, "ini/config.ini", changedOriginalBytes);
            await WriteClientFileAsync(patchedClientPath, "ini/config.ini", changedPatchedBytes);
            await WriteClientFileAsync(originalClientPath, "ani/missing.c3", missingBytes);
            await WriteClientFileAsync(patchedClientPath, "data/extra.dat", extraBytes);

            var result = await RunDataToolAsync("client-file-drift", originalClientPath, patchedClientPath);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Client file drift:", result.StandardOutput);
            Assert.Contains("Changed files: 1", result.StandardOutput);
            Assert.Contains("Missing files: 1", result.StandardOutput);
            Assert.Contains("Extra files: 1", result.StandardOutput);
            Assert.Contains(
                $"changed ini/config.ini original length=5 sha256={Sha256Prefix(changedOriginalBytes)} patched length=5 sha256={Sha256Prefix(changedPatchedBytes)}",
                result.StandardOutput);
            Assert.Contains(
                $"missing ani/missing.c3 length=3 sha256={Sha256Prefix(missingBytes)}",
                result.StandardOutput);
            Assert.Contains(
                $"extra data/extra.dat length=2 sha256={Sha256Prefix(extraBytes)}",
                result.StandardOutput);
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
    public async Task DefaultExcludesLogsDebugAndTempUnlessIncludeLogsIsPassed()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-file-drift-logs-" + Guid.NewGuid().ToString("N"));
        var originalClientPath = Path.Combine(tempRoot, "original");
        var patchedClientPath = Path.Combine(tempRoot, "patched");

        try
        {
            await WriteClientFileAsync(originalClientPath, "logs/client.log", Encoding.ASCII.GetBytes("old log"));
            await WriteClientFileAsync(patchedClientPath, "logs/client.log", Encoding.ASCII.GetBytes("new log"));
            await WriteClientFileAsync(patchedClientPath, "cache/local.tmp", Encoding.ASCII.GetBytes("generated"));

            var defaultResult = await RunDataToolAsync("client-file-drift", originalClientPath, patchedClientPath);

            Assert.Equal(0, defaultResult.ExitCode);
            Assert.Contains("Changed files: 0", defaultResult.StandardOutput);
            Assert.Contains("Extra files: 0", defaultResult.StandardOutput);
            Assert.DoesNotContain("logs/client.log", defaultResult.StandardOutput);
            Assert.DoesNotContain("cache/local.tmp", defaultResult.StandardOutput);

            var includeLogsResult = await RunDataToolAsync(
                "client-file-drift",
                originalClientPath,
                patchedClientPath,
                "--include-logs");

            Assert.Equal(2, includeLogsResult.ExitCode);
            Assert.Contains("Include logs/debug/temp: True", includeLogsResult.StandardOutput);
            Assert.Contains("Changed files: 1", includeLogsResult.StandardOutput);
            Assert.Contains("Extra files: 1", includeLogsResult.StandardOutput);
            Assert.Contains("changed logs/client.log", includeLogsResult.StandardOutput);
            Assert.Contains("extra cache/local.tmp", includeLogsResult.StandardOutput);
            Assert.Equal(string.Empty, defaultResult.StandardError);
            Assert.Equal(string.Empty, includeLogsResult.StandardError);
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
    public async Task PatchFilesOnlyLimitsDriftToSuppliedCatalogTargets()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-file-drift-patch-files-" + Guid.NewGuid().ToString("N"));
        var originalClientPath = Path.Combine(tempRoot, "original");
        var patchedClientPath = Path.Combine(tempRoot, "patched");
        var changedOriginalBytes = Encoding.ASCII.GetBytes("original patch text");
        var changedPatchedBytes = Encoding.ASCII.GetBytes("patched english text");

        try
        {
            await WriteClientFileAsync(originalClientPath, "ini/Cn_Res.ini", changedOriginalBytes);
            await WriteClientFileAsync(patchedClientPath, "ini/Cn_Res.ini", changedPatchedBytes);
            await WriteClientFileAsync(originalClientPath, "data/unowned.bin", Encoding.ASCII.GetBytes("old"));
            await WriteClientFileAsync(patchedClientPath, "data/unowned.bin", Encoding.ASCII.GetBytes("new"));
            await WriteClientFileAsync(patchedClientPath, "data/generated-extra.bin", Encoding.ASCII.GetBytes("extra"));

            var result = await RunDataToolAsync(
                "client-file-drift",
                originalClientPath,
                patchedClientPath,
                "--patch-files-only",
                await WriteCatalogAsync(tempRoot));

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Scope: Supplied catalog files only", result.StandardOutput);
            Assert.Contains("Changed files: 1", result.StandardOutput);
            Assert.Contains("Missing files: 0", result.StandardOutput);
            Assert.Contains("Extra files: 0", result.StandardOutput);
            Assert.Contains(
                $"changed ini/Cn_Res.ini original length=19 sha256={Sha256Prefix(changedOriginalBytes)} patched length=20 sha256={Sha256Prefix(changedPatchedBytes)}",
                result.StandardOutput);
            Assert.DoesNotContain("data/unowned.bin", result.StandardOutput);
            Assert.DoesNotContain("data/generated-extra.bin", result.StandardOutput);
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
    public async Task MissingArgumentsReturnUsageAndExitOne()
    {
        var result = await RunDataToolAsync("client-file-drift");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains(
            "Usage: WolfConquer.DataTool client-file-drift <originalClientPath> <patchedClientPath> [--include-logs] [--patch-files-only <catalogJsonPath>]",
            result.StandardError);
    }

    [Fact]
    public async Task MissingClientPathReturnsClearErrorAndExitOne()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-file-drift-missing-" + Guid.NewGuid().ToString("N"));
        var originalClientPath = Path.Combine(tempRoot, "original");
        var patchedClientPath = Path.Combine(tempRoot, "patched");

        try
        {
            Directory.CreateDirectory(patchedClientPath);

            var result = await RunDataToolAsync("client-file-drift", originalClientPath, patchedClientPath);

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.Contains($"Original client path not found: {Path.GetFullPath(originalClientPath)}", result.StandardError);
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
    public async Task HelpIncludesClientFileDriftUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool client-file-drift <originalClientPath> <patchedClientPath> [--include-logs] [--patch-files-only <catalogJsonPath>]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task PatchFilesOnlyWithoutCatalogIsRejected()
    {
        var result = await RunDataToolAsync("client-file-drift", "original", "patched", "--patch-files-only");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("requires an explicit catalog", result.StandardError);
        Assert.Equal(string.Empty, result.StandardOutput);
    }

    [Fact]
    public async Task CatalogWithoutExistingTargetsCannotReportCleanComparison()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-drift-empty-synthetic-" + Guid.NewGuid().ToString("N"));
        var original = Path.Combine(root, "original");
        var patched = Path.Combine(root, "patched");
        Directory.CreateDirectory(original);
        Directory.CreateDirectory(patched);
        try
        {
            var result = await RunDataToolAsync("client-file-drift", original, patched,
                "--patch-files-only", await WriteCatalogAsync(root));
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("No supplied catalog files", result.StandardError);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<string> WriteCatalogAsync(string root)
    {
        var path = Path.Combine(root, "comparison-catalog.json");
        await File.WriteAllTextAsync(path, """
            [{"relativePath":"ini/Cn_Res.ini","encodingKind":"Utf8"}]
            """);
        return path;
    }

    private static async Task WriteClientFileAsync(string clientPath, string relativePath, byte[] bytes)
    {
        var path = Path.Combine(clientPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes);
    }

    private static string Sha256Prefix(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()[..12];
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

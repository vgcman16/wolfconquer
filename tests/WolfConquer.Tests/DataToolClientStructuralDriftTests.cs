using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class DataToolClientStructuralDriftTests
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    [Fact]
    public async Task CleanSyntheticPatchReturnsZeroAndPrintsCleanReport()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-structural-drift-clean-" + Guid.NewGuid().ToString("N"));
        var originalClientPath = Path.Combine(tempRoot, "original");
        var patchedClientPath = Path.Combine(tempRoot, "patched");

        try
        {
            await WriteClientFileAsync(
                originalClientPath,
                Path.Combine("ini", "GameAnnounce.ini"),
                """
                [Announcement]
                Message=Player %s won %d points
                """);
            await WriteClientFileAsync(
                patchedClientPath,
                Path.Combine("ini", "GameAnnounce.ini"),
                """
                [Announcement]
                Message=Hero %s keeps %d points
                """);

            var result = await RunDataToolAsync("client-structural-drift", originalClientPath, patchedClientPath,
                await WriteCatalogAsync(tempRoot, originalClientPath));

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client structural drift:", result.StandardOutput);
            Assert.Contains($"Original: {Path.GetFullPath(originalClientPath)}", result.StandardOutput);
            Assert.Contains($"Patched: {Path.GetFullPath(patchedClientPath)}", result.StandardOutput);
            Assert.Contains("Compared files: 1", result.StandardOutput);
            Assert.Contains("High-risk issues: 0", result.StandardOutput);
            Assert.Contains("No high-risk structural drift found.", result.StandardOutput);
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
    public async Task LostPrintfPlaceholderReturnsNonZeroAndPrintsIssue()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-structural-drift-issue-" + Guid.NewGuid().ToString("N"));
        var originalClientPath = Path.Combine(tempRoot, "original");
        var patchedClientPath = Path.Combine(tempRoot, "patched");

        try
        {
            await WriteClientFileAsync(
                originalClientPath,
                Path.Combine("ini", "GameAnnounce.ini"),
                """
                [Announcement]
                Message=Player %s won %d points
                """);
            await WriteClientFileAsync(
                patchedClientPath,
                Path.Combine("ini", "GameAnnounce.ini"),
                """
                [Announcement]
                Message=Player won %d points
                """);

            var result = await RunDataToolAsync("client-structural-drift", originalClientPath, patchedClientPath,
                await WriteCatalogAsync(tempRoot, originalClientPath));

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Client structural drift:", result.StandardOutput);
            Assert.Contains("Compared files: 1", result.StandardOutput);
            Assert.Contains("High-risk issues: 1", result.StandardOutput);
            Assert.Contains("ini/GameAnnounce.ini", result.StandardOutput);
            Assert.Contains("LostPrintfPlaceholder", result.StandardOutput);
            Assert.Contains("Line 2 lost printf placeholder %s", result.StandardOutput);
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
    public async Task OriginalOversizedLineWithoutPatchGrowthReturnsCleanReport()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-structural-drift-original-oversized-" + Guid.NewGuid().ToString("N"));
        var originalClientPath = Path.Combine(tempRoot, "original");
        var patchedClientPath = Path.Combine(tempRoot, "patched");
        var longOriginalLine = "STR_ORIGINAL_LONG=" + new string('A', EnglishClientStructuralDriftOptions.DefaultMaxLineBytes);

        try
        {
            await WriteClientFileAsync(
                originalClientPath,
                Path.Combine("ini", "Cn_Res.ini"),
                longOriginalLine);
            await WriteClientFileAsync(
                patchedClientPath,
                Path.Combine("ini", "Cn_Res.ini"),
                longOriginalLine);

            var result = await RunDataToolAsync("client-structural-drift", originalClientPath, patchedClientPath,
                await WriteCatalogAsync(tempRoot, originalClientPath));

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Compared files: 1", result.StandardOutput);
            Assert.Contains("High-risk issues: 0", result.StandardOutput);
            Assert.Contains("No high-risk structural drift found.", result.StandardOutput);
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
    public async Task MissingArgumentsReturnUsageAndNonZeroExit()
    {
        var result = await RunDataToolAsync("client-structural-drift");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains(
            "Usage: WolfConquer.DataTool client-structural-drift <originalClientPath> <patchedClientPath> <catalogJsonPath>",
            result.StandardError);
    }

    [Fact]
    public async Task HelpIncludesClientStructuralDriftUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool client-structural-drift <originalClientPath> <patchedClientPath> <catalogJsonPath>",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task MissingCatalogCannotReturnSuccessfulEmptyComparison()
    {
        var result = await RunDataToolAsync("client-structural-drift", "original", "patched");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("<catalogJsonPath>", result.StandardError);
        Assert.Equal(string.Empty, result.StandardOutput);
    }

    [Fact]
    public async Task PatchClientRequiresAndUsesExplicitSyntheticCatalog()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-cli-patch-synthetic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var target = Path.Combine(root, "sample.ini");
            await File.WriteAllTextAsync(target, "Value=original", Utf8NoBom);
            var missing = await RunDataToolAsync("patch-client", root);
            Assert.Equal(1, missing.ExitCode);
            Assert.Equal("Value=original", await File.ReadAllTextAsync(target));
            var catalog = Path.Combine(root, "catalog.json");
            await File.WriteAllTextAsync(catalog, """
                [{"relativePath":"sample.ini","encodingKind":"Utf8","replacements":[
                  {"key":"Value","value":"Synthetic output"}]}]
                """);
            var configured = await RunDataToolAsync("patch-client", root, catalog);
            Assert.Equal(0, configured.ExitCode);
            Assert.Equal("Value=Synthetic output", await File.ReadAllTextAsync(target));
            await File.WriteAllTextAsync(catalog, "[]");
            var empty = await RunDataToolAsync("patch-client", root, catalog);
            Assert.Equal(1, empty.ExitCode);
            Assert.Contains("nonempty", empty.StandardError);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DbTextCommandRequiresAndUsesExplicitSyntheticCatalog()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-cli-db-text-synthetic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var target = Path.Combine(root, "sample.ini");
            await File.WriteAllTextAsync(target, "[sample]\r\nName=old\r\n", Utf8NoBom);
            var missing = await RunDataToolAsync("patch-db-visible-text", root);
            Assert.Equal(1, missing.ExitCode);
            var catalog = Path.Combine(root, "catalog.json");
            await File.WriteAllTextAsync(catalog, """
                [{"relativePath":"sample.ini","section":"sample","key":"Name",
                  "sourceValue":"old","replacementValue":"Synthetic label","sourceEvidence":"Authored test fixture"}]
                """);
            var configured = await RunDataToolAsync("patch-db-visible-text", root, catalog);
            Assert.Equal(0, configured.ExitCode);
            Assert.Equal("[sample]\r\nName=Synthetic label\r\n", await File.ReadAllTextAsync(target));
            await File.WriteAllTextAsync(catalog, "[]");
            Assert.Equal(1, (await RunDataToolAsync("patch-db-visible-text", root, catalog)).ExitCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StrResCommandReportsUnresolvedTextWithoutBundledFallback()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-cli-strres-synthetic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "ini"));
        try
        {
            var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
            var target = Path.Combine(root, "ini", "StrRes.ini");
            var original = encoding.GetBytes("STR_SAMPLE=\u6d4b\u8bd5\r\n");
            await File.WriteAllBytesAsync(target, original);
            var missing = await RunDataToolAsync("patch-client-str-res-source-backed", root);
            Assert.Equal(2, missing.ExitCode);
            Assert.Equal(original, await File.ReadAllBytesAsync(target));
            var source = Path.Combine(root, "source.ini");
            await File.WriteAllTextAsync(source, "STR_SAMPLE=Synthetic label\r\n", Utf8NoBom);
            var configured = await RunDataToolAsync("patch-client-str-res-source-backed", root, source);
            Assert.Equal(0, configured.ExitCode);
            Assert.Equal("STR_SAMPLE=Synthetic label\r\n", await File.ReadAllTextAsync(target));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StatusTextCommandReportsUnmatchedRowsWithoutBundledFallback()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-cli-status-synthetic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "ini"));
        Directory.CreateDirectory(Path.Combine(root, "source"));
        try
        {
            var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
            var target = Path.Combine(root, "ini", "StatusTips.ini");
            var original = encoding.GetBytes("[31]\r\nName=\u7532\r\n");
            await File.WriteAllBytesAsync(target, original);
            var missing = await RunDataToolAsync("patch-client-status-text-source-backed", root);
            Assert.Equal(2, missing.ExitCode);
            Assert.Equal(original, await File.ReadAllBytesAsync(target));
            var source = Path.Combine(root, "source", "StatusTips.ini");
            await File.WriteAllTextAsync(source, "[31]\r\nName=Synthetic status\r\n", Utf8NoBom);
            var configured = await RunDataToolAsync("patch-client-status-text-source-backed", root, source);
            Assert.Equal(0, configured.ExitCode);
            Assert.Equal("[31]\r\nName=Synthetic status\r\n", await File.ReadAllTextAsync(target));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<string> WriteCatalogAsync(string root, string originalClientPath)
    {
        var rows = Directory.EnumerateFiles(originalClientPath, "*", SearchOption.AllDirectories)
            .Select(path => new { relativePath = Path.GetRelativePath(originalClientPath, path).Replace('\\', '/'), encodingKind = "Utf8" })
            .ToArray();
        var catalog = Path.Combine(root, "comparison-catalog.json");
        await File.WriteAllTextAsync(catalog, System.Text.Json.JsonSerializer.Serialize(rows));
        return catalog;
    }

    private static async Task WriteClientFileAsync(string clientPath, string relativePath, string text)
    {
        var path = Path.Combine(clientPath, relativePath);
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

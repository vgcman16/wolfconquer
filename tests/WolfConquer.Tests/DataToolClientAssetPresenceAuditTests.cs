using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace WolfConquer.Tests;

public sealed class DataToolClientAssetPresenceAuditTests
{
    [Fact]
    public async Task ReportsPresentMissingMatrixAcrossClientAndReferences()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-asset-presence-audit-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");
        var baselineRoot = Path.Combine(tempRoot, "baseline");
        var donorRoot = Path.Combine(tempRoot, "donor");

        try
        {
            Directory.CreateDirectory(clientRoot);
            Directory.CreateDirectory(baselineRoot);
            Directory.CreateDirectory(donorRoot);
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "data", "ItemMinIcon", "3313804.dds"), [1, 2, 3]);
            await WriteFixtureFileAsync(Path.Combine(baselineRoot, "data", "ItemMinIcon", "3313804.dds"), [4, 5, 6, 7, 8]);
            await WriteFixtureFileAsync(Path.Combine(donorRoot, "data", "MapItemIcon", "3313804.dds"), [9, 10, 11, 12, 13, 14, 15]);

            var result = await RunDataToolAsync(
                "client-asset-presence-audit",
                clientRoot,
                "data/ItemMinIcon/3313804.dds",
                "data\\MapItemIcon\\3313804.dds",
                "--reference",
                $"baseline={baselineRoot}",
                $"--reference=donor={donorRoot}");
            var output = NormalizeNewlines(result.StandardOutput);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client asset presence audit:", output);
            Assert.Contains($"Client root: {Path.GetFullPath(clientRoot)}", output);
            Assert.Contains($"References: baseline={Path.GetFullPath(baselineRoot)}, donor={Path.GetFullPath(donorRoot)}", output);
            Assert.Contains("Require client: False", output);
            Assert.Contains("Assets: 2", output);
            Assert.Contains(
                NormalizeNewlines("""
                asset data/ItemMinIcon/3313804.dds
                  client exists length=3
                  reference baseline exists length=5
                  reference donor missing
                """),
                output);
            Assert.Contains(
                NormalizeNewlines("""
                asset data/MapItemIcon/3313804.dds
                  client missing
                  reference baseline missing
                  reference donor exists length=7
                """),
                output);
            Assert.Contains("Missing client assets: 1", output);
            Assert.DoesNotContain("asset data\\MapItemIcon\\3313804.dds", output);
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
    public async Task RequireClientReturnsTwoWhenClientAssetIsMissing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-asset-presence-audit-required-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");

        try
        {
            Directory.CreateDirectory(clientRoot);

            var result = await RunDataToolAsync(
                "client-asset-presence-audit",
                clientRoot,
                "data/ItemMinIcon/3313804.dds",
                "--require-client");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Require client: True", result.StandardOutput);
            Assert.Contains("asset data/ItemMinIcon/3313804.dds", result.StandardOutput);
            Assert.Contains("  client missing", result.StandardOutput);
            Assert.Contains("Missing client assets: 1", result.StandardOutput);
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

    [Theory]
    [InlineData("C:/absolute/file.dds", "Asset path must be relative:")]
    [InlineData("/absolute/file.dds", "Asset path must be relative:")]
    [InlineData("../escape.dds", "Asset path cannot contain '..':")]
    [InlineData("data/../escape.dds", "Asset path cannot contain '..':")]
    public async Task RejectsAbsoluteAndTraversalAssetPaths(string assetPath, string expectedError)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-asset-presence-audit-invalid-path-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");

        try
        {
            Directory.CreateDirectory(clientRoot);

            var result = await RunDataToolAsync(
                "client-asset-presence-audit",
                clientRoot,
                assetPath);

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.Contains(expectedError, result.StandardError);
            Assert.Contains("Usage: WolfConquer.DataTool client-asset-presence-audit", result.StandardError);
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
    public async Task RejectsMalformedReferences()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-asset-presence-audit-invalid-reference-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");
        var referenceRoot = Path.Combine(tempRoot, "reference");
        var missingRoot = Path.Combine(tempRoot, "missing-reference");

        try
        {
            Directory.CreateDirectory(clientRoot);
            Directory.CreateDirectory(referenceRoot);

            var cases = new (string[] ExtraArgs, string ExpectedError)[]
            {
                (["--reference"], "Missing value for --reference."),
                (["--reference", "donor"], "--reference requires label=path."),
                (["--reference", $"={referenceRoot}"], "--reference label must be non-empty."),
                (["--reference", "donor="], "--reference path must be non-empty."),
                (["--reference", $"donor={missingRoot}"], "Reference path not found for 'donor':"),
            };

            foreach (var testCase in cases)
            {
                var arguments = new List<string>
                {
                    "client-asset-presence-audit",
                    clientRoot,
                    "data/ItemMinIcon/3313804.dds",
                };
                arguments.AddRange(testCase.ExtraArgs);

                var result = await RunDataToolAsync(arguments.ToArray());

                Assert.Equal(1, result.ExitCode);
                Assert.Equal(string.Empty, result.StandardOutput);
                Assert.Contains(testCase.ExpectedError, result.StandardError);
                Assert.Contains("Usage: WolfConquer.DataTool client-asset-presence-audit", result.StandardError);
            }
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
    public async Task HelpIncludesClientAssetPresenceAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool client-asset-presence-audit <clientRoot> <relativeAssetPath> [relativeAssetPath ...] [--reference label=path] [...] [--require-client]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
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

    private static async Task WriteFixtureFileAsync(string path, byte[] contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, contents);
    }

    private static string NormalizeNewlines(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        var directory = Path.GetDirectoryName(sourcePath);
        while (directory is not null && !Directory.Exists(Path.Combine(directory, "src", "WolfConquer.DataTool")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        return directory ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}

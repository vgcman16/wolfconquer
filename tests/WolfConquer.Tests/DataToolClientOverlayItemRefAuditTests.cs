using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace WolfConquer.Tests;

public sealed class DataToolClientOverlayItemRefAuditTests
{
    [Fact]
    public async Task ReportsSideTableItemIdsMissingFromPlaintextKnownItemSource()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-overlay-item-ref-audit-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");
        var dbServerRoot = Path.Combine(tempRoot, "DBServer");

        try
        {
            await WriteFixtureFileAsync(
                Path.Combine(dbServerRoot, "itemtype.txt"),
                """
                100001@@KnownItem@@
                720001@@KnownPrize@@
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "Activity.ini"),
                """
                [1]
                Name=Safe row
                RewardItem=100001
                AwardItem=999999
                OpenTime=20260704
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "ActivityTask.ini"),
                """
                [Task1]
                ItemTypeId=720001
                ItemTypeId=888888
                """);

            var result = await RunDataToolAsync(
                "client-overlay-item-ref-audit",
                clientRoot,
                dbServerRoot,
                "ini/Activity.ini",
                "ini/ActivityTask.ini");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Client overlay item ref audit:", result.StandardOutput);
            Assert.Contains("Known item ids: 2", result.StandardOutput);
            Assert.Contains("Side-table files: 2", result.StandardOutput);
            Assert.Contains("Files with missing item ids: 2", result.StandardOutput);
            Assert.Contains("Unique missing item ids: 2", result.StandardOutput);
            Assert.Contains("ini/Activity.ini: uniqueRefs=2 missing=1", result.StandardOutput);
            Assert.Contains("missing 999999 lines=4", result.StandardOutput);
            Assert.Contains("ini/ActivityTask.ini: uniqueRefs=2 missing=1", result.StandardOutput);
            Assert.Contains("missing 888888 lines=3", result.StandardOutput);
            Assert.DoesNotContain("20260704", result.StandardOutput);
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
    public async Task DirectoryRootUsesPlaintextItemTypeTxtAsKnownItemSource()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-overlay-item-ref-audit-baseline-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");
        var dbServerRoot = Path.Combine(tempRoot, "DBServer");

        try
        {
            await WriteFixtureFileAsync(
                Path.Combine(dbServerRoot, "itemtype.txt"),
                """
                100001@@KnownItem@@
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "Activity.ini"),
                """
                [1]
                RewardItem=100001
                """);

            var result = await RunDataToolAsync(
                "client-overlay-item-ref-audit",
                clientRoot,
                dbServerRoot,
                "ini/Activity.ini");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"Known item source: {Path.GetFullPath(Path.Combine(dbServerRoot, "itemtype.txt"))}", result.StandardOutput);
            Assert.Contains("ini/Activity.ini: uniqueRefs=1 missing=0", result.StandardOutput);
            Assert.Contains("No missing item ids found.", result.StandardOutput);
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
    public async Task BaselineClientRootUsesIniItemTypeDatAsKnownItemSource()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-overlay-item-ref-audit-client-root-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");
        var baselineRoot = Path.Combine(tempRoot, "baseline");

        try
        {
            await WriteFixtureFileAsync(
                Path.Combine(baselineRoot, "ini", "itemtype.dat"),
                """
                100001@@KnownItem@@
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "Activity.ini"),
                """
                [1]
                RewardItem=100001
                """);

            var result = await RunDataToolAsync(
                "client-overlay-item-ref-audit",
                clientRoot,
                baselineRoot,
                "ini/Activity.ini");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"Known item source: {Path.GetFullPath(Path.Combine(baselineRoot, "ini", "itemtype.dat"))}", result.StandardOutput);
            Assert.Contains("ini/Activity.ini: uniqueRefs=1 missing=0", result.StandardOutput);
            Assert.Contains("No missing item ids found.", result.StandardOutput);
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
    public async Task RejectsBinaryClientItemTypeDatAsKnownItemSource()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-overlay-item-ref-audit-binary-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");
        var itemTypeDat = Path.Combine(tempRoot, "ini", "itemtype.dat");

        try
        {
            Directory.CreateDirectory(clientRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(itemTypeDat)!);
            await File.WriteAllBytesAsync(itemTypeDat, [0x31, 0x00, 0x32, 0x00]);
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "ini", "Activity.ini"), "RewardItem=999999");

            var result = await RunDataToolAsync(
                "client-overlay-item-ref-audit",
                clientRoot,
                itemTypeDat,
                "ini/Activity.ini");

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.Contains("Text parsing is not supported for binary-looking sources", result.StandardError);
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
    public async Task HelpIncludesClientOverlayItemRefAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool client-overlay-item-ref-audit <clientRoot> <baselineClientRoot|knownItemSourcePath> <relativeSideTablePath> [relativeSideTablePath ...]",
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

    private static async Task WriteFixtureFileAsync(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, contents);
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

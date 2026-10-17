using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolClientStringRefAuditTests
{
    [Fact]
    public async Task ReportsMissingReferencedStringKeys()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-string-ref-audit-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");

        try
        {
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "instancemgr.lua"),
                """
                local ok = GetStr("STR_INSTANCE_DESC_101")
                local missing = GetStr('STR_INSTANCE_DESC_102')
                local generic = GetStr("STR_ACTIVITY_NAME")
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "Cn_Res.ini"),
                """
                STR_INSTANCE_DESC_101=Known instance
                STR_ACTIVITY_NAME=Known activity
                """);

            var result = await RunDataToolAsync(
                "client-string-ref-audit",
                clientRoot,
                "ini/instancemgr.lua",
                "ini/Cn_Res.ini");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Client string ref audit:", result.StandardOutput);
            Assert.Contains($"Client: {Path.GetFullPath(clientRoot)}", result.StandardOutput);
            Assert.Contains("Reference file: ini/instancemgr.lua", result.StandardOutput);
            Assert.Contains("Resource files: 1", result.StandardOutput);
            Assert.Contains("Referenced keys: 3", result.StandardOutput);
            Assert.Contains("Available keys: 2", result.StandardOutput);
            Assert.Contains("Missing keys: 1", result.StandardOutput);
            Assert.Contains("STR_INSTANCE_DESC_102", result.StandardOutput);
            Assert.DoesNotContain("STR_INSTANCE_DESC_101, STR_ACTIVITY_NAME", result.StandardOutput);
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
    public async Task CoversKeysAcrossMultipleResourceFilesAndCodePage936()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-string-ref-audit-multi-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");

        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "instancemgr.lua"),
                """
                return {
                  "STR_INSTANCE_DESC_201",
                  "STR_INSTANCE_DESC_202",
                  "STR_OTHER_LABEL",
                }
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "Cn_Res.ini"),
                "STR_INSTANCE_DESC_201=First\r\n");
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "Server_Cn_Res.ini"),
                "STR_INSTANCE_DESC_202=第二\r\nSTR_OTHER_LABEL=标签\r\n",
                Encoding.GetEncoding(936));

            var result = await RunDataToolAsync(
                "client-string-ref-audit",
                clientRoot,
                "ini/instancemgr.lua",
                "ini/Cn_Res.ini",
                "ini/Server_Cn_Res.ini");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Resource files: 2", result.StandardOutput);
            Assert.Contains("Referenced keys: 3", result.StandardOutput);
            Assert.Contains("Available keys: 3", result.StandardOutput);
            Assert.Contains("Missing keys: 0", result.StandardOutput);
            Assert.Contains("Missing key list: <none>", result.StandardOutput);
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
    public async Task FindsBareIniAssignedStringKeys()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-string-ref-audit-bare-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");

        try
        {
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "ItemQuenchTipInfo.ini"),
                """
                [320]
                Name=STR_INI_ITEM_QUENCH_TIPINFO_NAME_320
                Num=STR_INI_ITEM_QUENCH_TIPINFO_NUM_320
                Note=STR_INI_ITEM_QUENCH_TIPINFO_NOTE_320
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "Cn_Res.ini"),
                """
                STR_INI_ITEM_QUENCH_TIPINFO_NAME_320=Magic Defense Increase
                STR_INI_ITEM_QUENCH_TIPINFO_NUM_320=Level %d Magic Defense Increase:+%d%%,
                """);

            var result = await RunDataToolAsync(
                "client-string-ref-audit",
                clientRoot,
                "ini/ItemQuenchTipInfo.ini",
                "ini/Cn_Res.ini");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Referenced keys: 3", result.StandardOutput);
            Assert.Contains("Available keys: 2", result.StandardOutput);
            Assert.Contains("Missing keys: 1", result.StandardOutput);
            Assert.Contains("STR_INI_ITEM_QUENCH_TIPINFO_NOTE_320", result.StandardOutput);
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
    public async Task RejectsRelativePathsThatEscapeClientRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-string-ref-audit-badpath-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");

        try
        {
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "ini", "instancemgr.lua"), """GetStr("STR_INSTANCE_DESC_301")""");
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "ini", "Cn_Res.ini"), "STR_INSTANCE_DESC_301=Known\r\n");

            var result = await RunDataToolAsync(
                "client-string-ref-audit",
                clientRoot,
                @"..\outside.lua",
                "ini/Cn_Res.ini");

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.Contains("Reference file must be relative to the client root and must not escape it", result.StandardError);
            Assert.Contains("Usage: WolfConquer.DataTool client-string-ref-audit", result.StandardError);
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
    public async Task HelpIncludesClientStringRefAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool client-string-ref-audit <clientRoot> <relativeReferenceFile> <relativeResourceFile> [relativeResourceFile ...]",
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

    private static async Task WriteFixtureFileAsync(string path, string contents, Encoding? encoding = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, contents, encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolClientLoginCacheAuditTests
{
    [Fact]
    public async Task ReportsCacheArtifactsSetupSummariesAndNewestDebugWarningsWithoutIniValues()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-login-cache-audit-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");
        var rootRegkey = new byte[] { 0x10, 0x20, 0x30, 0x40 };
        var dx9Regkey = new byte[] { 0x50, 0x60, 0x70 };

        try
        {
            Directory.CreateDirectory(clientRoot);
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "regkey.dat"), rootRegkey);
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "Env_DX9", "regkey.dat"), dx9Regkey);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "GameSetUp.ini"),
                """
                [Login]
                AccountName=saved-user-should-not-print
                Password=hunter2-should-not-print
                LastServer=Dragon

                [Video]
                Width=1024
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "MsgBoxEx.ini"),
                """
                [Profile]
                RememberAccount=saved-user-should-not-print
                HiddenTip=do-not-print-this-value
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "LoginServerAccountCfg.ini"),
                """
                [Server]
                LastServer=Dragon
                AccountKey=secret-key-should-not-print
                Host=192.0.2.10
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "Log", "Dragon", "profile-a", "Setup.ini"),
                """
                [AccountProfile]
                SavedAccount=saved-user-should-not-print
                SavedPassword=hunter2-should-not-print
                LastServer=Dragon
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "debug", "2026_7_7.log"),
                "08:00:00 Can't Load File Which is old/missing.lua");
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "debug", "2026_7_8.log"),
                """
                08:10:00 Can't Load File Which is ini/activity_main.lua
                =========Lua error: [string "CLuaVM::requirebuffer()"]:86: too many C levels (limit is 200) in function at line 68 near 'ToStringEx' -- Wed Jul  8 08:10:01 2026
                """);

            SetLastWriteUtc(Path.Combine(clientRoot, "regkey.dat"), 2026, 7, 8, 1, 2, 3);
            SetLastWriteUtc(Path.Combine(clientRoot, "Env_DX9", "regkey.dat"), 2026, 7, 8, 1, 2, 4);
            SetLastWriteUtc(Path.Combine(clientRoot, "ini", "GameSetUp.ini"), 2026, 7, 8, 1, 2, 5);
            SetLastWriteUtc(Path.Combine(clientRoot, "Log", "Dragon", "profile-a", "Setup.ini"), 2026, 7, 8, 1, 2, 6);
            SetLastWriteUtc(Path.Combine(clientRoot, "debug", "2026_7_7.log"), 2026, 7, 7, 1, 2, 7);
            SetLastWriteUtc(Path.Combine(clientRoot, "debug", "2026_7_8.log"), 2026, 7, 8, 1, 2, 8);

            var result = await RunDataToolAsync("client-login-cache-audit", clientRoot);
            var output = NormalizeNewlines(result.StandardOutput);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client login cache audit:", output);
            Assert.Contains($"Client root: {Path.GetFullPath(clientRoot)}", output);
            Assert.Contains("Binary candidates: present=2 missing=1", output);
            Assert.Contains(
                $"binary regkey.dat exists length=4 mtimeUtc=2026-07-08 01:02:03 sha256={Sha256Prefix(rootRegkey)}",
                output);
            Assert.Contains("binary Env_DX8/regkey.dat missing", output);
            Assert.Contains(
                $"binary Env_DX9/regkey.dat exists length=3 mtimeUtc=2026-07-08 01:02:04 sha256={Sha256Prefix(dx9Regkey)}",
                output);
            Assert.Contains("INI candidates: present=3 missing=0", output);
            Assert.Contains(
                "ini ini/GameSetUp.ini exists length=",
                output);
            Assert.Contains("sections=Login(AccountName[value-redacted],Password[value-redacted],LastServer)", output);
            Assert.Contains("ini ini/MsgBoxEx.ini exists length=", output);
            Assert.Contains("sections=Profile(RememberAccount[value-redacted],HiddenTip)", output);
            Assert.Contains("ini ini/LoginServerAccountCfg.ini exists length=", output);
            Assert.Contains("sections=Server(LastServer,AccountKey[value-redacted],Host)", output);
            Assert.Contains("Setup.ini summaries: 1/1", output);
            Assert.Contains(
                "setup server=Dragon profile=profile-a Log/Dragon/profile-a/Setup.ini exists length=",
                output);
            Assert.Contains(
                "sections=AccountProfile(SavedAccount[value-redacted],SavedPassword[value-redacted],LastServer)",
                output);
            Assert.Contains("Newest debug log:", output);
            Assert.Contains("debug-log debug/2026_7_8.log length=", output);
            Assert.Contains("mtimeUtc=2026-07-08 01:02:08 lines=2 warnings=2 firstWarning=08:10:00 lastWarning=08:10:01 counts=LuaError:1,MissingFile:1", output);
            Assert.DoesNotContain("saved-user-should-not-print", output);
            Assert.DoesNotContain("hunter2-should-not-print", output);
            Assert.DoesNotContain("secret-key-should-not-print", output);
            Assert.DoesNotContain("do-not-print-this-value", output);
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
    public async Task ReportsOpaqueBlobDiagnosticsForAccEuiTmpWithoutPrintingBlobStrings()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-login-cache-audit-blob-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");
        var repeatedHeader = new byte[] { 0xca, 0xfe, 0xba, 0xbe };
        var asciiPayload = Encoding.ASCII.GetBytes(
            "login=visible-screen\0server=Dragon\0profile=TestSrv\0user=saved-user-should-not-print\0pwd=hunter2-should-not-print\0");
        var utf16LePayload = Encoding.Unicode.GetBytes("world=Dragon profile=TestSrv server=China");
        var alignmentPadding = asciiPayload.Length % 2 == 0 ? Array.Empty<byte>() : new byte[] { 0x00 };
        var accEuiBlob = repeatedHeader
            .Concat(repeatedHeader)
            .Concat(repeatedHeader)
            .Concat(repeatedHeader)
            .Concat(asciiPayload)
            .Concat(alignmentPadding)
            .Concat(utf16LePayload)
            .ToArray();

        try
        {
            Directory.CreateDirectory(clientRoot);
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "Cache", "slotaccEui.tmp"), accEuiBlob);
            SetLastWriteUtc(Path.Combine(clientRoot, "Cache", "slotaccEui.tmp"), 2026, 7, 8, 2, 3, 4);

            var result = await RunDataToolAsync("client-login-cache-audit", clientRoot);
            var output = NormalizeNewlines(result.StandardOutput);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Binary candidates: present=1 missing=3 accEuiTmp=1 accEuiTmpOmitted=0", output);
            Assert.Contains(
                $"binary Cache/slotaccEui.tmp exists length={accEuiBlob.Length} mtimeUtc=2026-07-08 02:03:04 sha256={Sha256Prefix(accEuiBlob)}",
                output);
            Assert.Contains(
                $"blob Cache/slotaccEui.tmp Decode: unsupported/opaque inspectedBytes={accEuiBlob.Length} truncated=false header16Sha256={Sha256Prefix(accEuiBlob.Take(16).ToArray())}",
                output);
            Assert.Contains("leadRepeat4=4 leadRepeat8=2", output);
            Assert.Contains("asciiTokens=5", output);
            Assert.Contains("utf16leTokens=1", output);
            Assert.Contains("asciiLabels=login,profile,server", output);
            Assert.Contains("utf16leLabels=profile,server,world", output);
            Assert.Contains("values=redacted", output);
            Assert.DoesNotContain("visible-screen", output);
            Assert.DoesNotContain("Dragon", output);
            Assert.DoesNotContain("TestSrv", output);
            Assert.DoesNotContain("China", output);
            Assert.DoesNotContain("saved-user-should-not-print", output);
            Assert.DoesNotContain("hunter2-should-not-print", output);
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
    public async Task MissingClientRootReturnsUsage()
    {
        var missingRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-login-cache-audit-missing-" + Guid.NewGuid().ToString("N"));

        var result = await RunDataToolAsync("client-login-cache-audit", missingRoot);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains($"Client root not found: {Path.GetFullPath(missingRoot)}", result.StandardError);
        Assert.Contains("Usage: WolfConquer.DataTool client-login-cache-audit <clientRoot>", result.StandardError);
    }

    [Fact]
    public async Task HelpIncludesClientLoginCacheAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool client-login-cache-audit <clientRoot>",
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

    private static async Task WriteFixtureFileAsync(string path, byte[] contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, contents);
    }

    private static void SetLastWriteUtc(string path, int year, int month, int day, int hour, int minute, int second)
    {
        File.SetLastWriteTimeUtc(path, new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc));
    }

    private static string Sha256Prefix(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()[..12];
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

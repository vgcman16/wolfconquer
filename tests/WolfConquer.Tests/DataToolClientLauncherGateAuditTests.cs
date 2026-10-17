using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace WolfConquer.Tests;

public sealed class DataToolClientLauncherGateAuditTests
{
    [Fact]
    public async Task ReportsLauncherFieldsVersionMd5PathsAndRecentHints()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-launcher-gate-audit-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");
        var logPath = Path.Combine(clientRoot, "debug", "tqan_2026_07_05.log");

        try
        {
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "AutoPatch", "config.ini"),
                """
                [Launcher]
                Main=Conquer.exe
                Process=Update.exe
                Patch=AutoPatch.exe

                [PatchServer]
                Url=http://patch.example.test/7899/
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "GameInfo.ini"),
                """
                [1]
                GamePath=C:\fixtures\client-1
                GameProcess=._cache_Conquer.exe
                """);
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "version.dat"), "7899\r\n");
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "data", "present.dat"), "present");
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "ini", "missing.ini.del"), "deleted sibling");
            await WriteFixtureFileAsync(
                logPath,
                """
                2026-07-05 08:00:01 Update.exe start
                2026-07-05 08:00:02 Downloading=data/present.dat
                2026-07-05 08:00:03 MD5 Get File Error: data/present.dat
                2026-07-05 08:00:04 MD5 Get File Error: ini\missing.ini
                2026-07-05 08:00:05 Load File Error: ini\missing.ini
                FileName : ini\paired.ini
                MD5=Get File Error
                """);

            var result = await RunDataToolAsync("client-launcher-gate-audit", clientRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client launcher gate audit:", result.StandardOutput);
            Assert.Contains($"Client root: {Path.GetFullPath(clientRoot)}", result.StandardOutput);
            Assert.Contains("AutoPatch/config.ini: exists=true", result.StandardOutput);
            Assert.Contains("Launcher main fields: 1", result.StandardOutput);
            Assert.Contains("[Launcher] Main=Conquer.exe", result.StandardOutput);
            Assert.Contains("Launcher process fields: 1", result.StandardOutput);
            Assert.Contains("[Launcher] Process=Update.exe", result.StandardOutput);
            Assert.Contains("Launcher patch fields: 2", result.StandardOutput);
            Assert.Contains("[Launcher] Patch=AutoPatch.exe", result.StandardOutput);
            Assert.Contains("[PatchServer] Url=http://patch.example.test/7899/", result.StandardOutput);
            Assert.Contains("GameInfo.ini: exists=true GamePath=C:\\fixtures\\client-1 GameProcess=._cache_Conquer.exe", result.StandardOutput);
            Assert.Contains("ini/GameInfo.ini: exists=false GamePath=<missing> GameProcess=<missing>", result.StandardOutput);
            Assert.Contains("GameInfo comparison: status=single-present", result.StandardOutput);
            Assert.Contains("version.dat: exists=true version=7899", result.StandardOutput);
            Assert.Contains("Updater local routing patch state: 2", result.StandardOutput);
            Assert.Contains("Update.exe: exists=false oldSafeloginUrl=false localhostUrl=false", result.StandardOutput);
            Assert.Contains("._cache_Update.exe: exists=false oldSafeloginUrl=false localhostUrl=false", result.StandardOutput);
            Assert.Contains("Debug tqan logs considered: 1", result.StandardOutput);
            Assert.Contains("MD5 get file error paths: 3", result.StandardOutput);
            Assert.Contains("data/present.dat count=1 exists=true delSibling=false", result.StandardOutput);
            Assert.Contains("ini/missing.ini count=1 exists=false delSibling=true", result.StandardOutput);
            Assert.Contains("ini/paired.ini count=1 exists=false delSibling=false", result.StandardOutput);
            Assert.Contains("Recent update/download/error hints: 6", result.StandardOutput);
            Assert.Contains("[update] 2026-07-05 08:00:01 Update.exe start", result.StandardOutput);
            Assert.Contains("[download] 2026-07-05 08:00:02 Downloading=data/present.dat", result.StandardOutput);
            Assert.Contains("[error] 2026-07-05 08:00:05 Load File Error: ini\\missing.ini", result.StandardOutput);
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
    public async Task ReportsRootAndIniGameInfoMismatch()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-launcher-gate-audit-gameinfo-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");

        try
        {
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "GameInfo.ini"),
                """
                [1]
                GamePath=C:\fixtures\client-2
                GameProcess=._cache_Conquer.exe
                """);
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "GameInfo.ini"),
                """
                [1]
                GamePath=client
                GameProcess=Conquer.exe
                """);

            var result = await RunDataToolAsync("client-launcher-gate-audit", clientRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("GameInfo.ini: exists=true GamePath=C:\\fixtures\\client-2 GameProcess=._cache_Conquer.exe", result.StandardOutput);
            Assert.Contains("ini/GameInfo.ini: exists=true GamePath=client GameProcess=Conquer.exe", result.StandardOutput);
            Assert.Contains("GameInfo comparison: status=mismatch", result.StandardOutput);
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
    public async Task ReportsUpdaterLocalRoutingPatchBytes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-launcher-gate-audit-updaters-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");

        try
        {
            Directory.CreateDirectory(clientRoot);
            await WriteFixtureBytesAsync(
                Path.Combine(clientRoot, "Update.exe"),
                "prefix https://example.invalid/legacy-updater/ suffix");
            await WriteFixtureBytesAsync(
                Path.Combine(clientRoot, "._cache_Update.exe"),
                "prefix http://127.0.0.1/ndlogin/zfplay/ suffix");

            var result = await RunDataToolAsync("client-launcher-gate-audit", clientRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Updater local routing patch state: 2", result.StandardOutput);
            Assert.Contains("Update.exe: exists=true oldSafeloginUrl=true localhostUrl=false", result.StandardOutput);
            Assert.Contains("._cache_Update.exe: exists=true oldSafeloginUrl=false localhostUrl=true", result.StandardOutput);
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
    public async Task MissingOptionalLauncherInputsStillAuditsClientRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-launcher-gate-audit-missing-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");

        try
        {
            Directory.CreateDirectory(clientRoot);

            var result = await RunDataToolAsync("client-launcher-gate-audit", clientRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("AutoPatch/config.ini: exists=false", result.StandardOutput);
            Assert.Contains("Launcher main fields: 0", result.StandardOutput);
            Assert.Contains("GameInfo.ini: exists=false GamePath=<missing> GameProcess=<missing>", result.StandardOutput);
            Assert.Contains("ini/GameInfo.ini: exists=false GamePath=<missing> GameProcess=<missing>", result.StandardOutput);
            Assert.Contains("GameInfo comparison: status=both-missing", result.StandardOutput);
            Assert.Contains("version.dat: exists=false version=<missing>", result.StandardOutput);
            Assert.Contains("Updater local routing patch state: 2", result.StandardOutput);
            Assert.Contains("Update.exe: exists=false oldSafeloginUrl=false localhostUrl=false", result.StandardOutput);
            Assert.Contains("._cache_Update.exe: exists=false oldSafeloginUrl=false localhostUrl=false", result.StandardOutput);
            Assert.Contains("Debug tqan logs considered: 0", result.StandardOutput);
            Assert.Contains("MD5 get file error paths: 0", result.StandardOutput);
            Assert.Contains("Recent update/download/error hints: 0", result.StandardOutput);
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
    public async Task HelpIncludesClientLauncherGateAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool client-launcher-gate-audit <clientRoot> [--top N]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task MissingClientRootReturnsUsageAndExitOne()
    {
        var result = await RunDataToolAsync("client-launcher-gate-audit");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains("Missing client root.", result.StandardError);
        Assert.Contains("Usage: WolfConquer.DataTool client-launcher-gate-audit <clientRoot> [--top N]", result.StandardError);
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

    private static async Task WriteFixtureFileAsync(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, contents);
    }

    private static async Task WriteFixtureBytesAsync(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, System.Text.Encoding.ASCII.GetBytes(contents));
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

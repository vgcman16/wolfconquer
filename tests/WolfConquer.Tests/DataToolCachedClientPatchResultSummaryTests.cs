using System.Runtime.CompilerServices;
using System.Text.Json;

namespace WolfConquer.Tests;

public sealed class DataToolCachedClientPatchResultSummaryTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    [Fact]
    public async Task HelpMentionsCachedClientPatchResultSummaryCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool cached-client-patch-result-summary <patchResultJsonPath> [patchResultJsonPath ...]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task CachedClientPatchResultSummaryReportsCompactPatchArtifactRow()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var evidencePath = Path.Combine(tempRoot, "wer-artifact.json");
            await WriteWerPatchResultAsync(evidencePath);

            var result = await RunDataToolAsync("cached-client-patch-result-summary", evidencePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Cached client patch result summary:", result.StandardOutput);
            Assert.Contains(
                "file=wer-artifact.json responding=True mainWindow=0x0 " +
                "counts(window=2 exception=1 route=2 finalRoute=0 log=1 tcp=1) " +
                "hiddenWer=True knownFallback=True delayedRoutes=2 modulePatches=2",
                result.StandardOutput);
            Assert.Contains(
                "patches=datathread-33A456-force patched=True already=False before=0F 83 C5 DE D3 FF fail=<none>; " +
                "datathread-second patched=False already=False before=?? fail=expected bytes mismatch",
                result.StandardOutput);
            Assert.Contains("outcome=hidden-wer", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientPatchResultSummarySupportsRootPayloadAliasesAndMultiplePaths()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var firstPath = Path.Combine(tempRoot, "clean-root.json");
            var secondPath = Path.Combine(tempRoot, "wer-artifact.json");
            await WriteRootAliasPatchResultAsync(firstPath);
            await WriteWerPatchResultAsync(secondPath);

            var result = await RunDataToolAsync("cached-client-patch-result-summary", firstPath, secondPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(
                "file=clean-root.json responding=False mainWindow=0x1034 " +
                "counts(window=0 exception=0 route=0 finalRoute=0 log=0 tcp=2) " +
                "hiddenWer=True knownFallback=<missing> delayedRoutes=<missing> modulePatches=0 patches=<none>",
                result.StandardOutput);
            Assert.Contains("file=wer-artifact.json responding=True mainWindow=0x0", result.StandardOutput);
            Assert.Equal(3, SplitOutputLines(result.StandardOutput).Length);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientPatchResultSummaryClassifiesHiddenWerCleanNegativeAndRouteMovement()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var hiddenWerPath = Path.Combine(tempRoot, "hidden-wer-zero-movement.json");
            var cleanNegativePath = Path.Combine(tempRoot, "clean-negative.json");
            var routeMovedPath = Path.Combine(tempRoot, "route-moved.json");
            await WriteFocusedPatchResultAsync(
                hiddenWerPath,
                processEvidenceAfterObserve:
                [
                    new
                    {
                        Name = "WerFault.exe",
                        CommandLine = "C:\\WINDOWS\\SysWOW64\\WerFault.exe -u -p 60372 -s 884",
                    },
                ]);
            await WriteFocusedPatchResultAsync(cleanNegativePath);
            await WriteFocusedPatchResultAsync(
                routeMovedPath,
                routeEvidenceAfterObserve:
                [
                    new { Remote = "10.0.0.116:9865" },
                ]);

            var result = await RunDataToolAsync(
                "cached-client-patch-result-summary",
                hiddenWerPath,
                cleanNegativePath,
                routeMovedPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(
                "file=hidden-wer-zero-movement.json responding=True mainWindow=0x0 " +
                "counts(window=0 exception=0 route=0 finalRoute=0 log=0 tcp=0) " +
                "hiddenWer=True knownFallback=<missing> delayedRoutes=<missing> modulePatches=0 " +
                "patches=<none> outcome=hidden-wer",
                result.StandardOutput);
            Assert.Contains(
                "file=clean-negative.json responding=True mainWindow=0x0 " +
                "counts(window=0 exception=0 route=0 finalRoute=0 log=0 tcp=0) " +
                "hiddenWer=False knownFallback=<missing> delayedRoutes=<missing> modulePatches=0 " +
                "patches=<none> outcome=clean-negative",
                result.StandardOutput);
            Assert.Contains(
                "file=route-moved.json responding=True mainWindow=0x0 " +
                "counts(window=0 exception=0 route=1 finalRoute=0 log=0 tcp=0) " +
                "hiddenWer=False knownFallback=<missing> delayedRoutes=<missing> modulePatches=0 " +
                "patches=<none> outcome=route-moved",
                result.StandardOutput);
            Assert.Equal(4, SplitOutputLines(result.StandardOutput).Length);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static async Task WriteWerPatchResultAsync(string path)
    {
        var evidence = new
        {
            Result = new
            {
                ProcessRespondingAfterObserve = true,
                MainWindowHandleAfterObserve = "0x0",
                WindowEvidenceAfterObserve = new object[]
                {
                    new
                    {
                        Title = "WER Fault",
                        ClassName = "WerFaultWndClass",
                        Visible = false,
                    },
                    new
                    {
                        Title = "Default IME",
                        ClassName = "IME",
                        Visible = false,
                    },
                },
                ExceptionAddressEvidenceAfterObserve = new object[]
                {
                    new
                    {
                        ExceptionAddress = "0xDEAD",
                    },
                },
                RouteEvidenceAfterObserve = new object[]
                {
                    new { Remote = "192.0.2.10:9865" },
                    new { Remote = "10.0.0.116:9865" },
                },
                FinalRouteEvidenceAfterObserve = Array.Empty<object>(),
                LogEvidenceAfterObserve = new object[]
                {
                    new { Path = "debug.log" },
                },
                ClientTcpConnectionsAfterObserve = new object[]
                {
                    new { RemoteAddress = "10.0.0.116" },
                },
                KnownFallbackRoutesPatched = true,
                DelayedRoutePatches = new object[]
                {
                    new { Name = "route-1" },
                    new { Name = "route-2" },
                },
                ModuleMemoryPatchesBeforeRoutePatch = new object[]
                {
                    new
                    {
                        Name = "datathread-33A456-force",
                        Patched = true,
                        AlreadyPatched = false,
                        BeforeHex = "0F 83 C5 DE D3 FF",
                        FailureReason = string.Empty,
                    },
                    new
                    {
                        Name = "datathread-second",
                        Patched = false,
                        AlreadyPatched = false,
                        Before = "??",
                        FailureReason = "expected bytes mismatch",
                    },
                },
            },
        };

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(evidence, JsonOptions));
    }

    private static async Task WriteRootAliasPatchResultAsync(string path)
    {
        var evidence = new
        {
            ProcessRespondingAfterObserve = false,
            MainWindowHandleAfterObserve = "0x1034",
            WindowEvidenceAfterObserve = Array.Empty<object>(),
            ExceptionEvidenceAfterObserve = Array.Empty<object>(),
            RouteEvidenceAfterObserve = Array.Empty<object>(),
            FinalRouteEvidenceAfterObserve = Array.Empty<object>(),
            LogEvidenceAfterObserve = Array.Empty<object>(),
            TcpConnectionsAfterObserve = new object[]
            {
                new { RemoteAddress = "10.0.0.116" },
                new { RemoteAddress = "127.0.0.1" },
            },
            ProcessEvidenceAfterObserve = new object[]
            {
                new
                {
                    Name = "WerFault.exe",
                    CommandLine = "C:\\WINDOWS\\SysWOW64\\WerFault.exe -u -p 60372 -s 884",
                },
            },
            ModuleMemoryPatchesBeforeRoutePatch = Array.Empty<object>(),
        };

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(evidence, JsonOptions));
    }

    private static async Task WriteFocusedPatchResultAsync(
        string path,
        object[]? processEvidenceAfterObserve = null,
        object[]? routeEvidenceAfterObserve = null)
    {
        var result = new Dictionary<string, object?>
        {
            ["ProcessRespondingAfterObserve"] = true,
            ["MainWindowHandleAfterObserve"] = "0x0",
            ["WindowEvidenceAfterObserve"] = Array.Empty<object>(),
            ["ExceptionEvidenceAfterObserve"] = Array.Empty<object>(),
            ["RouteEvidenceAfterObserve"] = routeEvidenceAfterObserve ?? Array.Empty<object>(),
            ["FinalRouteEvidenceAfterObserve"] = Array.Empty<object>(),
            ["LogEvidenceAfterObserve"] = Array.Empty<object>(),
            ["ClientTcpConnectionsAfterObserve"] = Array.Empty<object>(),
            ["ModuleMemoryPatchesBeforeRoutePatch"] = Array.Empty<object>(),
        };

        if (processEvidenceAfterObserve is not null)
        {
            result["ProcessEvidenceAfterObserve"] = processEvidenceAfterObserve;
        }

        var evidence = new Dictionary<string, object?>
        {
            ["Result"] = result,
        };

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(evidence, JsonOptions));
    }

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-cached-client-patch-result-summary-" + Guid.NewGuid().ToString("N"));
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

    private static string[] SplitOutputLines(string output)
    {
        return output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
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

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
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

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolClientLauncherSafetyAuditTests
{
    private static readonly string[] LikelyRelativePaths =
    [
        "AutoP.exe",
        "._cache_AutoP.exe",
        "._cache_Conquer.exe",
        "Env_DX9/Conquer.exe",
        "Env_DX9/._cache_Conquer.exe",
        "Env_DX8/Conquer.exe",
        "Env_DX8/._cache_Conquer.exe",
        "drvup.dat",
        "drvup.exe",
    ];

    [Fact]
    public async Task CleanLikelyFilesExitZeroAndReportNoIndicators()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-launcher-safety-clean-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");

        try
        {
            await WriteCleanLikelyFilesAsync(clientRoot);

            var result = await RunDataToolAsync("client-launcher-safety-audit", clientRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client launcher safety audit:", result.StandardOutput);
            Assert.Contains($"Client root: {Path.GetFullPath(clientRoot)}", result.StandardOutput);
            Assert.Contains("Mode: read-only byte/string scan; no client executables launched.", result.StandardOutput);
            Assert.Contains("Evidence paths supplied: 0", result.StandardOutput);
            Assert.Contains("Scan targets: 9", result.StandardOutput);
            Assert.Contains("High-risk hits: 0", result.StandardOutput);
            Assert.Contains("Known NetDragon driver marker hits: 0", result.StandardOutput);
            Assert.Contains("AutoP.exe: exists=true", result.StandardOutput);
            Assert.Contains("._cache_AutoP.exe: exists=true", result.StandardOutput);
            Assert.Contains("._cache_Conquer.exe: exists=true", result.StandardOutput);
            Assert.Contains("Env_DX9/Conquer.exe: exists=true", result.StandardOutput);
            Assert.Contains("Env_DX9/._cache_Conquer.exe: exists=true", result.StandardOutput);
            Assert.Contains("Env_DX8/Conquer.exe: exists=true", result.StandardOutput);
            Assert.Contains("Env_DX8/._cache_Conquer.exe: exists=true", result.StandardOutput);
            Assert.Contains("drvup.dat: exists=true", result.StandardOutput);
            Assert.Contains("drvup.exe: exists=true", result.StandardOutput);
            Assert.Contains("hitCount=0 highRiskHitCount=0 indicators=none", result.StandardOutput);
            Assert.Contains("Result: no high-risk indicators found", result.StandardOutput);
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
    public async Task SuspiciousIndicatorsExitNonzeroAndReportExactMatchedNames()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-launcher-safety-suspect-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");

        try
        {
            await WriteFixtureBytesAsync(
                Path.Combine(clientRoot, "AutoP.exe"),
                "\0harmless prefix Synaptics.exe middle xred.mooo.com suffix");
            await WriteFixtureBytesAsync(
                Path.Combine(clientRoot, "Env_DX9", "Conquer.exe"),
                Encoding.Unicode.GetBytes("wide string AUTORUNINJ marker"));
            await WriteFixtureBytesAsync(
                Path.Combine(clientRoot, "Env_DX8", "Conquer.exe"),
                "clean client bytes");
            await WriteFixtureBytesAsync(
                Path.Combine(clientRoot, "drvup.dat"),
                "callback https://example.invalid/dynamic-route");

            var result = await RunDataToolAsync("client-launcher-safety-audit", clientRoot);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("High-risk hits: 4", result.StandardOutput);
            Assert.Contains("Known NetDragon driver marker hits: 0", result.StandardOutput);
            Assert.Contains("AutoP.exe: exists=true", result.StandardOutput);
            Assert.Contains("indicators=Synaptics.exe=1,xred.mooo.com=1", result.StandardOutput);
            Assert.Contains("Env_DX9/Conquer.exe: exists=true", result.StandardOutput);
            Assert.Contains("indicators=AUTORUNINJ=1", result.StandardOutput);
            Assert.Contains("drvup.dat: exists=true", result.StandardOutput);
            Assert.Contains("https://example.invalid/dynamic-route=1", result.StandardOutput);
            Assert.Contains("Result: high-risk indicators found", result.StandardOutput);
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
    public async Task QuarantinedEvidencePathIsScannedWhenSupplied()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-launcher-safety-evidence-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");
        var evidencePath = Path.Combine(tempRoot, "quarantine", "AutoP.exe.quarantine");

        try
        {
            await WriteCleanLikelyFilesAsync(clientRoot);
            await WriteFixtureBytesAsync(evidencePath, "captured SSLLibrary.dll and autorun.inf evidence");

            var result = await RunDataToolAsync(
                "client-launcher-safety-audit",
                clientRoot,
                "--quarantined-evidence-path",
                evidencePath);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Evidence paths supplied: 1", result.StandardOutput);
            Assert.Contains($"quarantined-evidence: {Path.GetFullPath(evidencePath)}", result.StandardOutput);
            Assert.Contains("Scan targets: 10", result.StandardOutput);
            Assert.Contains("quarantined-evidence:AutoP.exe.quarantine: exists=true", result.StandardOutput);
            Assert.Contains("autorun.inf=1", result.StandardOutput);
            Assert.Contains("SSLLibrary.dll=1", result.StandardOutput);
            Assert.Contains("Result: high-risk indicators found", result.StandardOutput);
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
    public async Task QuarantinedEvidenceDirectoryScansContainedFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-launcher-safety-evidence-dir-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");
        var evidenceRoot = Path.Combine(tempRoot, "quarantine");

        try
        {
            await WriteCleanLikelyFilesAsync(clientRoot);
            await WriteFixtureBytesAsync(Path.Combine(evidenceRoot, "Synaptics.exe"), "captured Synaptics.exe evidence");
            await WriteFixtureBytesAsync(Path.Combine(evidenceRoot, "nested", "note.bin"), "nested xredline evidence");

            var result = await RunDataToolAsync(
                "client-launcher-safety-audit",
                clientRoot,
                "--quarantined-evidence-path",
                evidenceRoot);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Evidence paths supplied: 1", result.StandardOutput);
            Assert.Contains($"quarantined-evidence: {Path.GetFullPath(evidenceRoot)}", result.StandardOutput);
            Assert.Contains("Scan targets: 11", result.StandardOutput);
            Assert.Contains("quarantined-evidence:Synaptics.exe: exists=true", result.StandardOutput);
            Assert.Contains("quarantined-evidence:nested/note.bin: exists=true", result.StandardOutput);
            Assert.Contains("Synaptics.exe=1", result.StandardOutput);
            Assert.Contains("xredline=1", result.StandardOutput);
            Assert.Contains("Result: high-risk indicators found", result.StandardOutput);
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
    public async Task TqhookAloneReportsKnownMarkerWithoutHighRiskFailure()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-launcher-safety-tqhook-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");

        try
        {
            await WriteCleanLikelyFilesAsync(clientRoot);
            await WriteFixtureBytesAsync(Path.Combine(clientRoot, "drvup.dat"), "known NetDragon driver marker tqhook only");

            var result = await RunDataToolAsync("client-launcher-safety-audit", clientRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("High-risk hits: 0", result.StandardOutput);
            Assert.Contains("Known NetDragon driver marker hits: 1", result.StandardOutput);
            Assert.Contains("drvup.dat: exists=true", result.StandardOutput);
            Assert.Contains("hitCount=1 highRiskHitCount=0 indicators=tqhook=1", result.StandardOutput);
            Assert.Contains("Result: no high-risk indicators found", result.StandardOutput);
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
    public async Task TargetOnlyScansTargetPathWithoutLikelyLauncherFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-launcher-safety-target-only-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");
        var targetPath = Path.Combine(clientRoot, "Env_DX9", "._cache_Conquer.exe");

        try
        {
            await WriteFixtureBytesAsync(Path.Combine(clientRoot, "AutoP.exe"), "Synaptics.exe should be ignored in target-only mode");
            await WriteFixtureBytesAsync(Path.Combine(clientRoot, "Env_DX9", "Conquer.exe"), "xred.mooo.com should be ignored in target-only mode");
            await WriteFixtureBytesAsync(targetPath, "clean cached client");

            var result = await RunDataToolAsync(
                "client-launcher-safety-audit",
                clientRoot,
                "--target-only",
                "--target-path",
                targetPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Mode: target-only read-only byte/string scan; no client executables launched.", result.StandardOutput);
            Assert.Contains("Evidence paths supplied: 1", result.StandardOutput);
            Assert.Contains($"target: {Path.GetFullPath(targetPath)}", result.StandardOutput);
            Assert.Contains("Scan targets: 1", result.StandardOutput);
            Assert.Contains("target:._cache_Conquer.exe: exists=true", result.StandardOutput);
            Assert.Contains("High-risk hits: 0", result.StandardOutput);
            Assert.Contains("Result: no high-risk indicators found", result.StandardOutput);
            Assert.DoesNotContain("AutoP.exe: exists=true", result.StandardOutput);
            Assert.DoesNotContain("Env_DX9/Conquer.exe: exists=true", result.StandardOutput);
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
    public async Task TargetOnlyStillFailsWhenTargetHasHighRiskIndicators()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-launcher-safety-target-risk-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");
        var targetPath = Path.Combine(clientRoot, "Env_DX9", "._cache_Conquer.exe");

        try
        {
            await WriteFixtureBytesAsync(targetPath, "bad cached target AUTORUNINJ");

            var result = await RunDataToolAsync(
                "client-launcher-safety-audit",
                clientRoot,
                "--target-only",
                "--target-path",
                targetPath);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Scan targets: 1", result.StandardOutput);
            Assert.Contains("High-risk hits: 1", result.StandardOutput);
            Assert.Contains("target:._cache_Conquer.exe: exists=true", result.StandardOutput);
            Assert.Contains("indicators=AUTORUNINJ=1", result.StandardOutput);
            Assert.Contains("Result: high-risk indicators found", result.StandardOutput);
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
    public async Task TargetOnlyWithoutTargetPathIsRejected()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-launcher-safety-target-missing-" + Guid.NewGuid().ToString("N"));
        var clientRoot = Path.Combine(tempRoot, "client");

        try
        {
            Directory.CreateDirectory(clientRoot);

            var result = await RunDataToolAsync(
                "client-launcher-safety-audit",
                clientRoot,
                "--target-only");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("--target-only requires at least one --target-path or evidence path.", result.StandardError);
            Assert.Contains("Usage: WolfConquer.DataTool client-launcher-safety-audit", result.StandardError);
            Assert.Equal(string.Empty, result.StandardOutput);
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
    public async Task HelpIncludesClientLauncherSafetyAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool client-launcher-safety-audit <clientRoot> [--target-only] [--target-path path] [--evidence-path path] [--live-evidence-path path] [--quarantined-evidence-path path]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    private static async Task WriteCleanLikelyFilesAsync(string clientRoot)
    {
        foreach (var relativePath in LikelyRelativePaths)
        {
            await WriteFixtureBytesAsync(
                Path.Combine(clientRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)),
                "harmless fixture bytes for " + relativePath);
        }
    }

    private static async Task WriteFixtureBytesAsync(string path, string contents)
    {
        await WriteFixtureBytesAsync(path, Encoding.ASCII.GetBytes(contents));
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

using System.Diagnostics;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class WorldServerWorldDataStartupGuardrailTests
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);
    private static readonly string WorldServerProjectPath =
        Path.Combine("src", "WolfConquer.WorldServer", "WolfConquer.WorldServer.csproj");

    [Theory]
    [InlineData("--listen")]
    [InlineData("--LiStEn")]
    public async Task MissingWorldSeedListenModeFailsClosedBeforeListening(string listenArgument)
    {
        var seedRoot = CreateEmptySeedRoot();
        try
        {
            var result = await RunWorldServerAsync(
                seedRoot,
                listenArgument,
                "--address",
                "127.0.0.1",
                "--port",
                "0");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains($"World data seed root: {Path.GetFullPath(seedRoot)}", result.StandardOutput);
            Assert.Contains("World data not loaded; manifest.json is missing.", result.StandardOutput);
            Assert.DoesNotContain("Listening on ", result.StandardOutput, StringComparison.Ordinal);

            var errorLine = Assert.Single(SplitLines(result.StandardError));
            Assert.Equal(
                "WorldServer cannot start in --listen mode because world data failed to load. " +
                $"Export the world seed data or set {WorldDataOptions.SeedJsonEnvironmentVariable} to a valid seed root.",
                errorLine);
        }
        finally
        {
            Directory.Delete(seedRoot, recursive: true);
        }
    }

    [Fact]
    public async Task MissingWorldSeedDefaultModePreservesDiagnosticsAndExitsSuccessfully()
    {
        var seedRoot = CreateEmptySeedRoot();
        try
        {
            var result = await RunWorldServerAsync(seedRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("World data not loaded; manifest.json is missing.", result.StandardOutput);
            Assert.Contains(
                $"Planned listen endpoint: {ConquerRuntimeDefaults.WorldServer.Address}:{ConquerRuntimeDefaults.WorldServer.Port}",
                result.StandardOutput);
            Assert.Contains("Pass --listen to start accepting TCP clients.", result.StandardOutput);
            Assert.DoesNotContain("Listening on ", result.StandardOutput, StringComparison.Ordinal);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
        }
        finally
        {
            Directory.Delete(seedRoot, recursive: true);
        }
    }

    private static string CreateEmptySeedRoot()
    {
        var seedRoot = Path.Combine(Path.GetTempPath(), $"wolf-world-missing-seed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(seedRoot);
        return seedRoot;
    }

    private static async Task<ConsoleRunResult> RunWorldServerAsync(string seedRoot, params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = TestProjectProcess.CreateFromProjectPath(repositoryRoot, WorldServerProjectPath);
        process.StartInfo.Environment[WorldDataOptions.SeedJsonEnvironmentVariable] = seedRoot;
        process.StartInfo.Environment["WOLFCONQUER_ACCOUNT_MYSQL"] = string.Empty;
        process.StartInfo.Environment["WOLFCONQUER_ACCOUNT_EMAIL_MYSQL"] = string.Empty;
        process.StartInfo.Environment[ConquerProtocolProfileConfiguration.EnvironmentVariable] =
            nameof(ConquerProtocolProfile.RootD11_7899);
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Could not start WorldServer.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(StartupTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail(
                $"WorldServer did not exit within {StartupTimeout}. " +
                $"stdout: {await outputTask} stderr: {await errorTask}");
        }

        return new ConsoleRunResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output directory.");
    }

    private static string[] SplitLines(string text)
    {
        return text
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd())
            .ToArray();
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}

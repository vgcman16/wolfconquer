using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// An actual disposable WorldServer process, not a listener/client login test.
public sealed class WorldServerLocalIdentityStartupTests
{
    [Theory]
    [InlineData("not-a-server-id")]
    [InlineData("4294967296")]
    public async Task InvalidExplicitIdentityExitsBeforeDataLoadingOrListeningWithoutEchoingItsValue(string value)
    {
        var repository = FindRepositoryRoot();
        using var process = TestProjectProcess.Create(repository, "WolfConquer.WorldServer");
        process.StartInfo.Environment[LegacyLocalServerIdentity.ServerIdEnvironmentVariable] = value;
        process.StartInfo.Environment[ConquerProtocolProfileConfiguration.EnvironmentVariable] =
            nameof(ConquerProtocolProfile.VisibleDx8D13);
        process.StartInfo.Environment[WorldDataOptions.SeedJsonEnvironmentVariable] =
            Path.Combine(Path.GetTempPath(), "wolf-unused-identity-seed-" + Guid.NewGuid().ToString("N"));
        process.StartInfo.Environment["WOLFCONQUER_ACCOUNT_MYSQL"] = string.Empty;
        process.StartInfo.Environment["WOLFCONQUER_ACCOUNT_EMAIL_MYSQL"] = string.Empty;

        Assert.True(process.Start());
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // This is the process created and retained by this test, never a
            // PID discovered from an unrelated running deployment.
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail("The disposable WorldServer identity-validation process did not exit.");
        }

        var output = await stdout;
        var error = await stderr;
        Assert.Equal(1, process.ExitCode);
        Assert.Equal(string.Empty, output);
        Assert.Equal("Invalid WOLFCONQUER_LOCAL_SERVER_ID; expected an unsigned 32-bit decimal server ID." +
            Environment.NewLine, error);
        Assert.DoesNotContain(value, error, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output directory.");
    }
}

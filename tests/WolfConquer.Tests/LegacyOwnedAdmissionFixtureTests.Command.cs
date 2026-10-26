using System.Diagnostics;
using System.Globalization;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedAdmissionFixtureTests
{
    [Fact]
    public async Task ActualDataToolCommandCreatesOnlyTheExplicitSyntheticFilesAndIgnoresUnrelatedAuthenticationConfiguration()
    {
        var result = await RunDataToolAsync("seed-owned-admission-fixture", Destination,
            AccountUid.ToString(CultureInfo.InvariantCulture), AccountUid.ToString(CultureInfo.InvariantCulture), ItemUid.ToString(CultureInfo.InvariantCulture));

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Contains("fixtureRoot=" + Destination, result.Output);
        Assert.Contains("ownedAdmissionConfig=" + ConfigurationPath, result.Output);
        Assert.Contains("no account password, authentication fallback, admin authority, server startup or live-client proof", result.Output);
        Assert.DoesNotContain("owned-unused-auth-configuration", result.Output + result.Error);
        AssertSnapshotSemantics(Destination);
        Assert.True(LegacyOwnedAdmissionOptions.TryReadFile(ConfigurationPath, out var parsed, out var reason), reason);
        var options = Assert.IsType<LegacyOwnedAdmissionOptions>(parsed);
        Assert.Contains("expectedDomain=" + options.ExpectedDomain.ToString("N"), result.Output);
        AssertInitialNamespace(Destination, options.ExpectedDomain);
        Assert.False(Directory.Exists(Path.Combine(parent, "must-not-create-auth-authority")));
        Assert.Equal(new[] { Destination }, Directory.GetFileSystemEntries(parent));
        var files = Files(Destination);

        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(options.CreateSnapshotLoader(), options, out var opened, out reason), reason);
        using (Assert.IsType<LegacyOwnedAdmissionRuntime>(opened)) AssertFiles(Destination, files);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("not-an-id")]
    [InlineData("4294967296")]
    public async Task ActualCommandRejectsMalformedIdsBeforeCreatingAnyPartialRoot(string id)
    {
        var result = await RunDataToolAsync("seed-owned-admission-fixture", Destination, id, id,
            ItemUid.ToString(CultureInfo.InvariantCulture));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Usage:", result.Error);
        Assert.DoesNotContain("created; offline preparation", result.Output);
        Assert.Empty(Directory.GetFileSystemEntries(parent));
    }

    [Fact]
    public async Task ActualCommandReportsExistingTargetRefusalWithoutReplacingItOrAnnouncingSuccess()
    {
        var fixture = Create();
        var files = Files(Destination);

        var result = await RunDataToolAsync("seed-owned-admission-fixture", Destination,
            AccountUid.ToString(CultureInfo.InvariantCulture), AccountUid.ToString(CultureInfo.InvariantCulture), ItemUid.ToString(CultureInfo.InvariantCulture));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("fixture refused:", result.Error);
        Assert.Contains("incomplete root or sibling staging directory is retained", result.Error);
        Assert.DoesNotContain("created; offline preparation", result.Output);
        AssertFiles(Destination, files);
        AssertInitialNamespace(Destination, fixture.DomainId);
        Assert.False(Directory.Exists(Staging));
    }

    [Fact]
    public async Task HelpAndMissingArgumentsExposeTheExplicitNewRootCommandWithoutCreatingOrProbingAnything()
    {
        var help = await RunDataToolAsync("help");
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("seed-owned-admission-fixture <NEW-absolute-wolf-owned-admission-GUID-dir>", help.Output);

        var missing = await RunDataToolAsync("seed-owned-admission-fixture");
        Assert.Equal(1, missing.ExitCode);
        Assert.Contains("Usage:", missing.Error);
        Assert.Empty(Directory.GetFileSystemEntries(parent));
    }

    private async Task<CommandResult> RunDataToolAsync(params string[] arguments)
    {
        var repository = FindRepositoryRoot();
        using var process = TestProjectProcess.Create(repository, "WolfConquer.DataTool");
        process.StartInfo.WorkingDirectory = parent;
        foreach (var key in process.StartInfo.Environment.Keys.Where(key => key.StartsWith("WOLFCONQUER_", StringComparison.OrdinalIgnoreCase)).ToArray())
            process.StartInfo.Environment.Remove(key);
        // Deliberately unusable synthetic markers: success must not require or
        // parse account/SQL/handoff credentials for this offline file command.
        process.StartInfo.Environment["WOLFCONQUER_ACCOUNT_MYSQL"] = "owned-unused-auth-configuration";
        process.StartInfo.Environment["WOLFCONQUER_ACCOUNT_EMAIL_MYSQL"] = "owned-unused-auth-configuration";
        process.StartInfo.Environment[AccountHandoffOptions.StoreRootEnvironmentVariable] = Path.Combine(parent, "must-not-create-auth-authority");
        process.StartInfo.Environment[AccountHandoffOptions.SigningSecretEnvironmentVariable] = "owned-unused-auth-configuration";
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start());
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail("Owned DataTool fixture command did not terminate.");
        }
        return new(process.ExitCode, await output, await error);
    }

    private static string FindRepositoryRoot()
    {
        for (var path = new DirectoryInfo(AppContext.BaseDirectory); path is not null; path = path.Parent)
            if (File.Exists(Path.Combine(path.FullName, "WolfConquer.sln"))) return path.FullName;
        throw new DirectoryNotFoundException("Could not locate the already-built test repository.");
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}

using System.Security.Cryptography;
using System.Text.Json;

namespace WolfConquer.Tests;

public sealed partial class SqlGameplayCheckToolsTests
{
    [Theory]
    [InlineData("Debug")]
    [InlineData("Release")]
    public async Task ActualPlanReportsExactOwnedCommandFromScriptRootWithoutExecutingInputs(string configuration)
    {
        var before = Snapshot();
        var enabled = Environment.GetEnvironmentVariable("WOLF_SQL_AUTH_FIXTURE_ENABLED");
        var connection = Environment.GetEnvironmentVariable("WOLF_SQL_AUTH_FIXTURE_ADMIN_CONNECTION");
        var result = await RunAsync("-Port", "34121", "-MariaDbHome", Path.Combine(root, "inert db"),
            "-Configuration", configuration, "-Plan");
        Assert.True(result.ExitCode == 0, $"Exit {result.ExitCode}; stderr: {result.Error}; stdout: {result.Output}");
        Assert.Equal("", result.Error);
        using var json = JsonDocument.Parse(result.Output);
        var plan = json.RootElement;
        Assert.Equal(1, plan.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("owned-sql-gameplay", plan.GetProperty("suite").GetString());
        Assert.Equal("not-run", plan.GetProperty("outcome").GetString());
        Assert.False(plan.GetProperty("fullGate").GetBoolean());
        Assert.Equal("plan", plan.GetProperty("mode").GetString());
        Assert.Equal(root, plan.GetProperty("repositoryRoot").GetString()!.TrimEnd(Path.DirectorySeparatorChar));
        Assert.Equal(Project, plan.GetProperty("project").GetString());
        Assert.Equal(configuration, plan.GetProperty("configuration").GetString());
        Assert.Equal(34121, plan.GetProperty("port").GetInt32());
        Assert.Equal(Path.Combine(root, "inert db"), plan.GetProperty("mariaDbHome").GetString());
        Assert.Equal(Path.Combine(root, "tools", "dev", "Invoke-OwnedSqlAuthFixture.ps1"), plan.GetProperty("ownedLifecycle").GetString());
        const string filter = "FullyQualifiedName~WolfConquer.Tests.ModernSqlAuthFileRoundtripTests";
        Assert.Equal(filter, plan.GetProperty("filter").GetString());
        var command = plan.GetProperty("command");
        Assert.Equal("dotnet", command.GetProperty("executable").GetString());
        Assert.Equal(new[] { "test", Project, "-c", configuration, "--nologo", "-m:1", "-nodeReuse:false",
            "/p:UseSharedCompilation=false", "--filter", filter, "--results-directory", "<owned-sql-root>",
            "--logger", "trx;LogFileName=qualification.trx", "--blame-hang-timeout", "180s",
            "--blame-hang-dump-type", "none" }, command.GetProperty("arguments").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(enabled, Environment.GetEnvironmentVariable("WOLF_SQL_AUTH_FIXTURE_ENABLED"));
        Assert.Equal(connection, Environment.GetEnvironmentVariable("WOLF_SQL_AUTH_FIXTURE_ADMIN_CONNECTION"));
        AssertSnapshot(before);
    }

    [Fact]
    public async Task DefaultPortIsNeverImplicitAndProductionPort3306IsRefusedBeforeArtifacts()
    {
        var before = Snapshot();
        var missing = await RunAsync("-Plan");
        Assert.NotEqual(0, missing.ExitCode);
        AssertSnapshot(before);
        var forbidden = await RunAsync("-Port", "3306", "-Plan");
        Assert.NotEqual(0, forbidden.ExitCode);
        Assert.Contains("3306", forbidden.Error, StringComparison.OrdinalIgnoreCase);
        AssertSnapshot(before);
    }

    [Fact]
    public async Task MissingExplicitRootIsRejectedWithoutCreatingIt()
    {
        var absent = Path.Combine(root, "absent repository");
        var before = Snapshot();
        var result = await RunAsync("-RepositoryRoot", absent, "-Port", "34121", "-Plan");
        Assert.NotEqual(0, result.ExitCode);
        Assert.False(Directory.Exists(absent));
        AssertSnapshot(before);
    }

    [Fact]
    public async Task MissingProjectAndMissingRealExecutionSupportRefuseBeforeArtifactsOrChildren()
    {
        File.Delete(Project);
        var before = Snapshot();
        var missingProject = await RunAsync("-Port", "34121", "-Plan");
        Assert.NotEqual(0, missingProject.ExitCode);
        AssertSnapshot(before);
        File.WriteAllText(Project, "<Project><!-- synthetic plan only; never build --></Project>");
        before = Snapshot();
        var module = Path.Combine(root, "tools", "dev", "FocusedCheckSupport.psm1");
        File.Delete(module);
        before = Snapshot();
        var missingModule = await RunAsync("-Port", "34121", "-MariaDbHome", Path.Combine(root, "inert db"));
        Assert.NotEqual(0, missingModule.ExitCode);
        Assert.Contains("FocusedCheckSupport.psm1", missingModule.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Plan must not", missingModule.Error, StringComparison.Ordinal);
        AssertSnapshot(before);
        File.WriteAllText(module, "throw 'Plan must not import support'");
        before = Snapshot();
        // Execution-only files are absent. Existing ordinary lifecycle/module inputs
        // throw if imported; the synthetic project must never reach dotnet.
        var missingSupport = await RunAsync("-Port", "34121", "-MariaDbHome", Path.Combine(root, "inert db"));
        Assert.NotEqual(0, missingSupport.ExitCode);
        Assert.Contains("FocusedCheckProcess.cs", missingSupport.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Plan must not", missingSupport.Error, StringComparison.Ordinal);
        AssertSnapshot(before);
    }

    private Dictionary<string, byte[]> Snapshot() => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(root, path), path => SHA256.HashData(File.ReadAllBytes(path)), StringComparer.Ordinal);

    private void AssertSnapshot(Dictionary<string, byte[]> before)
    {
        var after = Snapshot();
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var (path, hash) in before) Assert.Equal(hash, after[path]);
        Assert.False(Directory.Exists(Path.Combine(root, ".artifacts")));
    }
}




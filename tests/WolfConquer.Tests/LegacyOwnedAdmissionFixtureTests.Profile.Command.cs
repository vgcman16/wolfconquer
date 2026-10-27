using System.Globalization;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedAdmissionFixtureTests
{
    [Fact]
    public async Task LegacyCliRetainsClassTenDespiteAnAmbientD13Profile()
    {
        var result = await RunProfileCommandAsync(null, "VisibleDx8D13");
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.DoesNotContain("fixtureProfile=", result.Output);
        AssertProfileInitial(ReadProfileOptions(), 10);
    }

    [Theory]
    [InlineData("RootD11_7899", "VisibleDx8D13", 10L)]
    [InlineData("VisibleDx8D13", "RootD11_7899", 1000L)]
    public async Task ExplicitCliProfileOverridesNoGlobalSettingAndProducesOnlyItsSelectedSavedClass(
        string selected, string ambient, long expectedClass)
    {
        var result = await RunProfileCommandAsync(selected, ambient);
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Contains("fixtureProfile=" + selected, result.Output);
        AssertProfileInitial(ReadProfileOptions(), expectedClass);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("visibledx8d13")]
    [InlineData(" VisibleDx8D13 ")]
    [InlineData("RootD11")]
    [InlineData("unsupported")]
    public async Task InvalidCliSelectorDoesNotPublishOrCreatePartialFixtureOutput(string selected)
    {
        var result = await RunProfileCommandAsync(selected, "VisibleDx8D13");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Usage:", result.Error);
        Assert.DoesNotContain("created; offline preparation", result.Output);
        Assert.DoesNotContain("fixtureProfile=", result.Output);
        Assert.False(Directory.Exists(Destination));
        Assert.False(Directory.Exists(Staging));
        Assert.False(File.Exists(ConfigurationPath));
        Assert.Empty(Directory.GetFileSystemEntries(parent));
    }

    private async Task<LegacyOwnedAdmissionOptions> CreateD13ProfileAsync(bool useCli)
    {
        if (useCli)
        {
            var command = await RunProfileCommandAsync("VisibleDx8D13", "RootD11_7899");
            Assert.Equal(0, command.ExitCode);
            Assert.Empty(command.Error);
        }
        else Assert.True(LegacyOwnedAdmissionFixture.TryCreate(Destination, AccountUid, AccountUid, ItemUid,
            ConquerProtocolProfile.VisibleDx8D13, out _, out var reason), reason);
        return ReadProfileOptions();
    }

    private async Task<CommandResult> RunProfileCommandAsync(string? selected, string ambient)
    {
        using var process = TestProjectProcess.Create(FindRepositoryRoot(), "WolfConquer.DataTool");
        process.StartInfo.WorkingDirectory = parent;
        foreach (var key in process.StartInfo.Environment.Keys.Where(key => key.StartsWith("WOLFCONQUER_", StringComparison.OrdinalIgnoreCase)).ToArray())
            process.StartInfo.Environment.Remove(key);
        process.StartInfo.Environment[ConquerProtocolProfileConfiguration.EnvironmentVariable] = ambient;
        foreach (var argument in new[] { "seed-owned-admission-fixture", Destination,
            AccountUid.ToString(CultureInfo.InvariantCulture), AccountUid.ToString(CultureInfo.InvariantCulture),
            ItemUid.ToString(CultureInfo.InvariantCulture) }) process.StartInfo.ArgumentList.Add(argument);
        if (selected is not null)
        { process.StartInfo.ArgumentList.Add("--profile"); process.StartInfo.ArgumentList.Add(selected); }
        Assert.True(process.Start());
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail("Owned profile fixture CLI did not terminate.");
        }
        return new(process.ExitCode, await stdout, await stderr);
    }
}

using System.Security.Cryptography;
using WolfConquer.Updater;
using WolfConquer.Updates.ClientLayout;

namespace WolfConquer.Updates.Tests;

public sealed partial class UpdaterPipelineTests
{
    [WindowsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FreshSignedClientCreatesOnlyExplicitlyOptedInLayoutWithoutLaunchingSyntheticExecutable(bool optIn) => WithAssetLayoutFixture(async f =>
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        PublishLayout(f, key);
        string config = WriteLayoutConfig(f, key, optIn ? LayoutPreset : null);
        byte[] originalConfig = File.ReadAllBytes(config);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var result = await LauncherWorkflow.RunClientAsync(config, fresh: true, cancellationToken: deadline.Token);
        Assert.Equal("updated", result.Status);
        Assert.Equal(12, result.Installation!.InstalledFiles);
        Assert.Null(result.LaunchedProcessId);
        Assert.True(result.LaunchCandidateAvailable);
        Assert.Equal(f.Executable, File.ReadAllBytes(f.ClientFile(LayoutEngine)));
        foreach (string name in LayoutAssets)
        {
            Assert.Equal("owned " + name, File.ReadAllText(f.ClientFile(name + "/asset.bin")));
            if (optIn) Assert.True(ClientDirectoryJunction.IsExact(LayoutAliasPath(f, name), LayoutTargetPath(f, name)));
            else Assert.False(Directory.Exists(LayoutAliasPath(f, name)));
        }
        var settings = LauncherWorkflow.ReadClientSettings(config);
        Assert.Equal(optIn ? LayoutPreset : null, settings.ClientAssetLayoutPreset);
        Assert.Equal("Env_DX8", settings.LaunchWorkingDirectoryRelativePath);
        Assert.Equal(originalConfig, File.ReadAllBytes(config));
    });

    [WindowsTheory]
    [InlineData("unknown-preset", "Env_DX8")]
    [InlineData(LayoutPreset, null)]
    [InlineData(LayoutPreset, "data")]
    [InlineData(LayoutPreset, "env_dx8")]
    public Task InvalidLocalPresetOrCwdRefusesBeforeTargetCacheOrControlWrites(string preset, string? cwd) => WithAssetLayoutFixture(async f =>
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        PublishLayout(f, key);
        string config = WriteLayoutConfig(f, key, preset, cwd);
        var before = Fixture.Snapshot(f.Root);

        Assert.Throws<InvalidDataException>(() => LauncherWorkflow.ReadClientSettings(config));
        await Assert.ThrowsAsync<InvalidDataException>(() => LauncherWorkflow.RunClientAsync(config, fresh: true));

        Fixture.AssertSnapshot(before, Fixture.Snapshot(f.Root));
        Assert.False(Directory.Exists(f.Client));
        Assert.False(Directory.Exists(Path.Combine(f.Bootstrap, ".wolfconquer-cache")));
    });

    [WindowsFact]
    public Task SignedAliasPayloadIsRejectedByActualOptedInPipelineBeforeAnyPayloadIsInstalled() => WithAssetLayoutFixture(async f =>
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        PublishLayout(f, key, "Env_DX8/data/rogue.bin");
        string config = WriteLayoutConfig(f, key);
        byte[] configuration = File.ReadAllBytes(config);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await Assert.ThrowsAsync<InvalidDataException>(() => LauncherWorkflow.RunClientAsync(config,
            fresh: true, cancellationToken: deadline.Token));
        Assert.All(LayoutAssets, name => Assert.False(Directory.Exists(f.ClientFile(name))));
        Assert.False(File.Exists(f.ClientFile(LayoutEngine)));
        Assert.False(File.Exists(f.ClientFile(".wolfconquer-updates/state.json")));
        Assert.False(Directory.Exists(Path.Combine(f.Bootstrap, ".wolfconquer-cache")));
        Assert.Equal(configuration, File.ReadAllBytes(config));
    });

    [WindowsFact]
    public Task OptedInPresetDoesNotAuthorizeInstallingIntoMissingRootWithoutFreshConsent() => WithAssetLayoutFixture(async f =>
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        PublishLayout(f, key);
        string config = WriteLayoutConfig(f, key);
        var before = Fixture.Snapshot(f.Root);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => LauncherWorkflow.RunClientAsync(config));

        Fixture.AssertSnapshot(before, Fixture.Snapshot(f.Root));
        Assert.False(Directory.Exists(f.Client));
    });

    [WindowsFact]
    public Task MetadataOnlyUnchangedCheckDoesNotRecreateMissingAliasButExplicitRepairDoes() => WithAssetLayoutFixture(async f =>
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        PublishLayout(f, key);
        string config = WriteLayoutConfig(f, key);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await LauncherWorkflow.RunClientAsync(config, fresh: true, cancellationToken: deadline.Token);
        string alias = LayoutAliasPath(f, "c3");
        ClientDirectoryJunction.DeleteExact(alias, LayoutTargetPath(f, "c3"));
        byte[] state = File.ReadAllBytes(f.ClientFile(".wolfconquer-updates/state.json"));
        Assert.False(Directory.Exists(alias));

        var unchanged = await LauncherWorkflow.RunClientAsync(config, cancellationToken: deadline.Token);

        Assert.Equal("unchanged", unchanged.Status);
        Assert.True(unchanged.MetadataOnly);
        Assert.False(unchanged.LaunchCandidateAvailable);
        Assert.Null(unchanged.Installation);
        Assert.False(Directory.Exists(alias));
        Assert.Equal(state, File.ReadAllBytes(f.ClientFile(".wolfconquer-updates/state.json")));

        var repaired = await LauncherWorkflow.RunClientAsync(config, repair: true, cancellationToken: deadline.Token);

        Assert.Equal("verified", repaired.Status);
        Assert.False(repaired.MetadataOnly);
        Assert.True(repaired.LaunchCandidateAvailable);
        Assert.Null(repaired.LaunchedProcessId);
        Assert.True(ClientDirectoryJunction.IsExact(alias, LayoutTargetPath(f, "c3")));
        Assert.Equal(state, File.ReadAllBytes(f.ClientFile(".wolfconquer-updates/state.json")));
    });
}

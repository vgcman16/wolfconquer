using System.Security.Cryptography;
using WolfConquer.Updates.ClientLayout;
using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.Tests;

public sealed partial class ClientAssetLayoutTests
{
    [WindowsFact]
    public async Task LaterSignedAssetUpdateWorksWithoutTreatingUnlistedEngineAliasesAsPayloadAuthority()
    {
        using var f = new LayoutFixture(); await f.Install(); await f.Prepare();
        var next = f.Release(2, ("data/asset.bin", "second signed data"));
        ClientAssetLayout.ValidateManifest(next, Preset);

        var result = await f.Installer.InstallAsync(next, f.Options());

        Assert.Equal(1, result.InstalledFiles);
        Assert.Equal("second signed data", File.ReadAllText(f.PathOf("data/asset.bin")));
        Assert.Equal("second signed data", File.ReadAllText(Path.Combine(f.Alias("data"), "asset.bin")));
        foreach (string name in Assets) Assert.True(ClientDirectoryJunction.IsExact(f.Alias(name), f.Target(name)));
        await ClientAssetLayout.PrepareAsync(next, f.Options(), Preset, f.Guard);
    }

    [WindowsFact]
    public async Task SignedAliasTargetedManifestIsRefusedAndGenericInstallerStillRejectsReparsePayloads()
    {
        using var f = new LayoutFixture(); await f.Install(); await f.Prepare();
        var malformed = f.Release(2, ("Env_DX8/data/rogue.bin", "must not escape through alias"));
        var before = f.Snapshot();

        Assert.Throws<InvalidDataException>(() => ClientAssetLayout.ValidateManifest(malformed, Preset));
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Installer.InstallAsync(malformed, f.Options()));

        AssertSameFiles(before, f.Snapshot());
        Assert.False(File.Exists(f.PathOf("data/rogue.bin")));
        Assert.True(ClientDirectoryJunction.IsExact(f.Alias("data"), f.Target("data")));
    }

    [WindowsFact]
    public async Task AllElevenAssetRootsWithoutAnyOrdinaryEngineContentCannotEnableThePreset()
    {
        using var f = new LayoutFixture(); await f.Install();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var files = f.Installed.Manifest.Files.Where(file => file.Path != Engine).ToArray();
        Assert.Equal(11, files.Length);
        var now = DateTimeOffset.UtcNow;
        var manifest = new UpdateManifest(1, "layout-fixture", "preview", 1, now.AddMinutes(-1), now.AddHours(1),
            new Uri("https://updates.example.test/"), files);
        var verified = SignedUpdateManifest.VerifyEnvelope(SignedUpdateManifest.CreateEnvelope(manifest, "root-only-key", key),
            "root-only-key", key.ExportSubjectPublicKeyInfoPem(), new("layout-fixture", "preview", 1, now));

        Assert.Throws<InvalidDataException>(() => ClientAssetLayout.ValidateManifest(verified, Preset));

        Assert.All(Assets, name => Assert.False(Directory.Exists(f.Alias(name))));
    }

    [WindowsTheory]
    [InlineData("unknown", "Env_DX8/FixtureClient.exe", "Env_DX8")]
    [InlineData(Preset, "FixtureClient.exe", "Env_DX8")]
    [InlineData(Preset, "Env_DX8/FixtureClient.exe", null)]
    [InlineData(Preset, "Env_DX8/FixtureClient.exe", "data")]
    [InlineData(Preset, "Env_DX8/FixtureClient.exe", "env_dx8")]
    public void LocalConfigurationMustExplicitlySelectTheKnownPresetAndExactEngineWorkingDirectory(string preset, string launch, string? cwd)
    {
        Assert.Throws<InvalidDataException>(() => ClientAssetLayout.ValidateConfiguration(preset, launch, cwd));
    }

    [WindowsFact]
    public void ExplicitPresetAndLegacyUnconfiguredModeRemainDistinct()
    {
        ClientAssetLayout.ValidateConfiguration(Preset, Engine, "Env_DX8");
        ClientAssetLayout.ValidateConfiguration(null, "FixtureClient.exe", null);
    }
}

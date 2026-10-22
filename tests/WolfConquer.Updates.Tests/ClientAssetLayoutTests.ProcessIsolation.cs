using WolfConquer.Updates.ClientLayout;
using WolfConquer.Updates.Install;

namespace WolfConquer.Updates.Tests;

public sealed partial class ClientAssetLayoutTests
{
    [WindowsFact]
    public async Task NeverLaunchedLayoutFixturesHaveIndependentSignedAndProtectedExecutableNames()
    {
        using var first = new LayoutFixture(); using var second = new LayoutFixture();
        Assert.NotEqual(first.Engine, second.Engine);
        foreach (var fixture in new[] { first, second })
        {
            Assert.StartsWith("Env_DX8/WolfLayoutFixture-", fixture.Engine, StringComparison.Ordinal);
            Assert.EndsWith(".exe", fixture.Engine, StringComparison.Ordinal);
            Assert.True(Guid.TryParseExact(Path.GetFileNameWithoutExtension(fixture.Engine)["WolfLayoutFixture-".Length..], "N", out _));
            Assert.NotEqual("FixtureClient.exe", Path.GetFileName(fixture.Engine), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(new[] { fixture.Engine }, fixture.Options().ProtectedExecutablePaths);
            await fixture.Install();
            Assert.Single(fixture.Installed.Manifest.Files, file => file.Path == fixture.Engine);
            Assert.Equal("Synthetic fixture bytes; NEVER LAUNCH.", File.ReadAllText(fixture.PathOf(fixture.Engine)));
            await fixture.Prepare();
            Assert.NotEmpty(fixture.Guard.Calls);
            Assert.All(fixture.Guard.Calls, call => Assert.Equal(new[] { fixture.PathOf(fixture.Engine) }, call));
            Assert.All(Assets, name => Assert.True(ClientDirectoryJunction.IsExact(fixture.Alias(name), fixture.Target(name))));
        }
    }

    [WindowsFact]
    public async Task RealGuardRefusesExactRunningImageAllowsAttributedNamesakeAndAllowsOwnedImageAfterExit()
    {
        using var fixture = new LayoutFixture();
        await using var command = await OwnedGuardCommand.StartAsync(fixture.Root, Path.GetFileName(fixture.Engine));
        var guard = new ConfiguredExecutableProcessGuard();

        var denied = await Assert.ThrowsAnyAsync<IOException>(() => guard.EnsureStoppedAsync([command.Executable], default));
        Assert.Equal("Close the configured client before installing updates.", denied.Message);
        Assert.Null(denied.InnerException); Assert.False(command.HasExited);
        // The same basename is not path authority. This is a real live process, not a guard mock.
        Assert.Equal(Path.GetFileName(command.Executable), Path.GetFileName(fixture.Engine));
        await guard.EnsureStoppedAsync([fixture.PathOf(fixture.Engine)], default);
        Assert.False(command.HasExited);

        await command.StopAsync();
        Assert.True(command.HasExited);
        await guard.EnsureStoppedAsync([command.Executable], default);
    }

    [WindowsFact]
    public async Task RealJournalRecoveryAndLayoutStayProtectedWhileOldSharedCommandNameIsAlive()
    {
        using var fixture = new LayoutFixture();
        await using var command = await OwnedGuardCommand.StartAsync(fixture.Root, "FixtureClient.exe");
        Assert.NotEqual(Path.GetFileName(command.Executable), Path.GetFileName(fixture.Engine), StringComparer.OrdinalIgnoreCase);
        await fixture.Install();
        string pending = await fixture.PreparePending();
        var before = fixture.Snapshot();

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Prepare());
        AssertSameFiles(before, fixture.Snapshot());
        Assert.All(Assets, name => Assert.False(Directory.Exists(fixture.Alias(name))));
        Assert.False(command.HasExited);
        Assert.Equal(1, await fixture.Installer.RecoverAsync(fixture.Options()));
        Assert.Contains("rolledback", File.ReadAllText(Path.Combine(pending, "journal.json")));
        await fixture.Prepare();

        Assert.False(command.HasExited);
        Assert.All(Assets, name => Assert.True(ClientDirectoryJunction.IsExact(fixture.Alias(name), fixture.Target(name))));
        Assert.All(fixture.Guard.Calls, call => Assert.Equal(new[] { fixture.PathOf(fixture.Engine) }, call));
        Assert.Equal("owned data", File.ReadAllText(fixture.PathOf("data/asset.bin")));
        Assert.Equal("Synthetic fixture bytes; NEVER LAUNCH.", File.ReadAllText(fixture.PathOf(fixture.Engine)));
    }
}

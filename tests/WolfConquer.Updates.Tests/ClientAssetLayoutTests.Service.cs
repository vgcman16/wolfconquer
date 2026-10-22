using System.Text.Json.Nodes;
using WolfConquer.Updates.ClientLayout;

namespace WolfConquer.Updates.Tests;

public sealed partial class ClientAssetLayoutTests
{
    [WindowsFact]
    public async Task SignedOrdinaryInstallationCreatesElevenExactAliasesAndIsIdempotentWithoutPayloadMutation()
    {
        using var f = new LayoutFixture(); await f.Install();
        Assert.Equal(Preset, ClientAssetLayout.LegacyDx8V1);
        Assert.Equal(Assets, ClientAssetLayout.AssetDirectoryNames);
        var before = f.Snapshot(); byte[] state = File.ReadAllBytes(f.State);

        await f.Prepare();

        AssertSameFiles(before, f.Snapshot());
        Assert.Equal(state, File.ReadAllBytes(f.State));
        Assert.True(f.Guard.Calls.Count >= 12, "A stopped-client check must precede acquisition and every link mutation.");
        foreach (var call in f.Guard.Calls) Assert.Equal(new[] { f.PathOf(Engine) }, call);
        foreach (string name in Assets)
        {
            Assert.True(ClientDirectoryJunction.IsExact(f.Alias(name), f.Target(name)));
            Assert.Equal("owned " + name, File.ReadAllText(Path.Combine(f.Alias(name), "asset.bin")));
        }
        var stamps = Assets.Select(name => Directory.GetCreationTimeUtc(f.Alias(name))).ToArray();
        int previousChecks = f.Guard.Calls.Count;
        await f.Prepare();
        Assert.Equal(stamps, Assets.Select(name => Directory.GetCreationTimeUtc(f.Alias(name))).ToArray());
        Assert.True(f.Guard.Calls.Count > previousChecks);
        ClientAssetLayout.ValidateExistingLayout(f.Client, Preset);
        AssertSameFiles(before, f.Snapshot());
    }

    [WindowsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateOrdinaryAliasConflictRefusesBeforeAnyEarlierLinkMutation(bool file)
    {
        using var f = new LayoutFixture(); await f.Install();
        string collision = f.Alias("video");
        if (file) File.WriteAllText(collision, "preserved conflict");
        else { Directory.CreateDirectory(collision); File.WriteAllText(Path.Combine(collision, "keep"), "preserved conflict"); }
        var before = f.Snapshot();
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Prepare());
        AssertSameFiles(before, f.Snapshot());
        Assert.False(Directory.Exists(f.Alias("ani")));
        Assert.False(ExistsReparse(collision));
    }

    [WindowsFact]
    public async Task RetargetedAliasRefusesWithoutDeletingOtherValidAliasesOrTargets()
    {
        using var f = new LayoutFixture(); await f.Install();
        f.Junction(f.Alias("ani"), f.Target("ani"));
        f.Junction(f.Alias("video"), f.Target("sound"));
        var before = f.Snapshot();
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Prepare());
        Assert.True(ClientDirectoryJunction.IsExact(f.Alias("ani"), f.Target("ani")));
        Assert.True(ClientDirectoryJunction.IsExact(f.Alias("video"), f.Target("sound")));
        Assert.False(Directory.Exists(f.Alias("c3")));
        AssertSameFiles(before, f.Snapshot());
    }

    [WindowsFact]
    public async Task ProcessDenialIsObservedBeforeAnyAliasCreation()
    {
        using var f = new LayoutFixture(); await f.Install();
        var before = f.Snapshot();
        var denied = new RecordingGuard(_ => throw new IOException("Synthetic stopped-client refusal."));
        await Assert.ThrowsAsync<IOException>(() => f.Prepare(denied));
        Assert.Single(denied.Calls);
        Assert.Equal(new[] { f.PathOf(Engine) }, denied.Calls[0]);
        Assert.All(Assets, name => Assert.False(Directory.Exists(f.Alias(name))));
        AssertSameFiles(before, f.Snapshot());
    }

    [WindowsFact]
    public async Task MidLoopConflictRollsBackOnlyNewOwnedLinksAndPreservesEarlierValidLinkAndConflict()
    {
        using var f = new LayoutFixture(); await f.Install();
        f.Junction(f.Alias("ani"), f.Target("ani"));
        var before = f.Snapshot();
        bool injected = false;
        var guard = new RecordingGuard(_ =>
        {
            if (!injected && ExistsReparse(f.Alias("c3")))
            {
                injected = true;
                Directory.CreateDirectory(f.Alias("video"));
                File.WriteAllText(Path.Combine(f.Alias("video"), "conflict.keep"), "injected owned conflict");
            }
        });

        await Assert.ThrowsAsync<InvalidDataException>(() => f.Prepare(guard));

        Assert.True(injected, "The guard must observe a completed new junction before injecting the later conflict.");
        Assert.True(guard.Calls.Count >= 3);
        Assert.True(ClientDirectoryJunction.IsExact(f.Alias("ani"), f.Target("ani")));
        Assert.False(Directory.Exists(f.Alias("c3")));
        Assert.Equal("injected owned conflict", File.ReadAllText(Path.Combine(f.Alias("video"), "conflict.keep")));
        var after = f.Snapshot();
        Assert.True(after.Remove(Path.Combine("Env_DX8", "video", "conflict.keep")));
        AssertSameFiles(before, after);
        foreach (string name in Assets) Assert.Equal("owned " + name, File.ReadAllText(Path.Combine(f.Target(name), "asset.bin")));
    }

    [WindowsFact]
    public async Task CancellationAfterRealC3CreationRollsBackOnlyNewLinksUsingNoncancelableCleanup()
    {
        using var f = new LayoutFixture(); await f.Install();
        f.Junction(f.Alias("ani"), f.Target("ani"));
        var before = f.Snapshot(); byte[] state = File.ReadAllBytes(f.State);
        using var cancellation = new CancellationTokenSource();
        int cancelCheck = 0;
        var guard = new RecordingGuard(check =>
        {
            if (cancelCheck == 0 && ExistsReparse(f.Alias("c3")))
            {
                Assert.True(ClientDirectoryJunction.IsExact(f.Alias("c3"), f.Target("c3")));
                cancelCheck = check;
                cancellation.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ClientAssetLayout.PrepareAsync(f.Installed,
            f.Options(), Preset, guard, cancellation.Token));

        Assert.True(cancelCheck >= 3, "Cancellation must follow the observed real new c3 alias, not just initial preflight.");
        Assert.True(guard.Calls.Count > cancelCheck, "Rollback must perform another real stopped-client check.");
        Assert.All(guard.Tokens.Skip(cancelCheck), token => Assert.False(token.CanBeCanceled));
        Assert.True(ClientDirectoryJunction.IsExact(f.Alias("ani"), f.Target("ani")));
        Assert.All(Assets.Where(name => name != "ani"), name => Assert.False(Directory.Exists(f.Alias(name))));
        Assert.Equal(state, File.ReadAllBytes(f.State));
        AssertSameFiles(before, f.Snapshot());
    }

    [WindowsTheory]
    [InlineData("product")]
    [InlineData("sequence")]
    [InlineData("file-hash")]
    [InlineData("file-path")]
    public async Task CommittedStateIdentityAndFileMapMustMatchVerifiedReleaseBeforeMutation(string change)
    {
        using var f = new LayoutFixture(); await f.Install();
        var state = JsonNode.Parse(File.ReadAllBytes(f.State))!.AsObject();
        if (change == "product") state["ProductId"] = "other-product";
        else if (change == "sequence") state["Sequence"] = 9;
        else
        {
            var row = state["Files"]!.AsArray().Single(node => node!["Path"]!.GetValue<string>() == "ani/asset.bin")!;
            if (change == "file-hash") row["Sha256"] = new string('0', 64);
            else row["Path"] = "ANI/asset.bin";
        }
        File.WriteAllText(f.State, state.ToJsonString());
        var before = f.Snapshot();
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Prepare());
        AssertSameFiles(before, f.Snapshot());
        Assert.All(Assets, name => Assert.False(Directory.Exists(f.Alias(name))));
    }

    [WindowsFact]
    public async Task RealPreparedRecoveryJournalBlocksLayoutUntilInstallerRecoveryCompletes()
    {
        using var f = new LayoutFixture(); await f.Install();
        string pending = await f.PreparePending();
        var before = f.Snapshot();
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Prepare());
        AssertSameFiles(before, f.Snapshot());
        Assert.All(Assets, name => Assert.False(Directory.Exists(f.Alias(name))));
        Assert.Equal(1, await f.Installer.RecoverAsync(f.Options()));
        Assert.Contains("rolledback", File.ReadAllText(Path.Combine(pending, "journal.json")));
        await f.Prepare();
        Assert.All(Assets, name => Assert.True(ClientDirectoryJunction.IsExact(f.Alias(name), f.Target(name))));
    }
}

using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    internal static Task RunColdPirateRecoveryScenarioAsync(string scenario, uint job = 7051) => RunPackageJoinAsync(async (fixture, join) =>
    {
        var claimAll = scenario != "Mode5";
        var initial = await SeedPirateRecoveryAsync(fixture, join, scenario);
        var intent = PreparePirateRecoveryIntent(fixture, join, initial, claimAll, allocated: claimAll, scenario, job);
        var files = fixture.PackageFiles();
        var first = fixture.Sent.Count;
        var client = await join.RestartNativeDebtRecoveryAsync();
        if (scenario is "MissingTables" or "BadTables" or "MetadataMiss" or "DuplicateItem" or "DuplicateRune" or "Noncanonical" or "MissingAnima")
        {
            Assert.False(join.SessionStore.TryGet(join.Connection!, out _));
            Assert.Null(client.CombatGear);
            Assert.Equal(0U, client.Player.UID);
            Assert.Equal(0U, client.Player.Class);
            Assert.Equal(0U, client.Player.ClassExperience);
            Assert.False(client.FullLoading);
            Assert.False(client.Player.IsCheckedPass);
            Assert.Equal(first, fixture.Sent.Count);
            AssertPackageJoinFiles(fixture, files);
            AssertNinjaRecoveryIntentRetained(join, intent);
            return;
        }
        var gear = Assert.IsType<LegacyNativeMyCombatGear>(client.CombatGear);
        Assert.True(gear.IsCombatOwnedBy(client));
        AssertPirateRecoveryNative(gear, PirateRecoveryGear(), job);
        Assert.Equal(job, client.Player.Class);
        Assert.Equal(31U, client.Player.ClassExperience);
        Assert.False(client.FullLoading);
        Assert.Equal(0U, client.Player.Online);
        Assert.Null(client.MyNinja);
        Assert.Null(client.Beasts);
        Assert.Null(client.HairfaceStorage);
        Assert.Null(client.Socket);
        Assert.Equal(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), fixture.Sent[first]);
        AssertPackageJoinFiles(fixture, files);
        Assert.False(client.Player.IsCheckedPass);
        first = fixture.Sent.Count;
        await DebtRegisteredAsync(fixture, ActualDebtContext(fixture, join), PackageJoinRequest(claimAll), direct: true);
        Assert.Equal(first, fixture.Sent.Count);
        AssertPackageJoinFiles(fixture, files);
        AssertPirateRecoveryNative(gear, PirateRecoveryGear(), job);
        await AssertNativeDebtPinAsync(fixture, join, 999, 6, 0);
        Assert.False(client.Player.IsCheckedPass);
        AssertPackageJoinFiles(fixture, files);
        await AssertNativeDebtPinAsync(fixture, join, 111, 5, 257);
        Assert.True(client.Player.IsCheckedPass);
        AssertPackageJoinFiles(fixture, files);
        if (scenario is "OwnerSwap" or "MetadataSwap" or "StaleOwner")
        {
            if (scenario == "OwnerSwap") client.CombatGear = new LegacyNativeMyCombatGear(client);
            else if (scenario == "MetadataSwap") LegacyNativeCombatGearTable.Info[701][1].Skills[9]++;
            else
            {
                Assert.True(join.SessionStore.TryRemove(join.Connection!, out var retired));
                LegacySecondaryPasswordRuntime.Invalidate(join.Connection!, retired!);
            }
            first = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(claimAll));
            Assert.Equal(first, fixture.Sent.Count);
            AssertPackageJoinFiles(fixture, files);
            AssertNinjaRecoveryIntentRetained(join, intent);
            AssertPirateRecoveryNative(gear, PirateRecoveryGear(), job);
            Assert.Equal(31U, client.Player.ClassExperience);
            return;
        }
        var sends = 0;
        join.SendObserver = _ => { AssertPirateRecoveryCommitted(fixture, join, initial, claimAll, job); sends++; };
        first = fixture.Sent.Count;
        var started = DateTime.Now;
        await DebtRegisteredAsync(fixture, ActualDebtContext(fixture, join), PackageJoinRequest(claimAll), direct: false);
        join.SendObserver = null;
        Assert.True(sends > 0);
        AssertPirateRecoveryCommitted(fixture, join, initial, claimAll, job);
        AssertColdPirateRecoveryFrames(fixture, join, initial, claimAll, first, started, DateTime.Now);
        Assert.Same(gear, client.CombatGear);
        Assert.False(client.FullLoading);
        var committedIntent = AssertPackageJoinAllocation(fixture, join);
        Assert.Equal(intent.Request.OperationId, committedIntent.Request.OperationId);
        Assert.Equal(intent.CopyPayload(), committedIntent.CopyPayload());
        var committed = fixture.PackageFiles();
        var committedLive = System.Text.Json.JsonSerializer.Serialize(join.Current);
        first = fixture.Sent.Count;
        await join.FrameAsync(PackageJoinRequest(claimAll));
        AssertPackageJoinFiles(fixture, committed);
        Assert.Equal(committedLive, System.Text.Json.JsonSerializer.Serialize(join.Current));
        AssertPackageJoinReplay(fixture, claimAll, first);
        AssertPirateRecoveryNative(gear, PirateRecoveryAfter(PirateRecoveryGear()), job);
        Assert.Equal(1031U, client.Player.ClassExperience);
        AssertPackageJoinAllocation(fixture, join);
    }, job: job);
}

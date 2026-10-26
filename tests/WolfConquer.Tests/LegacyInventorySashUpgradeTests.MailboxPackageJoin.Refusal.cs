using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(10U)]
    [InlineData(11U)]
    [InlineData(12U)]
    [InlineData(uint.MaxValue)]
    public Task ActualD13ExplicitPackageJoinUnsupportedFlagRefusesWholeClaimAllBeforeAnyIntent(uint flag) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            await join.FrameAsync(PackageJoinRequest(claimAll: true));
            AssertPackageJoinUnchanged(fixture, join, before);
            AssertNoPackageJoinIntents(join);
        }, flag: flag);

    [Theory]
    [InlineData("missing", false)]
    [InlineData("durability", false)]
    [InlineData("stack", true)]
    [InlineData("raw-id", false)]
    [InlineData("raw-name", false)]
    [InlineData("timed", false)]
    public Task ActualD13ExplicitPackageJoinInvalidMetadataOrExpandedCapacityRefusesWholeAction(
        string fault, bool bagFull) => RunPackageJoinAsync(async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            await join.FrameAsync(PackageJoinRequest(claimAll: true));
            AssertPackageJoinUnchanged(fixture, join, before, bagFull ? 1 : 0);
            if (bagFull) Assert.Equal(LegacyMsgMessagePacket.SystemTalk("STR_BAG_FULL@@").ToFrameBytes(), fixture.Sent[^1]);
            AssertNoPackageJoinIntents(join);
        }, catalogFault: fault);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualD13ExplicitPackageJoinFullBagNeverClaimsAnyMixedRewardOrAllocatesUids(bool claimAll) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            await join.FrameAsync(PackageJoinRequest(claimAll));
            AssertPackageJoinUnchanged(fixture, join, before, extraFrames: 1);
            Assert.Equal(LegacyMsgMessagePacket.SystemTalk("STR_BAG_FULL@@").ToFrameBytes(), fixture.Sent[^1]);
            AssertNoPackageJoinIntents(join);
        }, inventoryCount: 40);

    [Theory]
    [InlineData(5051L)]
    [InlineData(7051L)]
    public Task ActualD13ExplicitPackageProducerDoesNotInventMissingNativeNinjaOrPirateOwners(long job) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            Assert.Null(join.Connection!.NativeGameClient);
            await join.FrameAsync(PackageJoinRequest(claimAll: true));
            AssertPackageJoinUnchanged(fixture, join, before);
            AssertNoPackageJoinIntents(join);
        }, job: job);

    [Fact]
    public Task ActualD13UnboundRealProducerRetainsTheDefaultPackageFence() =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            await join.FrameAsync(PackageJoinRequest(claimAll: true));
            AssertPackageJoinUnchanged(fixture, join, before);
            AssertNoPackageJoinIntents(join);
            Assert.Contains("mailboxClaimPackageOwnersUnresolved", fixture.Log.ToString());
        }, bindProducer: false);

    [Fact]
    public Task ActualD13RetiredPackageSessionCannotSpendItsPreviousAdmission() =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            Assert.True(join.SessionStore.TryRemove(join.Connection!, out var retired));
            Assert.Same(before.Owner, retired);
            await join.FrameAsync(PackageJoinRequest(claimAll: true));
            Assert.False(join.SessionStore.TryGet(join.Connection!, out _));
            AssertPackageJoinFiles(fixture, before.Files);
            Assert.Equal(before.FirstFrame, fixture.Sent.Count);
            AssertNoPackageJoinIntents(join);
        });

    private static void AssertNoPackageJoinIntents(PackageJoin join)
    {
        Assert.True(join.Registry.TryReadProducerIntents(join.Domain, out var intents, out var reason), reason);
        Assert.Empty(intents);
    }
}

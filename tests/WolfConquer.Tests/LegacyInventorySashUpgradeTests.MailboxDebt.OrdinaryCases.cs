using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RegisteredD13OrdinaryClaimMoneyCannotInvalidateGenuineRetainedPackageBeforeimages(bool allocated) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var initial = CapturePackageJoin(fixture, join);
            var pending = PrepareDebtIntent(fixture, join, initial, allocated);
            var before = CapturePackageJoin(fixture, join);
            Assert.True(before.Owner.Character.Money > 0);
            Assert.Equal(AtomicMailMoney, fixture.AtomicMailSeed.Money);
            Assert.Equal(AtomicMailCp, fixture.AtomicMailSeed.EMoney);
            var request = new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimMoney, AtomicMailId, []).ToFrameBytes();
            var context = ActualDebtContext(fixture, join);

            await DebtRegisteredAsync(fixture, context, request, direct: true);
            AssertDebtRetained(fixture, join, before, pending);
            await DebtRegisteredAsync(fixture, context, request, direct: false);
            AssertDebtRetained(fixture, join, before, pending);
            await join.FrameAsync(request);
            AssertDebtRetained(fixture, join, before, pending);
        });

    [Theory]
    [InlineData("item")]
    [InlineData("sash")]
    [InlineData("location")]
    [InlineData("walk")]
    public Task RegisteredD13PendingPackageFencesOrdinaryInventoryWalletAndWorldActionsThroughBothRegistryEntrypoints(string action) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var initial = CapturePackageJoin(fixture, join);
            var pending = PrepareDebtIntent(fixture, join, initial);
            var before = CapturePackageJoin(fixture, join);
            var request = action switch
            {
                "item" => new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimItem, AtomicMailId, []).ToFrameBytes(),
                "sash" => Fixture.Request(),
                "location" => new MsgActionPacket { ObjId = Fixture.Uid, Type = MsgActionType.SetLocation }.ToFrameBytes(),
                "walk" => new MsgMovementPacket { Uid = Fixture.Uid, Direction = 0, Running = MsgMovementMode.Walk,
                    MapId = 1002, Timestamp = 17 }.ToFrameBytes(),
                _ => throw new ArgumentOutOfRangeException(nameof(action)),
            };
            var context = ActualDebtContext(fixture, join);
            await DebtRegisteredAsync(fixture, context, request, direct: true);
            AssertDebtRetained(fixture, join, before, pending);
            await DebtRegisteredAsync(fixture, context, request, direct: false);
            AssertDebtRetained(fixture, join, before, pending);
            await join.FrameAsync(request);
            AssertDebtRetained(fixture, join, before, pending);
        });
}

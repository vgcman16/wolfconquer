using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public Task RegisteredD13LaterMailMergesIntoEarlierNewVirtualStackWithOneRealAllocationAndCorrectRequestOrder() =>
        RunStackPackageAsync(fixture =>
        {
            fixture.SetStackInventory(StackAllCompatible().Where(row => row.ItemId != 4_050_001));
            fixture.AddStackSecondPackageMail();
        }, async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var request = new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimAll, 0, [StackSecondMailId, AtomicMailId]).ToFrameBytes();
            var started = DateTime.Now;
            await join.FrameAsync(request);
            var fresh = ExpectedFreshStack(fixture, join, 1, 0, 4_050_001, 400);
            var once = StackCreditAll(before.Owner.PlayerItems.InventoryItems);
            var twice = StackCreditAll(before.Owner.PlayerItems.InventoryItems, claims: 2);
            var first = StackMultiOutputs(once, fresh.WithStackSize(200), LegacyGameItemPacket.AddItemMode);
            var second = StackMultiOutputs(twice, fresh, LegacyGameItemPacket.UpdateItemMode);
            var attachment = StackAttachmentBefore(before);
            var expected = StackExpectedItems(before, [.. twice, fresh, attachment], [attachment.Uid]);
            var selected = new[] { fixture.StackSecondMailSeed, fixture.AtomicMailSeed };
            AssertStackCommitted(fixture, join, before, expected, selected, true, 2031, newCount: 1);
            AssertStackFrames(fixture, before,
                [new(selected[0], first, 1031), new(selected[1], second, 2031, attachment)],
                true, started, DateTime.Now, before.FirstFrame, request);
            var committed = fixture.PackageFiles();
            var live = StackLiveBusiness(join);
            var payload = ReadStackAllocation(fixture, join, 1).CopyPayload();
            var replay = fixture.Sent.Count;
            await join.FrameAsync(request);
            AssertPackageJoinFiles(fixture, committed);
            Assert.Equal(live, StackLiveBusiness(join));
            AssertStackMultiReplay(fixture, replay, request, [StackSecondMailId, AtomicMailId]);
            var previous = join.Current;
            var savedCandidate = LoadDebtRecoveryCandidate(fixture, previous);
            await join.RestartAsync();
            AssertStackColdBusinessState(join, previous, savedCandidate);
            AssertPackageJoinFiles(fixture, committed);
            live = StackLiveBusiness(join);
            replay = fixture.Sent.Count;
            await join.FrameAsync(request);
            AssertPackageJoinFiles(fixture, committed);
            Assert.Equal(live, StackLiveBusiness(join));
            Assert.Equal(payload, ReadStackAllocation(fixture, join, 1).CopyPayload());
            AssertStackMultiReplay(fixture, replay, request, [StackSecondMailId, AtomicMailId]);
        });

    [Fact]
    public Task RegisteredD13NonstackableMetadataCreatesSourceZeroQuantityItemWithARealUidAndValidColor() =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(StackAllCompatible().Where(row => row.ItemId != 3_324_226)),
            async (fixture, join) =>
            {
                var before = CapturePackageJoin(fixture, join);
                var started = DateTime.Now;
                await join.FrameAsync(PackageJoinRequest(claimAll: false));
                var fresh = ExpectedFreshStack(fixture, join, 1, 0, 3_324_226, quantity: 0);
                var existing = StackCreditAll(before.Owner.PlayerItems.InventoryItems);
                var expected = StackExpectedItems(before, [.. existing, fresh]);
                var output = RankOnePackageRows.Select((grant, index) => new StackItemOutput((byte)(index < 4 ? 0 : 2),
                    grant.Id == 3_324_226 ? fresh : Assert.Single(existing, row => row.ItemId == grant.Id),
                    grant.Id == 3_324_226 ? LegacyGameItemPacket.AddItemMode : LegacyGameItemPacket.UpdateItemMode)).ToArray();
                AssertStackCommitted(fixture, join, before, expected, [fixture.AtomicMailSeed], false, 1031, 1);
                AssertStackFrames(fixture, before, [new(fixture.AtomicMailSeed, output, 1031)], false, started, DateTime.Now, before.FirstFrame);
                await AssertStackReplayRetainsAsync(fixture, join, claimAll: false);
            }, StackPackageCatalog(3_324_226, limit: 0));

    private static StackItemOutput[] StackMultiOutputs(PlayerItemSnapshotItem[] existing,
        PlayerItemSnapshotItem fresh, ushort freshMode) => RankOnePackageRows.Select((grant, index) =>
        new StackItemOutput((byte)(index < 4 ? 0 : 2), grant.Id == fresh.ItemId ? fresh :
            Assert.Single(existing, row => row.ItemId == grant.Id),
            grant.Id == fresh.ItemId ? freshMode : LegacyGameItemPacket.UpdateItemMode)).ToArray();

    private static void AssertStackMultiReplay(Fixture fixture, int first, byte[] request, uint[] ids)
    {
        var frames = fixture.Sent.Skip(first).ToArray();
        Assert.Equal(ids.Length + 1, frames.Length);
        for (var index = 0; index < ids.Length; index++)
        {
            Assert.True(LegacyMailListPacket.TryParse(ReadFrame(frames[index]), ConquerProtocolProfile.VisibleDx8D13, out var refresh));
            Assert.Equal(LegacyMailListType.ClaimAllUpdate, refresh.Type);
            var mail = Assert.Single(refresh.Mails);
            Assert.Equal(ids[index], mail.Id);
            Assert.Equal(0UL, mail.Flag);
            Assert.Equal(0UL, mail.Money);
            Assert.Equal(0UL, mail.EMoney);
            Assert.Equal(0UL, mail.ItemId);
        }
        Assert.Equal(request, frames[^1]);
    }
}

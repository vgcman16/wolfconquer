using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public Task RegisteredD13EarlierMailAttachmentBecomesARealLaterPackageMergeTargetInRequestOrder() =>
        RunStackPackageAsync(fixture =>
        {
            fixture.SetStackInventory(StackAllCompatible().Where(row => row.ItemId != 4_050_001));
            fixture.ConfigureStackAttachmentFirst();
        }, async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var moved = StackAttachmentBefore(before);
            Assert.Equal(4_050_001U, moved.ItemId);
            Assert.Equal(100, moved.StackSize);
            var request = new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimAll, 0, [StackSecondMailId, AtomicMailId]).ToFrameBytes();
            var started = DateTime.Now;
            await join.FrameAsync(request);
            var credited = StackCreditAll(before.Owner.PlayerItems.InventoryItems);
            var mergedAttachment = moved.WithStackSize(300);
            var expected = StackExpectedItems(before, [.. credited, mergedAttachment], [moved.Uid]);
            var output = StackMultiOutputs(credited, mergedAttachment, LegacyGameItemPacket.UpdateItemMode);
            var selected = new[] { fixture.StackSecondMailSeed, fixture.AtomicMailSeed };
            AssertStackCommitted(fixture, join, before, expected, selected, true, 1031, newCount: 0);
            AssertStackFrames(fixture, before,
                [new(selected[0], [], null, moved), new(selected[1], output, 1031)],
                true, started, DateTime.Now, before.FirstFrame, request);
            var files = fixture.PackageFiles();
            var live = StackLiveBusiness(join);
            var replay = fixture.Sent.Count;
            await join.FrameAsync(request);
            AssertPackageJoinFiles(fixture, files);
            Assert.Equal(live, StackLiveBusiness(join));
            AssertStackMultiReplay(fixture, replay, request, [StackSecondMailId, AtomicMailId]);
        });

    private sealed partial class Fixture
    {
        internal void ConfigureStackAttachmentFirst()
        {
            var original = AtomicMailSeed;
            AtomicMailSeed = new LegacyMailboxEntry
            {
                Id = original.Id, Title = original.Title, SenderName = original.SenderName, Content = original.Content,
                Money = original.Money, EMoney = original.EMoney, EmoneyRecordType = original.EmoneyRecordType,
                Flag = original.Flag, Timer = original.Timer, ItemId = 0,
            };
            var mailbox = new LegacyMailboxStore(Root);
            Assert.Equal(2, mailbox.Load(Uid).Count);
            mailbox.Add(Uid, AtomicMailSeed);
            StackSecondMailSeed = mailbox.Add(Uid, new LegacyMailboxEntry
            {
                Id = StackSecondMailId, ItemId = AtomicMailItemUid, Title = "Real attachment first", SenderName = "System sentinel",
                Content = "Moves into owned inventory before the later package.", Timer = original.Timer,
            });
            mailbox.Save(Uid);
            var attachment = StackOwnedRow(AtomicMailItemUid, 4_050_001, 100, color: 6).WithWarehouseId(1000);
            SaveTransferSetup(Current.PlayerItems with
            {
                WarehouseItems = Current.PlayerItems.WarehouseItems.Where(row => row.Uid != AtomicMailItemUid)
                    .Append(attachment).OrderBy(row => row.WarehouseId).ThenBy(row => row.Uid).ToArray(),
            });
        }
    }
}

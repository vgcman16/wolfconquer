using System.Buffers.Binary;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static void AssertPackageJoinFrames(Fixture fixture, PackageJoin join, PackageJoinBefore before,
        bool claimAll, DateTime started, DateTime ended, int firstFrame, long job = 10, bool sendClassExperience = true)
    {
        var intent = AssertPackageJoinAllocation(fixture, join);
        var expectedItems = ExpectedPackageJoinItems(before, intent, claimAll);
        var prefix = new List<byte[]>();
        if (claimAll)
        {
            prefix.Add(LegacyMsgMessagePacket.SystemTalk($"STR_PICK_MONEY@@{AtomicMailMoney}@@").ToFrameBytes());
            prefix.Add(new LegacyMsgUpdatePacket(Fixture.Uid,
                [new(LegacyMsgUpdateDataType.Money, checked((ulong)before.Owner.Character.Money + AtomicMailMoney))]).ToFrameBytes());
            prefix.Add(LegacyMsgMessagePacket.SystemTalk($"You received {AtomicMailCp} ConquerPoints.").ToFrameBytes());
            prefix.Add(new LegacyMsgUpdatePacket(Fixture.Uid,
                [new(LegacyMsgUpdateDataType.ConquerPoints, checked((ulong)before.Owner.Character.ConquerPoints + AtomicMailCp))]).ToFrameBytes());
        }
        // Native Process(uint) grants the first four stacks, then class experience,
        // then the job reward (none for Other), and finally the two rank1 tail stacks.
        for (var index = 0; index < 4; index++) prefix.Add(PackageJoinGeneratedFrame(expectedItems, intent, index));
        if (sendClassExperience)
            prefix.Add(new LegacyMsgUpdatePacket(Fixture.Uid,
                [new(LegacyMsgUpdateDataType.ClassExperience, 1031)]).ToFrameBytes());
        if (job == 5051) prefix.Add(PackageJoinNinjaPointsFrame());
        if (job == 7051) prefix.Add(Convert.FromHexString(
            "1E00A009080510A0FA4E18002210080710001800200028003000380040455451536572766572"));
        for (var index = 4; index < 6; index++) prefix.Add(PackageJoinGeneratedFrame(expectedItems, intent, index));
        // Native ClaimAll processes the package before moving its attachment.
        if (claimAll) prefix.Add(LegacyGameItemPacket.FromSnapshotItem(
            Assert.Single(expectedItems.InventoryItems, row => row.Uid == AtomicMailItemUid)).ToFrameBytes());
        var actual = fixture.Sent.Skip(firstFrame).ToArray();
        Assert.Equal(prefix.Count + (claimAll ? 2 : 1), actual.Length);
        for (var index = 0; index < prefix.Count; index++) Assert.Equal(prefix[index], actual[index]);
        if (claimAll)
        {
            Assert.True(LegacyMailListPacket.TryParse(ReadFrame(actual[^2]),
                ConquerProtocolProfile.VisibleDx8D13, out var refresh));
            Assert.Equal(LegacyMailListType.ClaimAllUpdate, refresh.Type);
            Assert.Equal(1, refresh.Count);
            var row = Assert.Single(refresh.Mails);
            var cleared = fixture.AtomicMailSeed.WithClearedFlag().WithClaimedMoney().WithClearedItemId();
            var earliest = LegacyMailListPacket.FromMailboxEntry(cleared, started);
            var latest = LegacyMailListPacket.FromMailboxEntry(cleared, ended);
            Assert.Equal(earliest with { ExpirationDate = row.ExpirationDate }, row);
            Assert.InRange(row.ExpirationDate, latest.ExpirationDate, earliest.ExpirationDate);
        }
        Assert.Equal(PackageJoinRequest(claimAll), actual[^1]);
    }

    private static byte[] PackageJoinGeneratedFrame(WolfConquer.Core.Data.PlayerItemSnapshot items,
        WolfConquer.Core.Data.LegacyItemUidProducerIntentSnapshot intent, int index) =>
        LegacyGameItemPacket.FromSnapshotItem(Assert.Single(items.InventoryItems,
            row => row.Uid == checked(intent.Allocation!.First + (uint)index))).ToFrameBytes();

    private static byte[] PackageJoinNinjaPointsFrame()
    {
        var payload = Convert.FromHexString(
            "080210001800204B28003000380040004800500058006000680070007800800100880100900100980100A00100A80100");
        var frame = new byte[payload.Length + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)frame.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2395);
        payload.CopyTo(frame, 4);
        return frame;
    }
}

using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static void AssertColdNinjaRecoveryFrames(Fixture fixture, PackageJoin join, PackageJoinBefore before,
        bool claimAll, int first, DateTime started, DateTime ended)
    {
        var intent = AssertPackageJoinAllocation(fixture, join);
        var items = ExpectedPackageJoinItems(before, intent, claimAll);
        var expected = new List<byte[]>();
        if (claimAll)
        {
            expected.Add(LegacyMsgMessagePacket.SystemTalk($"STR_PICK_MONEY@@{AtomicMailMoney}@@").ToFrameBytes());
            expected.Add(new LegacyMsgUpdatePacket(Fixture.Uid,
                [new(LegacyMsgUpdateDataType.Money, checked((ulong)before.Owner.Character.Money + AtomicMailMoney))]).ToFrameBytes());
            expected.Add(LegacyMsgMessagePacket.SystemTalk($"You received {AtomicMailCp} ConquerPoints.").ToFrameBytes());
            expected.Add(new LegacyMsgUpdatePacket(Fixture.Uid,
                [new(LegacyMsgUpdateDataType.ConquerPoints, checked((ulong)before.Owner.Character.ConquerPoints + AtomicMailCp))]).ToFrameBytes());
        }
        for (var index = 0; index < 4; index++) expected.Add(PackageJoinGeneratedFrame(items, intent, index));
        // Genuine cold ClassExperience setter branch emits no class update;
        // Ninja.Points still emits its exact required-zero Type2 source frame.
        expected.Add(PackageJoinNinjaPointsFrame());
        for (var index = 4; index < 6; index++) expected.Add(PackageJoinGeneratedFrame(items, intent, index));
        if (claimAll) expected.Add(LegacyGameItemPacket.FromSnapshotItem(
            Assert.Single(items.InventoryItems, item => item.Uid == AtomicMailItemUid)).ToFrameBytes());
        var actual = fixture.Sent.Skip(first).ToArray();
        Assert.Equal(expected.Count + (claimAll ? 2 : 1), actual.Length);
        for (var index = 0; index < expected.Count; index++) Assert.Equal(expected[index], actual[index]);
        if (claimAll)
        {
            Assert.True(LegacyMailListPacket.TryParse(ReadFrame(actual[^2]), ConquerProtocolProfile.VisibleDx8D13, out var refresh));
            Assert.Equal(LegacyMailListType.ClaimAllUpdate, refresh.Type);
            var row = Assert.Single(refresh.Mails);
            var cleared = fixture.AtomicMailSeed.WithClearedFlag().WithClaimedMoney().WithClearedItemId();
            var earliest = LegacyMailListPacket.FromMailboxEntry(cleared, started);
            var latest = LegacyMailListPacket.FromMailboxEntry(cleared, ended);
            Assert.Equal(earliest with { ExpirationDate = row.ExpirationDate }, row);
            Assert.InRange(row.ExpirationDate, latest.ExpirationDate, earliest.ExpirationDate);
        }
        Assert.Equal(PackageJoinRequest(claimAll), actual[^1]);
    }
}

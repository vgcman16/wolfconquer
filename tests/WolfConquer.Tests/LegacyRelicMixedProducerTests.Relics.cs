using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicMixedProducerTests
{
    private static async Task<uint> ComposeRelics(MixedFixture f)
    {
        var before = Capture(f); var issuedUid = checked(before.HighWater + 1);
        var first = before.Session.PlayerItems.InventoryItems.Single(row => row.Uid == LegacyRelicMixedProducerData.FirstRelicUid);
        var second = before.Session.PlayerItems.InventoryItems.Single(row => row.Uid == LegacyRelicMixedProducerData.SecondRelicUid);
        var request = new LegacyRelicFusePacket(0, first.Uid, second.Uid, 0);
        PlayerItemSnapshot Expected()
        {
            var result = Assert.Single(f.Current.PlayerItems.RelicFuseItems, row => row.Position == 215);
            Assert.Equal(issuedUid, result.Uid); Assert.Equal(first.ItemId, result.ItemId);
            Assert.Equal(Math.Max(first.Bound, second.Bound), result.Bound);
            Assert.Equal((ushort)1, result.StackSize); Assert.InRange(result.Durability, (ushort)3_000, (ushort)10_000);
            Assert.Equal(0, result.Durability % 100); Assert.Equal(result.Durability, result.MaximumDurability);
            AssertGeneratedWords(result);
            return before.Session.PlayerItems with
            {
                InventoryItems = before.Session.PlayerItems.InventoryItems.Where(row => row.Uid != first.Uid && row.Uid != second.Uid).ToArray(),
                RelicFuseItems = [first.WithPosition(213).WithStackSize(1), second.WithPosition(214).WithStackSize(1), result],
            };
        }
        f.Sent.Clear(); f.BeforeSend = _ => AssertSavedMutation(f, before, Expected(), issued: true, version: 7);
        try { await f.SendAsync(request.ToFrameBytes()); } finally { f.BeforeSend = null; }
        var expected = Expected(); AssertSavedMutation(f, before, expected, issued: true, version: 7);
        Assert.Equal(2, f.Sent.Count);
        var generated = expected.RelicFuseItems.Single(row => row.Position == 215);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(generated).ToFrameBytes(), f.Sent[0]);
        Assert.Equal((request with { RelicID3 = issuedUid }).ToFrameBytes(), f.Sent[1]);
        f.Sent.Clear(); return issuedUid;
    }

    private static async Task<uint> ChooseRelic(MixedFixture f, int position)
    {
        var before = Capture(f);
        var selected = before.Session.PlayerItems.RelicFuseItems.Single(row => row.Position == position);
        var returned = selected.WithPosition(0);
        var expected = before.Session.PlayerItems with
        {
            InventoryItems = before.Session.PlayerItems.InventoryItems.Append(returned).ToArray(), RelicFuseItems = [],
        };
        var request = new LegacyRelicFusePacket(1, selected.Uid, uint.MaxValue, 0xabcdef00);
        f.Sent.Clear(); f.BeforeSend = _ => AssertSavedMutation(f, before, expected, issued: false, version: before.Version);
        try { await f.SendAsync(request.ToFrameBytes()); } finally { f.BeforeSend = null; }
        AssertSavedMutation(f, before, expected, issued: false, version: before.Version); Assert.Equal(2, f.Sent.Count);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(returned, mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes(), f.Sent[0]);
        Assert.Equal(request.ToFrameBytes(), f.Sent[1]);
        f.Sent.Clear(); return selected.Uid;
    }

    private static void AssertGeneratedWords(PlayerItemSnapshotItem result)
    {
        uint[] words = [result.Attribute1, result.Attribute2, result.Attribute3, result.Attribute4, result.Attribute5];
        foreach (var raw in words)
        {
            var type = raw % 100; var value = raw / 1000; var marker = raw % 1000 - type;
            Assert.InRange(type, 1U, 14U);
            Assert.DoesNotContain(type, new uint[] { 1, 2, 6, 7 }); // Independently aligned4100001 exclusions.
            if (type == 14) Assert.Equal(100_114U, raw);
            else
            {
                Assert.InRange(value, 30U, 400U);
                Assert.Equal(value == 400 ? 100U : 0U, marker);
            }
        }
    }
}

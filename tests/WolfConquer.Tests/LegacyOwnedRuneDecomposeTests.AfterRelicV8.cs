using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneDecomposeTests
{
    [Fact]
    public Task ActualRuneDecomposeAfterRelicBatchKeepsSharedVersionAndNeverReissues() => Run(async f =>
    {
        var initialHigh = f.HighWater;
        var beforeFiles = f.Data.Files();
        var beforeItems = File.ReadAllBytes(f.ItemsPath);
        var beforeUids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(beforeItems).ToHashSet();
        var beforeState = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!.AsObject();
        var request = RelicRequest(DecomposeFixture.Kept + 1);
        f.Sent.Clear();
        f.BeforeSend = _ => f.AssertConverged();
        try { await f.SendAsync(request); } finally { f.BeforeSend = null; }
        Assert.Equal(11, f.Sent.Count); Assert.Equal(request, f.Sent[^1]);
        Assert.DoesNotContain(f.Current.PlayerItems.InventoryItems, row => row.Uid == DecomposeFixture.Kept + 1);
        var rewards = f.Current.PlayerItems.InventoryItems.Where(row => row.ItemId == 3_306_885).ToArray();
        var relicUids = Enumerable.Range(1, 9).Select(index => checked(initialHigh + (uint)index)).ToArray();
        Assert.Equal(relicUids, rewards.Select(row => row.Uid));
        Assert.All(rewards, row => { Assert.Equal((byte)0, row.Bound); Assert.Equal((ushort)1, row.StackSize); });
        Assert.All(relicUids, uid => Assert.DoesNotContain(uid, beforeUids));
        Assert.Equal(initialHigh + 9, f.HighWater);
        var afterRelic = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!.AsObject();
        Assert.Equal(8, afterRelic["Version"]!.GetValue<int>());
        Assert.Equal(checked(beforeState["Generation"]!.GetValue<ulong>() + 2), afterRelic["Generation"]!.GetValue<ulong>());
        Assert.True(JsonNode.DeepEquals(beforeState["Operations"] ?? new JsonArray(), afterRelic["Operations"] ?? new JsonArray()));
        Assert.True(JsonNode.DeepEquals(beforeState["ProducerIntents"] ?? new JsonArray(), afterRelic["ProducerIntents"] ?? new JsonArray()));
        var oldHistory = beforeState.DeepClone().AsObject(); var newHistory = afterRelic.DeepClone().AsObject();
        foreach (var key in new[] { "Version", "Generation", "HighWater", "Operations", "ProducerIntents" })
        { oldHistory.Remove(key); newHistory.Remove(key); }
        Assert.True(JsonNode.DeepEquals(oldHistory, newHistory));
        beforeFiles[Path.GetRelativePath(f.Data.Root, f.ItemsPath)] = File.ReadAllBytes(f.ItemsPath);
        beforeFiles[Path.GetRelativePath(f.Data.Root, f.Data.State)] = File.ReadAllBytes(f.Data.State);
        f.Data.AssertFiles(beforeFiles); f.AssertConverged();

        var rune = await ConsumeAndAssert(f); // Existing independent files/native/packet-golden control.
        Assert.NotEmpty(rune.NewUids);
        Assert.Equal(Enumerable.Range(1, rune.NewUids.Length).Select(index => checked(initialHigh + 9U + (uint)index)), rune.NewUids);
        Assert.Empty(relicUids.Intersect(rune.NewUids));
        var afterRune = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!.AsObject();
        Assert.Equal(8, afterRune["Version"]!.GetValue<int>());
        Assert.Equal(initialHigh + 9U + (uint)rune.NewUids.Length, f.HighWater);
        var settled = f.Data.Files();
        await f.CloseAsync(); _ = f.Open(); f.Data.AssertFiles(settled);
        Assert.Equal(8, JsonNode.Parse(File.ReadAllBytes(f.Data.State))!["Version"]!.GetValue<int>());
        await f.CloseAsync(); await f.ReopenFrameAsync(); f.Sent.Clear(); f.AssertConverged();
        await f.SendAsync(request); await f.SendAsync(DecomposeFixture.Request());
        Assert.Empty(f.Sent); f.Data.AssertFiles(settled);
        Assert.Equal(initialHigh + 9U + (uint)rune.NewUids.Length, f.HighWater);
        Assert.Equal(8, JsonNode.Parse(File.ReadAllBytes(f.Data.State))!["Version"]!.GetValue<int>());
    }, relicFirst: true);

    private static byte[] RelicRequest(uint uid)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.MsgItemUsage, MsgItemUsagePacket.PayloadLength + sizeof(uint));
        writer.WriteUInt32(0, 0); writer.WriteUInt64(4, 0);
        writer.WriteUInt16(12, (ushort)MsgItemUsageAction.DecomposeRelics);
        writer.WriteUInt32(14, 511_043_495); writer.WriteUInt32(18, 1);
        writer.WriteUInt32(22, 0); writer.WriteUInt32(26, 0);
        writer.WriteUInt32(MsgItemUsagePacket.PayloadLength, uid); return writer.ToArray();
    }
}

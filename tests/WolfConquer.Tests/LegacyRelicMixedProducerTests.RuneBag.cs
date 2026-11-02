using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicMixedProducerTests
{
    private static async Task<uint> ReturnRune(MixedFixture f, uint selectedUid)
    {
        var before = Capture(f);
        var selected = before.Session.PlayerItems.RuneItems.Single(row => row.Uid == selectedUid);
        Assert.Equal((ushort)211, selected.Position);
        var issuedUid = checked(before.HighWater + 1);
        var result = selected.WithUid(issuedUid).WithPosition(0);
        var expected = before.Session.PlayerItems with
        {
            InventoryItems = before.Session.PlayerItems.InventoryItems.Append(result).ToArray(),
            RuneItems = before.Session.PlayerItems.RuneItems.Where(row => row.Uid != selectedUid).ToArray(),
        };
        var request = LegacyRelicMixedProducerData.RuneReturnRequest(selectedUid);
        f.Sent.Clear();
        f.BeforeSend = _ => AssertSavedMutation(f, before, expected, issued: true, version: Math.Max(6, before.Version));
        try { await f.SendAsync(request); } finally { f.BeforeSend = null; }
        AssertSavedMutation(f, before, expected, issued: true, version: Math.Max(6, before.Version));
        Assert.Equal(3, f.Sent.Count);
        Assert.Equal(RemoveInventoryGolden(selectedUid), f.Sent[0]);
        var display = LegacyGameItemPacket.CreateRuneSpellOverride(result, f.Catalog.ItemsById);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(result, display).ToFrameBytes(), f.Sent[1]);
        Assert.Equal(request, f.Sent[2]);
        Assert.DoesNotContain(f.Current.PlayerItems.RuneItems, row => row.Uid == selectedUid);
        Assert.Contains(f.Current.PlayerItems.InventoryItems, row => row.Uid == issuedUid && row.ItemId == selected.ItemId);
        Assert.Equal(before.Items.Length, File.ReadAllBytes(f.ItemsPath).Length);
        f.Sent.Clear();
        return issuedUid;
    }

    // Existing independent2137 removal golden: the retired storage identity
    // precedes the new2116 inventory row and the exact accepted request echo.
    private static byte[] RemoveInventoryGolden(uint uid)
    {
        var bytes = new byte[91];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 91);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2137);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), uid);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 3);
        return bytes;
    }
}

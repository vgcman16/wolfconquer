using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private static uint OwnedBagHighWater(byte[] state) => JsonNode.Parse(state)!["HighWater"]!.GetValue<uint>();
    private static Dictionary<string, byte[]> OwnedBagExpectedFiles(OwnedEquipmentBehaviorFixture f, uint actor, PlayerItemSnapshot items)
    {
        var files = f.Data.Files(); files[Path.GetRelativePath(f.Data.Root, f.ItemPath(actor))] = PlayerItemSnapshotLoader.ToLegacyBytes(items);
        return files;
    }
    private static void AssertOwnedBagConverged(OwnedEquipmentBehaviorFixture f, uint uid, Dictionary<string, byte[]> files)
    {
        // The only bookkeeping delta is the actual one-root receipt lifecycle;
        // keep every other file byte exact, including the peer and spell images.
        var expected = files.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        expected[Path.GetRelativePath(f.Data.Root, f.Data.State)] = File.ReadAllBytes(f.Data.State); f.Data.AssertFiles(expected);
        var current = f.Current(uid); var binding = current.NativeGameClient!.OrdinarySpellBinding!;
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.True(binding.Items.Image.IsCurrentPlain());
        Assert.Equal(File.ReadAllBytes(f.ItemPath(uid)), PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
        Assert.Equal(File.ReadAllBytes(f.ItemPath(uid)), PlayerItemSnapshotLoader.ToLegacyBytes(binding.Items.Image.FinalSnapshot));
        Assert.Equal(current.PlayerItems.InventoryItems.Select(row => row.Uid).Order(), current.NativeGameClient.Inventory!.ClientItems.Keys.Order());
        Assert.Equal(current.PlayerItems.RuneItems.Select(row => row.Uid).Order(), binding.Items.Rune.Objects.Select(row => row.Uid).Order());
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); Assert.False(File.Exists(Path.Combine(f.Data.Registry, "pending.json")));
    }
    private static void AssertOwnedBagUidTransition(OwnedEquipmentBehaviorFixture f, byte[] original, bool birth)
    {
        var actual = File.ReadAllBytes(f.Data.State);
        if (!birth) { Assert.Equal(original, actual); return; }
        var before = JsonNode.Parse(original)!.AsObject(); var after = JsonNode.Parse(actual)!.AsObject();
        Assert.Equal(checked(OwnedBagHighWater(original) + 1), after["HighWater"]!.GetValue<uint>());
        Assert.Equal(checked(before["Generation"]!.GetValue<ulong>() + 2), after["Generation"]!.GetValue<ulong>());
        Assert.True(JsonNode.DeepEquals(before["Operations"] ?? new JsonArray(), after["Operations"] ?? new JsonArray()));
        Assert.True(JsonNode.DeepEquals(before["ProducerIntents"] ?? new JsonArray(), after["ProducerIntents"] ?? new JsonArray()));
        foreach (var key in new[] { "Version", "Generation", "HighWater", "Operations", "ProducerIntents" }) { before.Remove(key); after.Remove(key); }
        Assert.True(JsonNode.DeepEquals(before, after));
    }
    private static byte[] OwnedBagRemoveGolden(uint uid)
    {
        var bytes = new byte[91]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 91); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2137);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), uid); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 3); return bytes;
    }
    private static byte[] OwnedBagUsageGolden(uint uid, ulong parameter, params uint[] args)
    {
        var bytes = new byte[91 + args.Length * 4]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)bytes.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2137); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), uid);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8), parameter); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 61);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(18), 511043495);
        for (int i = 0; i < args.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(91 + i * 4), args[i]); return bytes;
    }
    private static byte[] OwnedBagItemGolden(PlayerItemSnapshotItem row, bool update = false)
    {
        var bytes = new byte[206];
        void U16(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
        void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        U16(0, 206); U16(2, 2116); U32(4, row.Uid); U32(8, row.ItemId); U16(12, row.Durability); U16(14, row.MaximumDurability);
        U16(16, update ? (ushort)3 : (ushort)1); bytes[18] = checked((byte)row.Position); U32(19, row.SocketProgress);
        bytes[23] = row.SocketOne; bytes[24] = row.SocketTwo;
        uint spell = row.ItemId switch { 4_010_501 => 1425000, 4_020_501 => 1416000, 4_020_517 => 1416016, _ => 0 };
        U32(25, spell); bytes[29] = checked((byte)row.Effect); bytes[30] = row.Plus; bytes[31] = row.Bless; bytes[32] = row.Bound;
        bytes[33] = row.Enchant; bytes[38] = row.Suspicious; bytes[40] = row.Locked; U32(42, row.PlusProgress);
        U32(50, row.Activate == 0 ? row.TimeLeftInMinutes : 0); U16(54, row.StackSize); U32(56, row.PerfectionLevel); U32(60, row.PerfectionProgress); U32(64, row.OwnerUid);
        Encoding.UTF8.GetBytes(row.OwnerName).CopyTo(bytes, 68); Encoding.UTF8.GetBytes(row.Signature).CopyTo(bytes, 100);
        U32(166, row.RuneExperience); U32(170, row.Attribute1); U32(174, row.Attribute2); U32(178, row.Attribute3); U32(182, row.Attribute4);
        U32(186, row.Attribute5); U32(190, row.AnimaItemId); U32(198, row.MythSoul); U32(202, row.MythSoulPoints); return bytes;
    }
}

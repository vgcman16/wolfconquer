using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private static Dictionary<string, byte[]> OwnedComposeExpectedFiles(OwnedEquipmentBehaviorFixture f, uint uid, byte[] items)
    {
        var files = f.Data.Files(); files[Path.GetRelativePath(f.Data.Root, f.ItemPath(uid))] = items; return files;
    }

    private static void AssertOwnedComposeConverged(OwnedEquipmentBehaviorFixture f, uint uid, Dictionary<string, byte[]> expected)
    {
        f.Data.AssertFiles(expected);
        var current = f.Current(uid); var native = current.NativeGameClient!;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(native.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Equal(uid, native.Player!.UID);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems), File.ReadAllBytes(f.ItemPath(uid)));
        var spellsPath = Path.Combine(f.Data.Spells, uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        Assert.Equal(current.PlayerSpells.Spells, PlayerSpellSnapshotLoader.Parse(File.ReadAllBytes(spellsPath)).Spells);
        Assert.Equal(current.PlayerSpells.Spells.Select(row => row.Id).Order(), native.MySpells!.ClientSpells.Keys.Order());
        foreach (var row in current.PlayerSpells.Spells)
        {
            var physical = native.MySpells.ClientSpells[row.Id]; Assert.Equal(row.Level, physical.Level); Assert.Equal(row.Experience, physical.Experience);
            Assert.Equal(row.PreviousLevel, physical.PreviousLevel); Assert.Equal(row.SoulLevel, physical.SoulLevel); Assert.Equal(row.UseSpellSoul, physical.UseSpellSoul);
        }
        foreach (var row in current.PlayerItems.RuneItems)
        {
            var physical = Assert.Single(binding.Items.Rune.Objects, item => item.Uid == row.Uid);
            Assert.Equal(row.ItemId, physical.ITEM_ID); Assert.Equal(row.Position, physical.Position); Assert.Equal(row.RuneExperience, physical.RuneEXP);
        }
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath(uid)), out _));
        Assert.Equal(current.Character.MinHitPoints, saved.MinHitPoints); Assert.Equal(current.Character.MinHitPoints, (long)native.Player.HitPoints);
        Assert.Equal(current.Character.Money, saved.Money); Assert.Equal(current.Character.Money, native.Player.Money);
        Assert.Equal(current.Character.ConquerPoints, saved.ConquerPoints); Assert.Equal((uint)current.Character.ConquerPoints, native.Player.ConquerPoints);
        Assert.False(current.Character.Attributes.ContainsKey("ManagedRuneSpellOrigins")); Assert.False(saved.Attributes.ContainsKey("ManagedRuneSpellOrigins"));
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); Assert.False(f.Recovery.IsBlocked);
    }

    private static byte[] OwnedComposeRemoveGolden(uint uid)
    {
        var bytes = new byte[91]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 91); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2137);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), uid); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 3); return bytes;
    }

    private static byte[] OwnedComposeUpdateGolden(PlayerItemSnapshotItem item)
    {
        // Independent whole D13 2116 image for these explicit saved target rows.
        Assert.Equal(0U, item.SocketProgress); Assert.Equal((byte)0, item.Plus); Assert.Equal((byte)0, item.Bound);
        Assert.Equal(0U, item.PerfectionLevel); Assert.Equal(0U, item.Attribute1); Assert.Equal("", item.OwnerName);
        var bytes = new byte[206]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 206); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2116);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), item.Uid); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), item.ItemId);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), item.Durability); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), item.MaximumDurability);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 3); bytes[18] = checked((byte)item.Position);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(25), 1_416_000U + item.ItemId % 100 - 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(54), item.StackSize); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(166), item.RuneExperience);
        return bytes;
    }

    private static void AssertOwnedComposeUidRetirement(byte[] before, byte[] after, IEnumerable<uint> removed) =>
        Assert.Equal(LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(before).Except(removed).Order(),
            LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(after).Order());
}

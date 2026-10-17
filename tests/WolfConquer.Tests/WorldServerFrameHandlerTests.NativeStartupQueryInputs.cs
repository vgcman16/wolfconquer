using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private static WorldDataCatalog CreateNativeStartupQueryInputCatalog(uint characterUid = 1_293_600)
    {
        ItemType Item(int id, uint hitpoints, uint mana)
        {
            var fields = Enumerable.Repeat("0", 60).ToArray();
            fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "NativeStartup" + id;
            fields[18] = hitpoints.ToString(CultureInfo.InvariantCulture);
            fields[19] = mana.ToString(CultureInfo.InvariantCulture);
            return new ItemType(id, fields[1], fields);
        }
        CombatGear Gear(int level)
        {
            var fields = Enumerable.Repeat("0", 23).ToArray();
            fields[0] = (701_000 + level).ToString(CultureInfo.InvariantCulture);
            fields[1] = "701"; fields[2] = level.ToString(CultureInfo.InvariantCulture); fields[^1] = "";
            return CombatGearParser.ParseRow("fixture://native-startup/loaded-gear", level, string.Join("@@", fields));
        }
        var rankingFields = Enumerable.Repeat("0", 23).ToArray();
        rankingFields[0] = characterUid.ToString(CultureInfo.InvariantCulture); rankingFields[1] = "HeroOne"; rankingFields[2] = "40";
        rankingFields[3] = "1"; rankingFields[4] = "7051"; rankingFields[5] = "140";
        rankingFields[20] = ""; rankingFields[^1] = "";
        var weekly = RankingSnapshotParser.ParseCombatGearRankRow("fixture://native-startup/weekly-Pirate", 1,
            string.Join("/", rankingFields));
        // Bare Pirate:10836 attributes +14000 rolledChi +5000 Dragon400 =29836.
        // Real itemHP31676 produces61512; no persisted fake MaxHitpoints alias is needed.
        return CreateStaticVisibilityCatalog(items: [Item(500_109, 31_676, 5_000), Item(4_010_501, 0, 0)],
            combatGearRanks: [weekly], combatGear: [Gear(3), Gear(4)]);
    }

    private static void EarnNativeStartupMythSoulInput(string itemsRoot, uint characterUid = 1_293_600)
    {
        var loader = new PlayerItemSnapshotLoader(itemsRoot);
        var inputs = loader.LoadByCharacterUid(characterUid);
        Assert.Single(inputs.EquipmentItems);
        Assert.Equal(500_109U, inputs.EquipmentItems[0].ItemId);
        var path = Path.Combine(itemsRoot, characterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var bytes = File.ReadAllBytes(path);
        const int itemOffset = sizeof(int);
        const int mythSoulOffset = itemOffset + 176; // Native DBItem field, not the points field at180.
        Assert.True(bytes.Length >= itemOffset + PlayerItemSnapshotLoader.LegacyDbItemSize);
        Assert.Equal(inputs.EquipmentItems.Count + inputs.RuneItems.Count, BinaryPrimitives.ReadInt32LittleEndian(bytes));
        Assert.Equal(2_001U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(itemOffset, sizeof(uint))));
        Assert.Equal(500_109U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(itemOffset + 4, sizeof(uint))));
        Assert.Equal((ushort)4, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(itemOffset + 12, sizeof(ushort))));
        var original = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(mythSoulOffset, sizeof(uint)), 16U);
        File.WriteAllBytes(path, bytes); // Declared persisted item input, never a learned/cache/flag/outcome mutation.
        var persisted = File.ReadAllBytes(path);
        Assert.Equal(original[..mythSoulOffset], persisted[..mythSoulOffset]);
        Assert.Equal(original[(mythSoulOffset + sizeof(uint))..], persisted[(mythSoulOffset + sizeof(uint))..]);
        Assert.Equal(16U, Assert.Single(loader.LoadByCharacterUid(characterUid).EquipmentItems).MythSoul);
    }
}

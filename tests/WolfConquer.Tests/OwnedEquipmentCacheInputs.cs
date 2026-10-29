using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Original non-Venom/non-Jiang cache inputs only, saved by the real owned
    // fixture before bootstrap. This factory issues no accepted runtime state.
    internal static (WorldDataCatalog World, OwnedEquipmentBehaviorInput Input) CreateOwnedEquipmentCacheInputs()
    {
        ItemType Item(uint id, uint mana, uint hp = 0)
        {
            var fields = Enumerable.Repeat("0", 66).ToArray();
            fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "NativeCache" + fields[0];
            fields[18] = hp.ToString(CultureInfo.InvariantCulture);
            fields[19] = mana.ToString(CultureInfo.InvariantCulture);
            fields[ItemType.DurabilityFieldIndex] = "100";
            fields[ItemType.StackSizeFieldIndex] = "1";
            return new ItemType(checked((int)id), fields[1], fields);
        }
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
            [CreateMagicRow(14_500, sort: 6, useMana: 0, useStamina: 0,
                damage: 1, duration: 60, range: 18, coolDown: 0, coldTime: 0)],
            items: [Item(111_095, 10, 5_000), Item(110_099, 20), Item(410_095, 30),
                Item(150_095, 40), Item(120_095, 50)]);
        PlayerItemSnapshotItem Row(uint uid, uint id, ushort slot, uint effect = 0, uint purify = 0) => new()
        {
            Uid = uid, ItemId = id, Position = slot, Durability = 100, MaximumDurability = 100,
            StackSize = 1, Effect = effect, PurificationItemId = purify, PurificationDuration = 0,
        };
        var items = PlayerItemSnapshot.Empty with
        {
            EquipmentItems = new[] { Row(99_101, 111_095, 1, purify: 110_099), Row(99_102, 120_095, 2, 203),
                Row(99_106, 150_095, 6, 201), Row(99_104, 410_095, 4, 202) },
        };
        var owner = CreateSession(mapId: 1_000, strength: 0, spirit: 10, classCode: 14_005,
            minHitPoints: 5_000, minMana: 1_000, stamina: 100, spellIds: [14_500], playerItems: items,
            attributes: new Dictionary<string, string> { ["PkMode"] = "0", ["Stamina"] = "100" });
        return (world, new(owner.Character, owner.PlayerItems, owner.PlayerSpells, owner.PlayerProficiencies));
    }
}

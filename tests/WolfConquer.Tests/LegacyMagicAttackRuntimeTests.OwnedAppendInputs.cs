using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    internal static (WorldDataCatalog World, OwnedEquipmentBehaviorInput Actor, OwnedEquipmentBehaviorInput Peer) OwnedQueryAppendInputs()
    {
        const uint helmet = 111_099, armor = 130_099, replacement = 111_100, dummyHelmet = 111_101;
        ItemType Hp(uint id, string name, ushort hp, uint critical, uint bt, uint ca)
        {
            var row = DirectOverlayItem(id, name, critical, bt, ca);
            var fields = row.Fields.ToArray(); fields[ItemType.ItemHPFieldIndex] = hp.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return row with { Fields = fields };
        }
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
            [CreateMagicRow(DirectOverlaySpellId, sort: 99, useMana: 0, damage: 30_250, range: 4,
                damageOnHuman: 0, damage2: 33, damage3: 2, damageOnMonster: 30_085, gDamage: 0, coolDown: 0, coldTime: 0)],
            items: [Hp(helmet, "QueryStageHelmet", 50_000, 1_000, 60, 50), Hp(armor, "QueryStageArmor", 49_100, 0, 0, 0),
                Hp(replacement, "QueryStageReplacement", 50_000, 1_000, 80, 70), Hp(dummyHelmet, "QueryStageDummy", 50_000, 1_000, 100, 65),
                DirectOverlayItem(DirectOverlayFrontBreakRuneId, "Front Break (+9)"),
                DirectOverlayItem(DirectOverlaySyntheticImpregnabilityRuneId, "Impregnability(+9)")]);
        PlayerItemSnapshotItem Row(uint uid, uint id, ushort position) => new()
        { Uid = uid, ItemId = id, Position = position, Durability = 100, MaximumDurability = 100 };
        Dictionary<string, string> Attributes() => new()
        {
            ["PkMode"] = "0", ["BattlePower"] = "120", ["LegacyCombatGearAttackSpellIndex"] = "3",
            ["LegacyCombatGearAttackRateRoll"] = "99", ["LegacyPhysicalRefineryRoll"] = "0",
            ["LegacyPhysicalPlayerLuckyBranchRoll"] = "99", ["LegacyPhysicalPlayerCriticalRoll"] = "99",
            ["LegacyPhysicalDashRoll"] = "0", ["DashRate"] = "0", ["LegacyPhysicalBreakthroughRefineryRoll"] = "0",
            ["LegacyPhysicalImpregnabilityRoll"] = "0",
        };
        CombatGearSnapshot Gear() => CombatGearSnapshot.Empty with { Count = 1,
            Items = [new CombatGearSnapshotItem(201, 1, 0, 1, 0, 0, [], [])] };
        var actor = CreateSession(mapId: 1_000, classCode: 2_005, reborn: 2, strength: 300, spirit: 0,
            minHitPoints: 100_000, stamina: 100, spellIds: [DirectOverlaySpellId], attributes: Attributes(),
            playerItems: PlayerItemSnapshot.Empty with { EquipmentItems = [Row(98_801, helmet, 1), Row(98_802, armor, 3)],
                RuneItems = [Row(98_804, DirectOverlayFrontBreakRuneId, 103), Row(98_805, DirectOverlaySyntheticImpregnabilityRuneId, 104)],
                InventoryItems = [Row(98_803, replacement, 0)] }) with { CombatGear = Gear() };
        var peer = CreateSession(uid: TargetPlayerUid, name: "QueryStageDummy", mapId: 1_000, x: 839, y: 584,
            classCode: 2_005, reborn: 2, strength: 300, spirit: 0, minHitPoints: 100_000, stamina: 100,
            spellIds: [DirectOverlaySpellId], attributes: Attributes(),
            playerItems: PlayerItemSnapshot.Empty with { EquipmentItems = [Row(99_801, dummyHelmet, 1), Row(99_802, armor, 3)] }) with { CombatGear = Gear() };
        return (world, new(actor.Character, actor.PlayerItems, actor.PlayerSpells, actor.PlayerProficiencies, actor.CombatGear),
            new(peer.Character, peer.PlayerItems, peer.PlayerSpells, peer.PlayerProficiencies, peer.CombatGear));
    }
}

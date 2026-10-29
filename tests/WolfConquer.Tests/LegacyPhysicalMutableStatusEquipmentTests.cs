using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Pure catalog/snapshot data only. The standalone fixture below saves these
    // inputs before its real bootstrap/admission; this grants no runtime owner.
    internal static (WorldDataCatalog World, WorldSession Owner, WorldSession Striker)
        CreateOwnedPhysicalEquipmentInputs(string scenario, uint ownerUid, uint strikerUid)
    {
        const uint oldNormal = 111_050, newNormal = 111_051, oldAlternate = 111_052, newAlternate = 111_053, strikerHelmet = 111_054;
        var alternate = scenario is "normal-equip-while-alternate" or "active-alternate-equip" or "normal-unequip-while-alternate";
        var alternateEquip = scenario is "inactive-alternate-equip" or "active-alternate-equip";
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
        [
            CreateMagicRow(FineRainAttachStatusSpellId, sort: 6, useMana: 0, damage: 0,
                duration: 60, coolDown: 0, coldTime: 0),
            CreateMagicRow(DirectOverlaySpellId, sort: 99, useMana: 0, damage: 30_250,
                range: 4, damageOnHuman: 0, damage2: 33, damage3: 2, damageOnMonster: 30_085,
                gDamage: 0, coolDown: 0, coldTime: 0),
        ], items:
        [
            DirectOverlayItem(oldNormal, "OverlayEquipmentOldNormal", breakthrough: 60, counteraction: 50),
            DirectOverlayItem(newNormal, "OverlayEquipmentNewNormal", breakthrough: 80, counteraction: 70),
            DirectOverlayItem(oldAlternate, "OverlayEquipmentOldAlternate", breakthrough: 60, counteraction: 50),
            DirectOverlayItem(newAlternate, "OverlayEquipmentNewAlternate", breakthrough: 80, counteraction: 70),
            DirectOverlayItem(strikerHelmet, "OverlayEquipmentStriker", critical: 1_000, breakthrough: 100),
            DirectOverlayItem(DirectOverlaySyntheticImpregnabilityRuneId, "Impregnability(+9)"),
        ]);
        PlayerItemSnapshotItem Item(uint uid, uint id, ushort slot) => new()
        {
            Uid = uid, ItemId = id, Position = slot, Durability = 100, MaximumDurability = 100, StackSize = 1,
        };
        var owner = CreateSession(uid: ownerUid, mapId: 1_000, classCode: 2_005, reborn: 2,
            strength: 0, spirit: 0, minHitPoints: 100_000, stamina: 100, spellIds: [FineRainAttachStatusSpellId],
            playerItems: PlayerItemSnapshot.Empty with
            {
                EquipmentItems = [Item(91_001, oldNormal, 1), Item(91_002, oldAlternate, 51)],
                InventoryItems = [Item(91_003, alternateEquip ? newAlternate : newNormal, 0)],
                RuneItems = [Item(91_004, DirectOverlaySyntheticImpregnabilityRuneId, alternate ? (ushort)123 : (ushort)103)],
            },
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0", ["MaxHitpoints"] = "100000", ["BattlePower"] = "200",
                ["LegacyPhysicalImpregnabilityRoll"] = "0",
            }) with { AlternateEquipmentActive = alternate };
        var striker = CreateSession(uid: strikerUid, mapId: 1_000, x: 839, y: 584, classCode: 2_005,
            strength: 300, spirit: 0, minHitPoints: 100_000, stamina: 100, spellIds: [DirectOverlaySpellId],
            playerItems: PlayerItemSnapshot.Empty with { EquipmentItems = [Item(92_001, strikerHelmet, 1)] },
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0", ["MaxHitpoints"] = "100000", ["BattlePower"] = "100",
                ["LegacyCombatGearAttackSpellIndex"] = "3", ["LegacyCombatGearAttackRateRoll"] = "99",
                ["LegacyPhysicalRefineryRoll"] = "0", ["LegacyPhysicalPlayerLuckyBranchRoll"] = "99",
                ["LegacyPhysicalPlayerCriticalRoll"] = "99", ["LegacyPhysicalDashRoll"] = "0", ["DashRate"] = "0",
                ["LegacyPhysicalBreakthroughRefineryRoll"] = "0",
            }) with
        {
            CombatGear = CombatGearSnapshot.Empty with
            {
                Count = 1, Items = [new CombatGearSnapshotItem(201, Level: 1, Exp: 0, Equip: 1,
                    Transform: 0, MasteryScore: 0, Animas: [], BlackJades: [])],
            },
        };
        return (world, owner, striker);
    }
}

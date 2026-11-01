using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Pure versions of the original input construction, saved and admitted by
    // the real owned fixture. Distinct target UIDs satisfy the real UID domain.
    internal static (WorldDataCatalog World, OwnedEquipmentBehaviorInput Actor, OwnedEquipmentBehaviorInput Target,
        OwnedEquipmentBehaviorInput Observer) OwnedOverlayReentryInputs(bool impregnation, ushort spellId, bool callback)
    {
        const uint zeroHelmet = DirectOverlayCasterHelmetId + 1, observerHelmet = 111_058;
        var magic = spellId == 17_650
            ? CreateMagicRow(spellId, sort: 90, useMana: 0, damage: 30_500, range: 10,
                damageOnHuman: 38, damage2: 30_205, damage3: 2, damageOnMonster: 7, gDamage: 38, coolDown: 0, coldTime: 0)
            : CreateMagicRow(spellId, sort: 99, useMana: 0, damage: 30_250, range: 4,
                damageOnHuman: 0, damage2: 33, damage3: 2, damageOnMonster: 30_085, gDamage: 0, coolDown: 0, coldTime: 0);
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], new MagicType[] { magic }, items:
        [
            NativeCombatGearProcFixture.WithHitpoints(DirectOverlayItem(DirectOverlayCasterHelmetId, "OwnedOverlayCaster", critical: 1_000, breakthrough: impregnation ? 100U : 60U), 51_100),
            NativeCombatGearProcFixture.WithHitpoints(DirectOverlayItem(zeroHelmet, "OwnedOverlayReplacement", breakthrough: 60), 51_100),
            NativeCombatGearProcFixture.WithHitpoints(DirectOverlayItem(DirectOverlayTargetHelmetId, "OwnedOverlayTarget", counteraction: impregnation || callback ? 50U : 65U), 52_000),
            NativeCombatGearProcFixture.WithHitpoints(DirectOverlayItem(observerHelmet, "OwnedOverlayObserver"), 52_000),
            DirectOverlayItem(DirectOverlayFrontBreakRuneId, "Front Break (+9)"),
            DirectOverlayItem(DirectOverlaySyntheticImpregnabilityRuneId, "Impregnability(+9)"),
        ], combatGear: NativeCombatGearProcFixture.CatalogRows());
        PlayerItemSnapshot Items(uint uid, uint helmet, uint? rune, bool replacement, uint replacementType) =>
            PlayerItemSnapshot.Empty with
            {
                EquipmentItems = [new PlayerItemSnapshotItem { Uid = uid, ItemId = helmet, Position = 1, Durability = 100, MaximumDurability = 100 }],
                InventoryItems = replacement ? [new PlayerItemSnapshotItem { Uid = uid + 1, ItemId = replacementType, Durability = 100, MaximumDurability = 100 }] : [],
                RuneItems = rune is { } id ? [new PlayerItemSnapshotItem { Uid = uid + 2, ItemId = id, Position = 103 }] : [],
            };
        var caster = CreateSession(mapId: 1_000, classCode: 2_005, reborn: impregnation ? 0 : 2,
            strength: 300, spirit: 0, minHitPoints: 100_000, stamina: 100, spellIds: [spellId],
            playerItems: Items(90_001, DirectOverlayCasterHelmetId, impregnation ? null : DirectOverlayFrontBreakRuneId,
                callback || !impregnation, callback ? zeroHelmet : DirectOverlayCasterHelmetId),
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0", ["BattlePower"] = callback ? "120" : "100",
                ["LegacyCombatGearAttackSpellIndex"] = "3", ["LegacyCombatGearAttackRateRoll"] = callback ? "0" : "99",
                ["LegacyPhysicalRefineryRoll"] = "0", ["LegacyPhysicalPlayerCriticalRoll"] = "99",
                ["LegacyPhysicalPlayerLuckyBranchRoll"] = "99", ["DashRate"] = callback ? "10000" : "0",
                ["LegacyPhysicalDashRoll"] = "0", ["LegacyPhysicalBreakthroughRefineryRoll"] = "0",
                ["LegacyPhysicalImpregnabilityRoll"] = "0",
            }) with { CombatGear = CombatGearSnapshot.Empty with { Count = 1,
                Items = [new CombatGearSnapshotItem(201, 1, 0, 1, 0, 0, [], [])] } };
        // All HP is backed by saved Vitality and actual helmet metadata before
        // bootstrap. No MaxHitpoints attribute substitutes for the real query.
        caster = caster with { Character = caster.Character with { Vitality = 2_000 } };
        var target = CreateSession(uid: TargetPlayerUid, name: "OwnedOverlayTarget", mapId: 1_000, x: 831, y: 584,
            classCode: 2_005, reborn: impregnation || callback ? 2 : 0, strength: 0, spirit: 0, minHitPoints: 100_000, spellIds: [],
            playerItems: Items(91_001, DirectOverlayTargetHelmetId,
                impregnation || callback ? DirectOverlaySyntheticImpregnabilityRuneId : null,
                impregnation && !callback, DirectOverlayTargetHelmetId),
            attributes: new Dictionary<string, string>
            {
                ["BattlePower"] = callback ? "120" : "200",
                ["LegacyPhysicalImpregnabilityRoll"] = "0", ["LegacySpeffectLevel.Solid"] = "0", ["LegacySpeffectRoll.Solid"] = "0",
            });
        target = target with { Character = target.Character with { Vitality = 2_000 } };
        var observer = CreateSession(uid: PlayerUid + 50, mapId: 1_000, x: 831, y: 588,
            strength: 0, spirit: 0, minHitPoints: 100_000, spellIds: [],
            playerItems: Items(93_001, observerHelmet, null, false, observerHelmet));
        observer = observer with { Character = observer.Character with { Vitality = 2_000 } };
        return (world, new(caster.Character, caster.PlayerItems, caster.PlayerSpells, caster.PlayerProficiencies, caster.CombatGear),
            new(target.Character, target.PlayerItems, target.PlayerSpells, target.PlayerProficiencies, target.CombatGear),
            new(observer.Character, observer.PlayerItems, observer.PlayerSpells, observer.PlayerProficiencies, observer.CombatGear));
    }
}

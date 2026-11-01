using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed record DragonhowlExperienceOverlapInput(WorldDataCatalog Catalog,
    OwnedEquipmentBehaviorInput Actor, OwnedEquipmentBehaviorInput Peer, Monster Family);

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Only saved input/catalog construction. Actual native19 publication and
    // ordinary admission are performed by OwnedEquipmentBehaviorFixture.
    internal static DragonhowlExperienceOverlapInput DragonhowlExperienceOverlapInputs(ushort spellId)
    {
        Assert.True(spellId is 17_490 or 17_650);
        const int map = 1_018;
        var family = new Monster(910, "OwnedOverlapMob", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "500", ["level"] = "10", ["Boss"] = "0", ["lookface"] = "910",
            ["defense"] = "0", ["defense2"] = "10000", ["attack_range"] = "1", ["view_range"] = "30",
            ["attack_speed"] = "1000", ["move_speed"] = "500", ["attack_min"] = "5", ["attack_max"] = "6",
            ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        // Source row2857 includes zero player MIN candidates: Human0 and
        // plain33/2 are not encoded bonuses. Strength1000 gives base1000;
        // neutral no-breakthrough adds50%=1500, then Ironbone3/4 gives1125.
        // Existing DirectTriple source-contract controls independently cover
        // this interpretation; no level multiplier or extra2% is introduced.
        var magic = CreateMagicRow(spellId, sort: 99, useMana: 7, damage: 30_250, range: 4,
            useStamina: 5, coolDown: 0, coldTime: 0, damageOnHuman: 0, damage2: 33,
            damage3: 2, damageOnMonster: 30_085, gDamage: spellId == 17_650 ? 200U : 100U);
        var thresholds = new[]
        {
            new LevelExperience(0, 10, 1, 0, 0, 1, "0 10 1 0 0", new[] { "0", "10", "1", "0", "0" }),
            new LevelExperience(0, 11, 1_000_000, 0, 0, 2, "0 11 1000000 0 0", new[] { "0", "11", "1000000", "0", "0" }),
        };
        var stats = new[] { new ClassLevelStat("Taoist", 11, 0, 0, 20, 20,
            "fixture://overlap/Taoist.ini", 1, "11", "0,0,20,20", "11=0,0,20,20", new[] { "0", "0", "20", "20" }) };
        var catalog = LegacyItemWorldDataFixture.Create(Array.Empty<ItemType>(), [],
            classLevelStats: stats, monsters: new[] { family }, magic: new[] { magic },
            levelExperience: thresholds, combatGear: NativeCombatGearProcFixture.CatalogRows());
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 2_005, level: 10, reborn: 0,
            strength: 1_000, agility: 0, mapId: map, x: 830, y: 584, minHitPoints: 51_000) with
        {
            Vitality = 2_000, Spirit = 0, MinMana = 500, MaxMana = 500,
            Attributes = Fields(actor: true),
        };
        var peer = OwnedEquipmentBehaviorFixture.Character(uid: OwnedEquipmentBehaviorFixture.Peer, name: "OverlapPeer",
            classCode: 14_005, level: 10, reborn: 0, strength: 0, agility: 0,
            mapId: map, x: 832, y: 584, minHitPoints: 30) with
        {
            Vitality = 0, Spirit = 10, MinMana = 20, MaxMana = 450,
            Attributes = Fields(actor: false),
        };
        var gear = CombatGearSnapshot.Empty with
            { Count = 1, Items = new[] { new CombatGearSnapshotItem(201, 1, 0, 1, 0, 0, [], []) } };
        var learned = new PlayerSpellSnapshot(new[] { new PlayerSpellSnapshotEntry(spellId, 0, 0, 0, 0, 0) });
        return new(catalog, new(actor, PlayerItemSnapshot.Empty, learned, CombatGear: gear),
            new(peer, PlayerItemSnapshot.Empty), family);

        static Dictionary<string, string> Fields(bool actor) => new(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100", ["PkMode"] = "0",
            ["BattlePower"] = "120", ["UserExpRate"] = "1", ["OpaqueOverlap"] = actor ? "actor-seed" : "peer-seed",
            // Source-supported deterministic roll inputs are saved before
            // admission. They never stand in for a kill/reward/gear owner.
            ["LegacyCombatGearAttackSpellIndex"] = "3", ["LegacyCombatGearAttackRateRoll"] = "0",
            ["LegacyMonsterDropRoll"] = "0", ["LegacyMonsterDeathConquerPointRatePercent"] = "100",
            ["LegacyMonsterDeathConquerPointRateRoll"] = "0", ["LegacyMonsterDeathConquerPointRoll"] = "5",
        };
    }
}

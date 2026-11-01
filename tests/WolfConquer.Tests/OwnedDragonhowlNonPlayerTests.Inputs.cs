using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed record OwnedDragonhowlNonPlayerInput(WorldDataCatalog World, OwnedEquipmentBehaviorInput Actor,
    Monster Family, SobNpc? Sob);

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Pure saved/catalog inputs, before actual native publisher and admission.
    // Reuses the original nonplayer row's independent1050 damage construction.
    internal static OwnedDragonhowlNonPlayerInput OwnedDragonhowlNonPlayerInputs(bool sobTarget, ushort manaCost)
    {
        var template = CreateMonsterFamily();
        var family = template with
        {
            Attributes = new Dictionary<string, string>(template.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = template.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["life"] = "100000", ["level"] = "121", ["defense"] = "0", ["defense2"] = "10000",
                ["Boss"] = "1", ["attack_range"] = "0",
            },
        };
        var sob = CreateMoveLineSobNpc(65_174, 831, 584);
        var sobFields = sob.Fields.ToArray();
        sobFields[4] = "1000"; sobFields[7] = sobFields[8] = "100000";
        sob = sob with { MapId = 1_000, Life = 100_000, MaxLife = 100_000,
            Fields = sobFields, RawLine = string.Join(',', sobFields) };
        const uint helmet = 111_070;
        var world = CreateMagicCatalogCore(family, [], sobTarget ? new[] { sob } : [],
            new MagicType[]
            {
                CreateMagicRow(17_490, sort: 99, useMana: manaCost, damage: 30_250, range: 4,
                    useStamina: 0, coolDown: 0, coldTime: 0, damageOnHuman: 0, damage2: 33,
                    damage3: 2, damageOnMonster: 30_085, gDamage: 0),
            }, items: new[] { NativeCombatGearProcFixture.WithHitpoints(DirectOverlayItem(helmet, "OwnedNonPlayerHelmet"), 51_100) },
            combatGear: NativeCombatGearProcFixture.CatalogRows(), monsterRows: new[] { family });
        var actor = CreateSession(mapId: 1_000, classCode: 2_005, strength: 300, spirit: 0,
            minHitPoints: 100_000, stamina: 100, spellIds: [17_490],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0", ["BattlePower"] = "120", ["LegacyCombatGearAttackSpellIndex"] = "3",
                ["LegacyCombatGearAttackRateRoll"] = "99",
            }, playerItems: PlayerItemSnapshot.Empty with
            {
                EquipmentItems = [new PlayerItemSnapshotItem { Uid = 94_001, ItemId = helmet, Position = 1,
                    Durability = 100, MaximumDurability = 100, StackSize = 1 }],
            }) with
        {
            CombatGear = CombatGearSnapshot.Empty with
                { Count = 1, Items = [new CombatGearSnapshotItem(201, 1, 0, 1, 0, 0, [], [])] },
        };
        actor = actor with { Character = actor.Character with { Vitality = 2_000, Agility = 0 } };
        return new(world, new(actor.Character, actor.PlayerItems, actor.PlayerSpells,
            actor.PlayerProficiencies, actor.CombatGear), family, sobTarget ? sob : null);
    }
}

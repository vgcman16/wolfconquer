using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private static (WorldDataCatalog World, OwnedEquipmentBehaviorInput Owner, OwnedEquipmentBehaviorInput Peer)
        OwnedRootMagicInputs(string scenario)
    {
        var crop = scenario == "crop";
        var xp = scenario == "xp";
        ItemType Item(uint id, uint hp = 0, uint mp = 0) => OwnedEquipmentBehaviorFixture.Item(id,
            "NativeCrop" + id, new Dictionary<int, string>
            { [18] = hp.ToString(CultureInfo.InvariantCulture), [19] = mp.ToString(CultureInfo.InvariantCulture) });
        var magic = new List<MagicType>
        {
            CreateMagicRow(1000, sort: 1, useMana: 40, useStamina: 10, damage: 100, range: 4, coolDown: 0, coldTime: 0),
            CreateMagicRow(7001, sort: 32, useMana: 0, useStamina: 0, damage: 100, coolDown: 0, coldTime: 0),
        };
        if (crop) magic.Add(CreateMagicRow(14500, sort: 6, useMana: 0, useStamina: 0,
            damage: 1, duration: 60, range: 18, coolDown: 0, coldTime: 0));
        if (xp) magic.Add(CreateMagicRow(1020, sort: 6, useMana: 0, useStamina: 0,
            damage: 100, duration: 30, range: 18, coolDown: 0, coldTime: 0));
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], magic,
            items: crop ? [Item(111095, 5000), Item(203003), Item(111005, 50_000), Item(120005, 50_000)] :
                [Item(111095, 5000, 1000), Item(410095), Item(150095), Item(120095), Item(111005, 50_000), Item(120005, 50_000)]);
        PlayerItemSnapshotItem Row(uint uid, uint id, ushort position, uint effect = 0) => new()
        { Uid = uid, ItemId = id, Position = position, Effect = effect, Durability = 100, MaximumDurability = 100, StackSize = 1 };
        PlayerItemSnapshotItem[] equipment = crop
            ? [Row(99180, 203003, 18), Row(99181, 111095, 1)] :
            [Row(99181, 111095, 1), Row(99184, 410095, 4, xp ? 0U : 202U),
                Row(99186, 150095, 6, xp ? 0U : 1U), Row(99182, 120095, 2, xp ? 0U : 200U)];
        var attrs = new Dictionary<string, string>
        {
            ["PkMode"] = "0", ["BattlePower"] = "120", ["LegacyPhysicalPlayerLuckyBranchRoll"] = "99",
            ["LegacyPhysicalPlayerCriticalRoll"] = "99", ["LegacyWeaponEffectRateRoll.1175"] = "49",
            ["LegacyWeaponEffectRateRoll.1095"] = "19", ["LegacyWeaponEffectRateRoll.1020"] = "19",
        };
        var owner = CreateSession(mapId: 1000, strength: 0, spirit: 0, classCode: 142, minHitPoints: 5000,
            minMana: scenario == "manual" ? 100 : 1000, stamina: 100,
            spellIds: magic.Select(row => checked((ushort)row.TypeId)).ToArray(), attributes: attrs,
            playerItems: new(xp ? new[] { Row(99183, 120095, 0, 200) } : [], [], [], equipment, []));
        var peer = CreateSession(uid: TargetPlayerUid, name: "RootEntryPeer", mapId: 1000, x: 832, y: 584,
            strength: 0, spirit: 0, minHitPoints: 100_000, minMana: 1000, stamina: 100, spellIds: [],
            // Real startup/query has no current-HP floor. Back the original
            //100000 target HP with actual saved zero-proc catalogued equipment.
            // Each50000 field fits both ushort and this decoder's uint width.
            playerItems: new([], [], [], new[] { Row(99191, 111005, 1), Row(99192, 120005, 2) }, []),
            attributes: new Dictionary<string, string> { ["PkMode"] = "0", ["BattlePower"] = "120" });
        return (world, new(owner.Character, owner.PlayerItems, owner.PlayerSpells, owner.PlayerProficiencies),
            new(peer.Character, peer.PlayerItems, peer.PlayerSpells, peer.PlayerProficiencies));
    }
}

using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Pure saved input/catalog construction only. The existing owned fixture
    // supplies real admission, runtime, registry, PIN, recovery and transport.
    internal static Task RestorationStaminaRun(ushort id, string? rawStamina, bool blessed,
        bool omnipotence, byte staminaCost, ushort manaCost, Func<OwnedEquipmentBehaviorFixture, Task> exercise)
    {
        var input = RestorationStaminaInputs(id, rawStamina, blessed, omnipotence, staminaCost, manaCost);
        return OwnedEquipmentBehaviorFixture.RunAsync(input.Actor, input.World, exercise);
    }

    internal static Task RestorationStaminaRejected(ushort id, string rawStamina, bool blessed = false,
        bool omnipotence = false, byte staminaCost = 0, ushort manaCost = 7)
    {
        var input = RestorationStaminaInputs(id, rawStamina, blessed, omnipotence, staminaCost, manaCost);
        var source = CreateSession(uid: TargetPlayerUid, name: "RestorationAdmissionPeer", mapId: 1000,
            x: 832, y: 584, minHitPoints: 1000, minMana: 500,
            spellIds: Array.Empty<ushort>(), playerItems: PlayerItemSnapshot.Empty,
            attributes: new Dictionary<string, string> { ["Stamina"] = "100" });
        var peer = new OwnedEquipmentBehaviorInput(source.Character, source.PlayerItems, source.PlayerSpells, source.PlayerProficiencies);
        return RunRejectedStaminaParticipants(input.Actor, peer, input.World, id);
    }

    private static (OwnedEquipmentBehaviorInput Actor, WorldDataCatalog World) RestorationStaminaInputs(
        ushort id, string? rawStamina, bool blessed, bool omnipotence, byte staminaCost, ushort manaCost)
    {
        var attributes = new Dictionary<string, string>();
        if (rawStamina is not null) attributes["Stamina"] = rawStamina;
        if (blessed) attributes["HeavenBlessing"] = "1";
        if (omnipotence) attributes["StatusFlags"] = BuildStatusFlags(0xC0);
        var magic = CreateMagicRow(id, sort: id == 11960 ? (byte)70 : (byte)91,
            useMana: manaCost, useStamina: staminaCost, damage: 12, experience: 5000,
            range: 18, coolDown: 1, coldTime: 1500);
        var next = CreateMagicRow(id, sort: id == 11960 ? (byte)70 : (byte)91, level: 1,
            useMana: manaCost, useStamina: staminaCost, damage: 13, experience: 10000,
            range: 18, coolDown: 1, coldTime: 1500);
        var head = OwnedEquipmentBehaviorFixture.Item(111003, "RestorationHead",
            new Dictionary<int, string> { [18] = "5000", [19] = "5000" });
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], [magic, next], items: [head]);
        var session = CreateSession(mapId: 1000, minHitPoints: 1000, minMana: 500,
            spellEntries: new[] { new PlayerSpellSnapshotEntry(id, 0, 0, 0, 0, 0) }, attributes: attributes,
            playerItems: new PlayerItemSnapshot([], [], [], new[] { new PlayerItemSnapshotItem
                { Uid = 99181, ItemId = 111003, Position = 1, StackSize = 1, Durability = 100, MaximumDurability = 100 } }, []));
        return (new(session.Character, session.PlayerItems, session.PlayerSpells, session.PlayerProficiencies), world);
    }
}

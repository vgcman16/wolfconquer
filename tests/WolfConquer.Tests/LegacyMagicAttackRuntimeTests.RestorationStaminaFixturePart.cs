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
        return OwnedEquipmentBehaviorFixture.RunAsync(new(session.Character, session.PlayerItems, session.PlayerSpells,
            session.PlayerProficiencies), world, exercise);
    }
}

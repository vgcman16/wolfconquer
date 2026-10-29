using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Pure complete saved inputs, before the existing fixture's real bootstrap.
    // The two passives coexist through actual rune/perfection input fields.
    internal static Task CapriceContinuationLightRunAsync(
        Func<OwnedEquipmentBehaviorFixture, WorldDataCatalog, Task> body)
    {
        var world = CreateMagicCatalogWithItems(CreateMonsterFamily(), [],
            [CreateRetainedNativeMagic421Helmet(),
             OwnedEquipmentBehaviorFixture.Item(LegacyCapriceLevelTwentySevenItemId, "Caprice (+27)")],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var actor = CreateRetainedNativeMagic421Input(CreateSession(stamina: 40,
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            { Uid = LegacyCapriceLevelTwentySevenItemId, ItemId = LegacyCapriceLevelTwentySevenItemId, Position = 101 }),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyCapriceRoll"] = "0", ["LegacyCapriceBranch"] = "1",
                ["LegacyCapriceStampUtc"] = "2026-01-01T00:00:00.0000000+00:00",
                ["PerfectionStatus.LightOfStamina"] = "100", ["LegacyLightOfStaminaRoll"] = "0",
                ["PrestigeLevel"] = "5",
            }));
        var peer = CreateRetainedNativeMagic421Input(CreateSession(uid: TargetPlayerUid, name: "CapriceLightPeer",
            x: 832, y: 584, stamina: 80, attributes: new Dictionary<string, string>
            { ["PrestigeLevel"] = "5", ["LegacyConsolidationRoll"] = "99" }));
        return OwnedEquipmentBehaviorFixture.RunAsync(new(actor.Character, actor.PlayerItems, actor.PlayerSpells), world,
            f => body(f, world), new OwnedEquipmentBehaviorInput(peer.Character, peer.PlayerItems, peer.PlayerSpells));
    }
}

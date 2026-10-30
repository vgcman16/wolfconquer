using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    internal static Task CapricePairConsolidationRunAsync(Func<OwnedEquipmentBehaviorFixture, WorldDataCatalog, Task> body)
    {
        var world = CreateMagicCatalogWithItems(CreateMonsterFamily(), [],
            [CreateRetainedNativeMagic421Helmet(), OwnedEquipmentBehaviorFixture.Item(LegacyCapriceLevelTwentySevenItemId, "Caprice (+27)"),
             OwnedEquipmentBehaviorFixture.Item(LegacyConsolidationLevelNineItemId, "Consolidation(+9)")],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var actor = CreateRetainedNativeMagic421Input(CreateSession(stamina: 40,
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem { Uid = LegacyCapriceLevelTwentySevenItemId,
                ItemId = LegacyCapriceLevelTwentySevenItemId, Position = 101 }),
            attributes: new Dictionary<string, string> { ["LegacyCapriceRoll"] = "0", ["LegacyCapriceBranch"] = "1",
                ["LegacyCapriceStampUtc"] = "2026-01-01T00:00:00.0000000+00:00" }));
        var peer = CreateRetainedNativeMagic421Input(CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584, stamina: 80,
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem { Uid = LegacyConsolidationLevelNineItemId,
                ItemId = LegacyConsolidationLevelNineItemId, Position = 101 }),
            attributes: new Dictionary<string, string> { ["LegacyConsolidationRoll"] = "99" }));
        return OwnedEquipmentBehaviorFixture.RunAsync(new(actor.Character, actor.PlayerItems, actor.PlayerSpells), world,
            fixture => body(fixture, world), new OwnedEquipmentBehaviorInput(peer.Character, peer.PlayerItems, peer.PlayerSpells));
    }
}

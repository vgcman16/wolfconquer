using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    internal static Task CapriceNestedRunAsync(ushort proc,
        Func<OwnedEquipmentBehaviorFixture, WorldDataCatalog, Task> body)
    {
        // Explicit parsed fixture metadata, not an assertion about stock prices
        // or equipped-gear selection. Both real wrapper entries resolve this row.
        var row = CreateMagicRow(proc, sort: proc == 10950 ? (byte)5 : (byte)1,
            useMana: 7, useStamina: 0, damage: 541, range: 3, coldTime: 1000,
            coolDown: 0, gDamage: 100);
        var world = CreateMagicCatalogWithItems(CreateMonsterFamily(), [],
            [CreateRetainedNativeMagic421Helmet(),
             OwnedEquipmentBehaviorFixture.Item(LegacyCapriceLevelTwentySevenItemId, "Caprice (+27)")], row);
        static Dictionary<string, string> Transform(uint maximum) => new(StringComparer.OrdinalIgnoreCase)
        {
            ["PkMode"] = "0", ["BattlePower"] = "120",
            ["TransformationID"] = "2", ["TransformationId"] = "2",
            [LegacyTransformState.ActiveAttribute] = "1",
            [LegacyTransformState.ExpiresAtUtcAttribute] = "2030-01-01T00:00:00.0000000+00:00",
            [LegacyTransformState.MaxHitpointsAttribute] = maximum.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        var actorAttributes = Transform(3000);
        actorAttributes["LegacyCapriceRoll"] = "0";
        actorAttributes["LegacyCapriceBranch"] = "1";
        actorAttributes["LegacyCapriceStampUtc"] = "2026-01-01T00:00:00.0000000+00:00";
        var actor = CreateRetainedNativeMagic421Input(CreateSession(stamina: 40, minHitPoints: 3000,
            spellIds: [proc], attributes: actorAttributes,
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            { Uid = LegacyCapriceLevelTwentySevenItemId, ItemId = LegacyCapriceLevelTwentySevenItemId, Position = 101 })));
        var peer = CreateRetainedNativeMagic421Input(CreateSession(uid: TargetPlayerUid, name: "NestedCapricePeer",
            x: 832, y: 584, stamina: 80, minHitPoints: 1, spellIds: [], attributes: Transform(1)));
        return OwnedEquipmentBehaviorFixture.RunAsync(new(actor.Character, actor.PlayerItems, actor.PlayerSpells), world,
            f => body(f, world), new OwnedEquipmentBehaviorInput(peer.Character, peer.PlayerItems, peer.PlayerSpells));
    }
}

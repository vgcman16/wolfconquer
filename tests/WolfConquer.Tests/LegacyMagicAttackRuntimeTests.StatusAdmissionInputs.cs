using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    internal static WorldSession StatusAdmissionState(WorldSession source, bool xpList, string? block = null)
    {
        var attributes = new Dictionary<string, string>(source.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        var flags = new List<int> { 0x1FF };
        if (xpList) flags.Add(LegacyXpListFlag);
        if (block == "ride") flags.Add(0x32);
        if (block == "shadow") flags.Add(0x91);
        attributes["StatusFlags"] = BuildStatusFlags(flags.ToArray());
        attributes.Remove("TransformationID"); attributes.Remove("TransformationId");
        if (block == "transform") attributes["TransformationID"] = "2";
        return source with { Character = source.Character with { Attributes = attributes } };
    }

    internal static Task StatusAdmissionRunAsync(ushort id, bool xpList,
        Func<OwnedEquipmentBehaviorFixture, Task> body, string? block = null, long map = 1000,
        string rawStamina = "100")
    {
        var row = id == 8003
            ? CreateMagicRow(id, sort: 6, level: 2, useMana: 7, useStamina: 4, duration: 55, coldTime: 0)
            : CreateAttachStatusXpBuffSourceRow(id, observableResourceCosts: true);
        var head = OwnedEquipmentBehaviorFixture.Item(111003, "StatusAdmissionHead",
            new Dictionary<int, string> { [18] = "5000", [19] = "5000" });
        // XP Fly's source row requires weapon family500. Supply an actual
        // catalogued, saved bow rather than deleting that metadata prerequisite.
        var bow = OwnedEquipmentBehaviorFixture.Item(500003, "StatusAdmissionBow");
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], [row], items: [head, bow]);
        OwnedEquipmentBehaviorInput Input(uint uid)
        {
            // Adjacent character UIDs need disjoint TWO-row historical item
            // ranges before the real registry scans this complete fixture.
            var firstItemUid = checked(uid * 2 + 70000);
            var items = new PlayerItemSnapshot([], [], [], new[]
            {
                new PlayerItemSnapshotItem { Uid = firstItemUid, ItemId = 111003, Position = 1,
                    Durability = 100, MaximumDurability = 100, StackSize = 1 },
                new PlayerItemSnapshotItem { Uid = checked(firstItemUid + 1), ItemId = 500003, Position = 4,
                    Durability = 100, MaximumDurability = 100, StackSize = 1 },
            }, []);
            var source = CreateSession(uid: uid, name: uid == PlayerUid ? "StatusAdmissionActor" : "StatusAdmissionPeer",
                x: uid == PlayerUid ? 830 : 832, mapId: map, minHitPoints: 1000, minMana: 500,
                strength: 300, stamina: 100, playerItems: items,
                spellEntries: uid == PlayerUid ? new[] { CreatePlayerSpell(id, id == 8003 ? (ushort)2 : (ushort)0) } :
                    Array.Empty<PlayerSpellSnapshotEntry>(),
                attributes: new Dictionary<string, string> { ["XPCount"] = "37", ["PkMode"] = "0" });
            source = StatusAdmissionState(source, uid == PlayerUid && xpList, uid == PlayerUid ? block : null);
            if (uid == PlayerUid)
                source = source with { Character = source.Character with
                { Attributes = new Dictionary<string, string>(source.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                    { ["Stamina"] = rawStamina } } };
            return new(source.Character, source.PlayerItems, source.PlayerSpells, source.PlayerProficiencies);
        }
        var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 29, 16, 0, 0, TimeSpan.Zero));
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(Input(PlayerUid), world,
            new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock), async f =>
            {
                foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                    await f.SendAsync(uid, new MsgActionPacket { ObjId = uid, Type = MsgActionType.AllowAnimation }.ToFrameBytes());
                Assert.Equal(500U, f.Current().NativePlayer!.Mana); Assert.True(f.Current().NativePlayer!.Alive);
                f.ActorFrames.Clear(); f.Frames(TargetPlayerUid).Clear(); await body(f);
            }, Input(TargetPlayerUid));
    }

    internal static WorldSession StatusAdmissionInstallShield(WorldSession source, string expiry) => source with
    {
        Character = source.Character with
        {
            Attributes = new Dictionary<string, string>(source.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(0x1FF, LegacyMagicShieldFlag),
                ["LegacyMagicShieldExpiresAtUtc"] = expiry,
                ["LegacyMagicShieldRemoveOnDead"] = bool.FalseString,
            },
        },
    };
}

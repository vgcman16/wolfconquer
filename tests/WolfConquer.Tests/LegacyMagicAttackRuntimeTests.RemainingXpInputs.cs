using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    internal static WorldSession RemainingXpWithEligibility(WorldSession original, bool ready)
    {
        var attributes = new Dictionary<string, string>(original.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        if (ready) attributes["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag);
        else attributes.Remove("StatusFlags");
        return original with { Character = original.Character with { Attributes = attributes } };
    }

    // Complete saved test inputs; real fixture bootstrap/admission owns all
    // registry, native, PIN, persistence and transport authority.
    internal static Task RemainingXpRun(ushort id, string? stamina, bool xpList,
        Func<OwnedEquipmentBehaviorFixture, Task> body, byte staminaCost = 5,
        int classCode = 142, long map = 1000, bool blessed = false)
    {
        var sort = id switch { 12860 => (byte)85, 15780 => (byte)94, _ => (byte)6 };
        var row = CreateMagicRow(id, sort: sort, useMana: 7, useStamina: staminaCost,
            damage: 100, duration: 60, range: 18, coolDown: 0, coldTime: 1500);
        var head = OwnedEquipmentBehaviorFixture.Item(111003, "RemainingXpHead",
            new Dictionary<int, string> { [18] = "5000", [19] = "5000" });
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], [row], items: [head]);
        OwnedEquipmentBehaviorInput Input(uint uid)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { ["PkMode"] = "0", ["XPCount"] = "55" };
            if (uid == PlayerUid)
            {
                if (stamina is not null) attributes["Stamina"] = stamina;
                if (xpList) attributes["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag);
                if (blessed) attributes["HeavenBlessing"] = "1";
            }
            else attributes["Stamina"] = "100";
            var items = new PlayerItemSnapshot([], [], [], new[] { new PlayerItemSnapshotItem
            {
                Uid = uid + 70_000, ItemId = 111003, Position = 1,
                Durability = 100, MaximumDurability = 100, StackSize = 1,
            } }, []);
            var source = CreateSession(uid: uid, name: uid == PlayerUid ? "RemainingXpActor" : "RemainingXpPeer",
                classCode: uid == PlayerUid ? classCode : 142, mapId: map, x: uid == PlayerUid ? 830 : 832,
                y: 584, minHitPoints: 1000, minMana: 500, strength: 300,
                spellIds: uid == PlayerUid ? new ushort[] { id } : Array.Empty<ushort>(),
                attributes: attributes, playerItems: items);
            return new(source.Character, items, source.PlayerSpells, source.PlayerProficiencies);
        }
        var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 29, 15, 0, 0, TimeSpan.Zero));
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(Input(PlayerUid), world,
            new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock), async f =>
            {
                foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                    await f.SendAsync(uid, new MsgActionPacket { ObjId = uid, Type = MsgActionType.AllowAnimation }.ToFrameBytes());
                f.Frames(PlayerUid).Clear(); f.Frames(TargetPlayerUid).Clear();
                await body(f);
            }, Input(TargetPlayerUid));
    }
}

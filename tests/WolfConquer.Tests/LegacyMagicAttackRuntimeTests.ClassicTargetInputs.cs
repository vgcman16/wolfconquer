using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using WolfConquer.Core.Packets;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    internal static async Task ClassicTargetWithoutMonsterOwnerAsync(OwnedEquipmentBehaviorFixture f, ushort id, uint target)
    {
        var current = f.Current();
        var context = f.Handler.CreateMonsterRuntimeContext(new(f.Connection(PlayerUid), current)) with { MonsterStates = null };
        var row = Assert.Single(current.PlayerSpells.Spells, row => row.Id == id);
        Assert.True(LegacyMagicRuntime.TryResolveNativeLoadedSpell(context.WorldData!, id, row.Level, out var spell, out _));
        var operations = Assert.IsType<WorldOperationBarrier>(typeof(WorldServerFrameHandler)
            .GetField("_worldOperations", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f.Handler));
        var method = typeof(WorldPacketHandlers).GetMethod("HandleLegacyPlayerAttachStatusMagicAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var query = new MsgAttackQuery(PlayerUid, target, 836, 584, MsgAttackType.Magic,
            0, 0, 0, MsgAttackEffect.None, 0, SpellId: id);
        using var shared = await operations.EnterSharedAsync();
        object? pending;
        try { pending = method.Invoke(null, [context, query, row, spell, CancellationToken.None]); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
        Assert.True(await Assert.IsType<ValueTask<bool>>(pending));
    }

    internal static Task ClassicTargetRunAsync(ushort id,
        Func<OwnedEquipmentBehaviorFixture, LegacyMonsterInstance, Task> exercise, bool hiddenPeer = false)
    {
        var family = CreateMonsterFamily(); var spawn = CreateMonsterSpawn();
        var rows = new[] { (byte)0, (byte)1 }.Select(level => CreateMagicRow(id, sort: 6,
            level: level, useMana: 7, useStamina: 4, duration: 45, experience: 1000, range: 18, coldTime: 1000)).ToArray();
        var head = OwnedEquipmentBehaviorFixture.Item(111003, "ClassicTargetHead",
            new Dictionary<int, string> { [18] = "5000", [19] = "5000" });
        var catalog = CreateMagicCatalogCore(family, [spawn], [], rows, items: [head]);
        var monster = CreateRuntimeMonsterInstance(catalog, spawn, family);
        OwnedEquipmentBehaviorInput Input(uint uid)
        {
            var items = new PlayerItemSnapshot([], [], [], new[]
            {
                new PlayerItemSnapshotItem { Uid = checked(uid + 70000), ItemId = 111003, Position = 1,
                    Durability = 100, MaximumDurability = 100, StackSize = 1 },
            }, []);
            var source = CreateSession(uid: uid, name: uid == PlayerUid ? "ClassicOwner" : "ClassicPeer",
                x: uid == PlayerUid ? 830 : hiddenPeer ? 900 : 832, mapId: 1015,
                minHitPoints: 1000, minMana: 500, stamina: 100, playerItems: items,
                spellEntries: uid == PlayerUid ? new[] { CreatePlayerSpell(id) } : Array.Empty<PlayerSpellSnapshotEntry>(),
                attributes: new Dictionary<string, string> { ["StatusFlags"] = BuildStatusFlags(0x1FF) });
            return new(source.Character, source.PlayerItems, source.PlayerSpells, source.PlayerProficiencies);
        }
        var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 29, 18, 0, 0, TimeSpan.Zero));
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(Input(PlayerUid), catalog,
            new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock), async f =>
            {
                foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                    await f.SendAsync(uid, new MsgActionPacket { ObjId = uid, Type = MsgActionType.AllowAnimation }.ToFrameBytes());
                Assert.True(f.Current().NativePlayer!.Alive); Assert.Equal(500U, f.Current().NativePlayer!.Mana);
                f.ActorFrames.Clear(); f.Frames(TargetPlayerUid).Clear(); await exercise(f, monster);
            }, Input(TargetPlayerUid));
    }
}

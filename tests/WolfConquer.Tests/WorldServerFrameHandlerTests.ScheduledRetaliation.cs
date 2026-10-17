using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private static async Task SendPlayerHitThenScheduledMonsterRetaliationAsync(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        IReadOnlyList<byte[]> sent,
        PacketFrame request)
    {
        Assert.True(MsgAttackPacket.TryParse(request, out var attack));
        Assert.Equal(MsgAttackType.Physical, attack.Query.AttackType);
        var requestStart = sent.Count;
        await handler.HandleFrameAsync(connection, request);

        Assert.DoesNotContain(sent.Skip(requestStart), bytes =>
            (MsgAttackPacket.TryParse(ReadFrame(bytes), out var response) &&
             response.Query.Uid == attack.Query.OpponentUid) ||
            (LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) &&
             animation.AttackerUid == attack.Query.OpponentUid));

        var session = GetSession(handler, connection);
        var context = handler.CreateMonsterRuntimeContext(new WorldActiveSession(connection, session));
        Assert.NotNull(context.WorldData);
        Assert.NotNull(context.MonsterStates);
        Assert.True(context.WorldData.MonsterSpawnsByMapId.TryGetValue(checked((int)session.Character.MapId), out var spawns));
        var entry = Assert.Single(
            LegacyStaticMonsterMaterializer.Enumerate(context.WorldData, spawns),
            candidate => candidate.Instance.Uid == attack.Query.OpponentUid);
        var now = DateTimeOffset.UtcNow;
        Assert.True(context.MonsterStates.TryGetVisibleInstance(entry.Instance, now, out var monster));

        var intervalMethod = typeof(WorldServerFrameHandler).GetMethod(
            "GetLegacyMonsterAttackInterval", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(intervalMethod);
        var attackInterval = Assert.IsType<TimeSpan>(intervalMethod.Invoke(null, [entry.MonsterFamily]));
        var attackRange = checked((int)LegacyMonsterFamilyRuntimeRules.ResolveAttackRange(entry.MonsterFamily));
        var viewRange = entry.MonsterFamily.Attributes.TryGetValue("view_range", out _)
            ? checked((int)ReadTestAttributeUInt32(entry.MonsterFamily.Attributes, "view_range"))
            : 18;
        var alive = session.Character.MinHitPoints > 0 &&
            !LegacyPlayerDeathState.HasStatusFlag(session.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag) &&
            !LegacyPlayerDeathState.HasStatusFlag(session.Character.Attributes, LegacyPlayerDeathState.GhostStatusFlag);
        var target = new LegacyMonsterTargetSnapshot(
            checked((uint)session.Character.Uid),
            checked((int)LegacyTestDistance(session.Character.X, session.Character.Y, monster.X, monster.Y)),
            alive,
            Flying: LegacyPlayerDeathState.HasStatusFlag(session.Character.Attributes, 0x1B),
            SocketAlive: true);

        // Run the production per-monster due decision/action without advancing unrelated seed spawns.
        var due = context.MonsterStates.AdvanceTargetLock(
            monster, target, LegacyMonsterTargetLockLifecycle.Alive,
            attackRange, Math.Max(attackRange, viewRange), now, attackInterval);
        Assert.True(due.AttackDue, $"Expected selected monster {monster.Uid} to be due for its first scheduled attack.");
        await WorldPacketHandlers.TrySendLegacyMonsterRetaliationAsync(
            context, session, monster, entry.MonsterFamily, CancellationToken.None);
    }
}

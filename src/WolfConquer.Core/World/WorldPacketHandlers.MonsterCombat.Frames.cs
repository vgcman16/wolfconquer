using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask SendMonsterCombatFrameToVisibleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        MsgAttackPacket packet,
        CancellationToken cancellationToken)
    {
        var frame = packet.ToFrameBytes();
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (context.Sessions is not { } sessions)
        {
            return;
        }

        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, context.Connection) ||
                active.Session.Character.Uid == session.Character.Uid ||
                !CanSeeMonster(active.Session.Character, monster))
            {
                continue;
            }

            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask SendMonsterAttackFrameToVisibleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        MsgAttackPacket packet,
        CancellationToken cancellationToken)
    {
        var frame = packet.ToFrameBytes();
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (context.Sessions is not { } sessions)
        {
            return;
        }

        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, context.Connection) ||
                active.Session.Character.Uid == session.Character.Uid ||
                !CanSeeMonster(active.Session.Character, monster))
            {
                continue;
            }

            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask SendMonsterSpellAnimationToVisibleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        LegacySpellAnimationPacket packet,
        CancellationToken cancellationToken)
    {
        var frames = packet.ToFrames();
        foreach (var frame in frames)
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        if (context.Sessions is not { } sessions)
        {
            return;
        }

        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, context.Connection) ||
                active.Session.Character.Uid == session.Character.Uid ||
                !CanSeeMonster(active.Session.Character, monster))
            {
                continue;
            }

            foreach (var frame in frames)
            {
                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask SendMonsterStatusFlagVectorToVisibleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        IReadOnlyList<ulong> statusFlags,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgUpdatePacket
            .CreateLegacyRuntimeStatusFlagVectorUpdate(monster.Uid, statusFlags)
            .ToFrameBytes();
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (context.Sessions is not { } sessions)
        {
            return;
        }

        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, context.Connection) ||
                active.Session.Character.Uid == session.Character.Uid ||
                !CanSeeMonster(active.Session.Character, monster))
            {
                continue;
            }

            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask SendPlayerSpellAnimationToVisibleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacySpellAnimationPacket packet,
        CancellationToken cancellationToken)
    {
        var frames = packet.ToFrames();
        foreach (var frame in frames)
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        if (context.Sessions is not { } sessions)
        {
            return;
        }

        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, context.Connection) ||
                active.Session.Character.Uid == session.Character.Uid ||
                !CanSeePlayer(active.Session.Character, session.Character))
            {
                continue;
            }

            foreach (var frame in frames)
            {
                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask SendPlayerStatusFlagVectorToVisibleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        await SendPlayerStatusFlagVectorToVisibleAsync(
                context,
                context.Connection,
                session,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendPlayerStatusFlagVectorToVisibleAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection subjectConnection,
        WorldSession subjectSession,
        CancellationToken cancellationToken)
    {
        var packet = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
            CheckedUInt32(subjectSession.Character.Uid, nameof(subjectSession.Character.Uid)),
            CreateLegacyRuntimeStatusFlagVector(subjectSession.Character));
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                subjectConnection,
                subjectSession,
                packet.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }
}

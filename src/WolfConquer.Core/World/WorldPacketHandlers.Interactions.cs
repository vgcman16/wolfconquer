using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleInteractionAttackStateAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            return;
        }

        var query = packet.Query;
        switch (query.AttackType)
        {
            case MsgAttackType.InteractionRequest:
                await HandleInteractionRequestAsync(context, session, packet, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case MsgAttackType.InteractionAccept:
                await HandleInteractionAcceptAsync(context, session, packet, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case MsgAttackType.InteractionRefuse:
                await HandleInteractionRefuseAsync(context, session, packet, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case MsgAttackType.InteractionStopEffect:
                await HandleInteractionStopEffectAsync(context, session, packet, cancellationToken)
                    .ConfigureAwait(false);
                break;
        }
    }

    private static async ValueTask HandleInteractionRequestAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackPacket packet,
        CancellationToken cancellationToken)
    {
        if (!TryCommitInteractionInvitation(context, session, packet, out var committed) || committed is null) return;
        if (!committed.IsCurrent()) return;
        await committed.PartnerConnection.SendAsync(committed.Frame, cancellationToken).ConfigureAwait(false);
        if (!committed.IsCurrent()) return;
        await committed.PartnerConnection.SendAsync(committed.Frame, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer requested MsgAttack interaction " +
                $"{FormatSession(context)} opponentUid={packet.Query.OpponentUid} activeDance={committed.Dance}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleInteractionAcceptAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackPacket packet,
        CancellationToken cancellationToken)
    {
        if (!TryCommitInteractionInvitation(context, session, packet, out var committed) || committed is null) return;
        if (!committed.IsCurrent()) return;
        await committed.PartnerConnection.SendAsync(committed.Frame, cancellationToken).ConfigureAwait(false);
        if (!committed.IsCurrent()) return;
        await committed.OwnerConnection.SendAsync(committed.Frame, cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer paired MsgAttack interaction " +
                $"{FormatSession(context)} opponentUid={committed.Partner.Character.Uid} activeDance={committed.Dance}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleInteractionRefuseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackPacket packet,
        CancellationToken cancellationToken)
    {
        if (!TryCommitInteractionInvitation(context, session, packet, out var committed) || committed is null || !committed.IsCurrent()) return;
        await committed.PartnerConnection.SendAsync(committed.Frame, cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer refused MsgAttack interaction " +
                $"{FormatSession(context)} opponentUid={packet.Query.OpponentUid}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleInteractionStopEffectAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackPacket packet,
        CancellationToken cancellationToken)
    {
        if (!TryCommitInteractionInvitation(context, session, packet, out var committed) || committed is null) return;
        if (!await SendCommittedInteractionToVisibleAsync(context, committed, committed.Frame, cancellationToken).ConfigureAwait(false)) return;
        if (!await SendCommittedInteractionToVisibleAsync(context, committed, committed.Swapped, cancellationToken).ConfigureAwait(false)) return;
        await context.Log.WriteLineAsync(
                "WorldServer stopped MsgAttack interaction " +
                $"{FormatSession(context)} opponentUid={packet.Query.OpponentUid}.")
            .ConfigureAwait(false);
    }

    private static bool CanBeginInteractionInvitation(WorldPacketHandlerContext context, MsgAttackPacket packet)
    {
        if (context.Session is not { } session || context.Sessions is not { } sessions ||
            !TryGetInteractionInvitationPartner(context, session, packet, out var partner)) return false;
        return sessions.CanBeginInteractionInvitation(context, partner, packet);
    }

    private static bool TryCommitInteractionInvitation(WorldPacketHandlerContext context, WorldSession session,
        MsgAttackPacket packet, out WorldSessionStore.InteractionInvitationReceipt? committed)
    {
        committed = null;
        return context.Sessions is { } sessions && ReferenceEquals(context.Session, session) &&
            TryGetInteractionInvitationPartner(context, session, packet, out var partner) &&
            sessions.TryCommitInteractionLinkPublication(context, partner, packet, out committed);
    }

    private static bool TryGetInteractionInvitationPartner(WorldPacketHandlerContext context, WorldSession session,
        MsgAttackPacket packet, out WorldActiveSession partner) => packet.Query.AttackType == MsgAttackType.InteractionStopEffect
            ? TryGetSessionByUid(context, packet.Query.OpponentUid, out partner)
            : TryGetVisibleSessionByUid(context, session, packet.Query.OpponentUid, out partner);

    internal static bool AreInteractionInvitationParticipantsVisible(WorldSession actor, WorldSession partner) =>
        CanSeePlayer(actor.Character, partner.Character);

    private static async ValueTask<bool> SendCommittedInteractionToVisibleAsync(WorldPacketHandlerContext context,
        WorldSessionStore.InteractionInvitationReceipt committed, ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        if (!committed.IsCurrent()) return false;
        await committed.OwnerConnection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (context.Sessions is not { } sessions) return false;
        foreach (var active in sessions.Snapshot())
        {
            if (!committed.IsCurrent()) return false;
            if (ReferenceEquals(active.Connection, committed.OwnerConnection) ||
                active.Session.Character.Uid == committed.Owner.Character.Uid ||
                !CanSeePlayer(active.Session.Character, committed.Owner.Character)) continue;
            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        return committed.IsCurrent();
    }

    private static async ValueTask SendInteractionToVisibleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
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
                !CanSeePlayer(active.Session.Character, session.Character))
            {
                continue;
            }

            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask SendFrameToVisiblePlayersAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte[] frame,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
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

            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask SendFrameToPlayerAndVisiblePeersAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection subjectConnection,
        WorldSession subjectSession,
        byte[] frame,
        CancellationToken cancellationToken)
    {
        await SendLegacyTeleportFrameAsync(context, subjectConnection, frame, cancellationToken).ConfigureAwait(false);
        if (context.Sessions is not { } sessions)
        {
            return;
        }

        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, subjectConnection) ||
                active.Session.Character.Uid == subjectSession.Character.Uid ||
                !CanSeePlayer(active.Session.Character, subjectSession.Character))
            {
                continue;
            }

            await SendLegacyTeleportFrameAsync(context, active.Connection, frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask HandleInteractionMovementAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldActiveSession partner,
        CharacterSnapshot character,
        MsgMovementPacket packet,
        uint angle,
        bool removedMagicDefenderFlag,
        bool removedFocusedFlag,
        bool removedMovementFlags,
        bool clearedAway,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            return;
        }

        var interactionWalk = LegacyInterActionWalkPacket.ForWalk(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            CheckedUInt32(partner.Session.Character.Uid, nameof(partner.Session.Character.Uid)), (byte)angle);
        var frame = interactionWalk.ToFrameBytes();
        if (!sessions.TryCommitPairMovement(context, partner, LegacyPairMovementKind.Walk,
                CheckedUInt16(character.X, nameof(character.X)), CheckedUInt16(character.Y, nameof(character.Y)),
                checked((ushort)angle), out var committed) || committed is null || !committed.IsCurrent()) return;
        var updated = committed.Actor;
        var updatedPartner = committed.Partner;
        character = updated.Character;
        await SendMovementAzureShieldRemovalIfNeededAsync(context, session, removedMagicDefenderFlag, cancellationToken).ConfigureAwait(false);
        if (!committed.IsCurrent()) return;
        await SendMovementFocusedCancelIfNeededAsync(context, session, removedFocusedFlag, cancellationToken).ConfigureAwait(false);
        if (!committed.IsCurrent()) return;

        if (clearedAway)
        {
            await SendPlayerSpawnToVisiblePeersAsync(context, updated, cancellationToken)
                .ConfigureAwait(false);
            if (!committed.IsCurrent()) return;
            await context.Log.WriteLineAsync(
                    "WorldServer cleared MsgMovement Away " +
                    $"{FormatSession(context)}; legacy MsgMovement.Movement clears Player.Away and refreshes visible role spawn.")
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (!committed.IsCurrent()) return;

        var visiblePeerCount = 0;
        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, context.Connection) ||
                active.Session.Character.Uid == character.Uid ||
                !CanSeePlayer(active.Session.Character, character))
            {
                continue;
            }

            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!committed.IsCurrent()) return;
            visiblePeerCount++;
        }

        await SendVisibleStaticNpcObjectsAsync(context with { Session = updated }, character, cancellationToken).ConfigureAwait(false);
        if (!committed.IsCurrent()) return;
        await SendVisibleStaticNpcObjectsAsync(context with { Connection = partner.Connection, Session = updatedPartner },
            updatedPartner.Character, cancellationToken).ConfigureAwait(false);

        if (removedMovementFlags)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer cleared MsgMovement status flags " +
                    $"{FormatSession(context)} flags=0x15/0x1E/0x1F/0x80.")
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent MsgMovement interaction walk " +
                $"{FormatSession(context)} opponentUid={interactionWalk.OpponentUid} x={character.X} y={character.Y} angle={angle} playerCount={visiblePeerCount}.")
            .ConfigureAwait(false);
    }
}

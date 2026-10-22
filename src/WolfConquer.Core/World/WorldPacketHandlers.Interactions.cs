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
        if (session.Character.MapId == 5040 ||
            session.InteractionPartnerUid is not null ||
            !TryGetVisibleSessionByUid(context, session, packet.Query.OpponentUid, out var partner) ||
            partner.Session.InteractionPartnerUid is not null)
        {
            return;
        }

        var activeDance = packet.Query.ResponseDamage;
        var requester = session with { ActiveInteractionDance = activeDance };
        var requested = partner.Session with { ActiveInteractionDance = activeDance };
        context.Sessions?.Set(context.Connection, requester);
        context.Sessions?.Set(partner.Connection, requested);

        var frame = packet.ToFrameBytes();
        await partner.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        await partner.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer requested MsgAttack interaction " +
                $"{FormatSession(context)} opponentUid={packet.Query.OpponentUid} activeDance={activeDance}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleInteractionAcceptAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackPacket packet,
        CancellationToken cancellationToken)
    {
        if (session.Character.MapId == 5040 ||
            session.InteractionPartnerUid is not null ||
            !TryGetVisibleSessionByUid(context, session, packet.Query.OpponentUid, out var partner) ||
            partner.Session.InteractionPartnerUid is not null)
        {
            return;
        }

        var response = new MsgAttackPacket(packet.Query with { ResponseDamage = session.ActiveInteractionDance });
        var frame = response.ToFrameBytes();
        await partner.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);

        var sessionUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var partnerUid = CheckedUInt32(partner.Session.Character.Uid, nameof(partner.Session.Character.Uid));
        context.Sessions?.Set(context.Connection, session with { InteractionPartnerUid = partnerUid });
        context.Sessions?.Set(partner.Connection, partner.Session with { InteractionPartnerUid = sessionUid });

        await context.Log.WriteLineAsync(
                "WorldServer paired MsgAttack interaction " +
                $"{FormatSession(context)} opponentUid={partnerUid} activeDance={session.ActiveInteractionDance}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleInteractionRefuseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackPacket packet,
        CancellationToken cancellationToken)
    {
        if (session.Character.MapId == 5040 ||
            !TryGetVisibleSessionByUid(context, session, packet.Query.OpponentUid, out var partner))
        {
            return;
        }

        await partner.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        context.Sessions?.Set(
            context.Connection,
            session with
            {
                InteractionPartnerUid = null,
                ActiveInteractionDance = 0,
            });
        context.Sessions?.Set(
            partner.Connection,
            partner.Session with
            {
                InteractionPartnerUid = null,
                ActiveInteractionDance = 0,
            });

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
        if (session.Character.MapId == 5040)
        {
            return;
        }

        var sessionUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var response = new MsgAttackPacket(packet.Query with { ResponseDamage = session.ActiveInteractionDance });
        await SendInteractionToVisibleAsync(context, session, response, cancellationToken).ConfigureAwait(false);

        var swapped = new MsgAttackPacket(response.Query with
        {
            Uid = response.Query.OpponentUid,
            OpponentUid = sessionUid,
        });
        await SendInteractionToVisibleAsync(context, session, swapped, cancellationToken).ConfigureAwait(false);

        ClearInteractionPair(context, session);
        await context.Log.WriteLineAsync(
                "WorldServer stopped MsgAttack interaction " +
                $"{FormatSession(context)} opponentUid={packet.Query.OpponentUid}.")
            .ConfigureAwait(false);
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

    private static void ClearInteractionPair(WorldPacketHandlerContext context, WorldSession session)
    {
        if (context.Sessions is not { } sessions)
        {
            return;
        }

        var clearedSession = session with
        {
            InteractionPartnerUid = null,
            ActiveInteractionDance = 0,
        };
        sessions.Set(context.Connection, clearedSession);
        if (session.InteractionPartnerUid is { } partnerUid &&
            TryGetSessionByUid(context, partnerUid, out var partner))
        {
            sessions.Set(
                partner.Connection,
                partner.Session with
                {
                    InteractionPartnerUid = null,
                    ActiveInteractionDance = 0,
                });
        }
    }

    private static async ValueTask HandleInteractionMovementAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldActiveSession partner,
        CharacterSnapshot character,
        MsgMovementPacket packet,
        uint angle,
        bool removedMovementFlags,
        bool clearedAway,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            return;
        }

        var partnerCharacter = partner.Session.Character with
        {
            X = character.X,
            Y = character.Y,
            Attributes = WithCharacterAttribute(partner.Session.Character, "Angle", angle).Attributes,
        };
        var updated = session with { Character = character };
        var updatedPartner = partner.Session with { Character = partnerCharacter };
        sessions.Set(context.Connection, updated);
        sessions.Set(partner.Connection, updatedPartner);

        if (clearedAway)
        {
            await SendPlayerSpawnToVisiblePeersAsync(context, updated, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer cleared MsgMovement Away " +
                    $"{FormatSession(context)}; legacy MsgMovement.Movement clears Player.Away and refreshes visible role spawn.")
                .ConfigureAwait(false);
        }

        var interactionWalk = LegacyInterActionWalkPacket.ForWalk(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            CheckedUInt32(partner.Session.Character.Uid, nameof(partner.Session.Character.Uid)),
            (byte)angle);
        var frame = interactionWalk.ToFrameBytes();
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);

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
            visiblePeerCount++;
        }

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

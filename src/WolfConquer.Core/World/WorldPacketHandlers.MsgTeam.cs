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
    [PacketHandler(WorldPacketOpcodes.MsgTeam)]
    private static async ValueTask HandleMsgTeamAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!MsgTeamPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed MsgTeam packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                $"WorldServer handled MsgTeam {FormatSession(context)} type={packet.Type} uid={packet.Uid} from {context.Connection.RemoteEndPoint}.")
            .ConfigureAwait(false);

        if (context.Session is not { } session)
        {
            return;
        }

        await HandleMsgTeamStateAsync(context, packet, session, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask HandleMsgTeamStateAsync(
        WorldPacketHandlerContext context,
        MsgTeamPacket packet,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var teamStates = context.TeamStates ?? new TeamSessionStateStore();
        switch (packet.Type)
        {
            case MsgTeamType.Create:
                if (session.Character.MapId == 700)
                {
                    await context.Log.WriteLineAsync(
                            $"WorldServer skipped MsgTeam Create {FormatSession(context)}; map=700 is blocked.")
                        .ConfigureAwait(false);
                    return;
                }

                teamStates.CreateTeam(context.Connection, session.Character.Uid);
                CaptureLegacyTeamMemberInfo(context, teamStates, session, context.Connection);
                await SendTeamCreateMembershipAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);
                await SendMsgTeamPacketAsync(context, MsgTeamType.Send, packet.Uid, cancellationToken)
                    .ConfigureAwait(false);
                await SendMsgTeamPacketAsync(context, MsgTeamType.Create, packet.Uid, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MsgTeamType.Dismiss:
                if (teamStates.HasTeam(context.Connection))
                {
                    var removedUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
                    var wasLeader = teamStates.IsLeader(context.Connection, session.Character.Uid);
                    var connections = teamStates.GetConnections(context.Connection);
                    await BroadcastMsgTeamPacketAsync(context, connections, MsgTeamType.Kick, removedUid, cancellationToken)
                        .ConfigureAwait(false);
                    teamStates.ExitTeam(context.Connection);
                    var remainingConnection = connections.FirstOrDefault(connection => !ReferenceEquals(connection, context.Connection));
                    await SendTeamRemovalCleanupAsync(
                            context,
                            context.Connection,
                            removedUid,
                            remainingConnection is null ? 0 : checked((int)teamStates.GetMemberCount(remainingConnection)),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (wasLeader && remainingConnection is not null)
                    {
                        await SendTeamLeaderHandoffFramesAsync(context, remainingConnection, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                break;

            case MsgTeamType.ExitTeam:
                if (teamStates.HasTeam(context.Connection))
                {
                    var removedUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
                    var wasLeader = teamStates.IsLeader(context.Connection, session.Character.Uid);
                    var connections = teamStates.GetConnections(context.Connection);
                    await BroadcastMsgTeamPacketAsync(context, connections, MsgTeamType.ExitTeam, removedUid, cancellationToken)
                        .ConfigureAwait(false);
                    teamStates.ExitTeam(context.Connection);
                    var remainingConnection = connections.FirstOrDefault(connection => !ReferenceEquals(connection, context.Connection));
                    await SendTeamRemovalCleanupAsync(
                            context,
                            context.Connection,
                            removedUid,
                            remainingConnection is null ? 0 : checked((int)teamStates.GetMemberCount(remainingConnection)),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (wasLeader && remainingConnection is not null)
                    {
                        await SendTeamLeaderHandoffFramesAsync(context, remainingConnection, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                break;

            case MsgTeamType.Kick:
                await HandleMsgTeamKickAsync(context, packet, session, teamStates, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MsgTeamType.InviteRequest:
                await HandleMsgTeamInviteRequestAsync(context, packet, session, teamStates, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MsgTeamType.JoinRequest:
                await HandleMsgTeamJoinRequestAsync(context, packet, session, teamStates, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MsgTeamType.AcceptInvitation:
                await HandleMsgTeamAcceptInvitationAsync(context, packet, session, teamStates, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MsgTeamType.AcceptJoinRequest:
                await HandleMsgTeamAcceptJoinRequestAsync(context, packet, session, teamStates, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MsgTeamType.ForbidJoining:
                await SetTeamOptionAsync(
                        context,
                        teamStates,
                        packet,
                        state => state.SetForbidJoin(context.Connection, true),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MsgTeamType.UnforbidJoining:
                await SetTeamOptionAsync(
                        context,
                        teamStates,
                        packet,
                        state => state.SetForbidJoin(context.Connection, false),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MsgTeamType.LootMoneyOn:
                await SetTeamOptionAsync(
                        context,
                        teamStates,
                        packet,
                        state => state.SetPickupMoney(context.Connection, true),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MsgTeamType.LootMoneyOff:
                await SetTeamOptionAsync(
                        context,
                        teamStates,
                        packet,
                        state => state.SetPickupMoney(context.Connection, false),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MsgTeamType.LootItemsOn:
                await SetTeamOptionAsync(
                        context,
                        teamStates,
                        packet,
                        state => state.SetPickupItems(context.Connection, true),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MsgTeamType.LootItemsOff:
                await SetTeamOptionAsync(
                        context,
                        teamStates,
                        packet,
                        state => state.SetPickupItems(context.Connection, false),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
        }
    }

    private static async ValueTask HandleMsgTeamInviteRequestAsync(
        WorldPacketHandlerContext context,
        MsgTeamPacket packet,
        WorldSession session,
        TeamSessionStateStore teamStates,
        CancellationToken cancellationToken)
    {
        if (!teamStates.HasTeam(context.Connection) ||
            !teamStates.IsLeader(context.Connection, session.Character.Uid) ||
            teamStates.GetMemberCount(context.Connection) >= 5 ||
            !TryGetVisibleSessionByUid(context, session, packet.Uid, out var target) ||
            teamStates.HasTeam(target.Connection))
        {
            return;
        }

        var leaderUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        await target.Connection.SendAsync(
                CreateTeamRelationPacket(session.Character, target.Session.Character).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await target.Connection.SendAsync(new MsgTeamPacket(MsgTeamType.InviteRequest, leaderUid).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer forwarded MsgTeam InviteRequest {FormatSession(context)} targetUid={packet.Uid}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleMsgTeamKickAsync(
        WorldPacketHandlerContext context,
        MsgTeamPacket packet,
        WorldSession session,
        TeamSessionStateStore teamStates,
        CancellationToken cancellationToken)
    {
        if (!teamStates.IsLeader(context.Connection, session.Character.Uid) ||
            !TryGetTeamConnectionByUid(context, teamStates, context.Connection, packet.Uid, out var targetConnection) ||
            ReferenceEquals(targetConnection, context.Connection))
        {
            return;
        }

        var connections = teamStates.GetConnections(context.Connection);
        await BroadcastMsgTeamPacketAsync(context, connections, MsgTeamType.Kick, packet.Uid, cancellationToken)
            .ConfigureAwait(false);
        teamStates.ExitTeam(targetConnection);
        await SendTeamRemovalCleanupAsync(
                context,
                targetConnection,
                packet.Uid,
                checked((int)teamStates.GetMemberCount(context.Connection)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleMsgTeamJoinRequestAsync(
        WorldPacketHandlerContext context,
        MsgTeamPacket packet,
        WorldSession session,
        TeamSessionStateStore teamStates,
        CancellationToken cancellationToken)
    {
        if (teamStates.HasTeam(context.Connection) ||
            !TryGetVisibleSessionByUid(context, session, packet.Uid, out var leader) ||
            !teamStates.IsLeader(leader.Connection, leader.Session.Character.Uid) ||
            teamStates.IsFull(leader.Connection))
        {
            return;
        }

        var requesterUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        await leader.Connection.SendAsync(
                CreateTeamRelationPacket(session.Character, leader.Session.Character).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await leader.Connection.SendAsync(new MsgTeamPacket(MsgTeamType.JoinRequest, requesterUid).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer forwarded MsgTeam JoinRequest {FormatSession(context)} leaderUid={packet.Uid}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleMsgTeamAcceptInvitationAsync(
        WorldPacketHandlerContext context,
        MsgTeamPacket packet,
        WorldSession session,
        TeamSessionStateStore teamStates,
        CancellationToken cancellationToken)
    {
        if (teamStates.HasTeam(context.Connection) ||
            !TryGetVisibleSessionByUid(context, session, packet.Uid, out var leader) ||
            teamStates.GetForbidJoin(leader.Connection) ||
            !teamStates.TryAddMember(leader.Connection, context.Connection, session.Character.Uid))
        {
            return;
        }

        CaptureLegacyTeamMemberInfo(context, teamStates, session, context.Connection);
        await SendTeamAddFramesAsync(context, session, context.Connection, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleMsgTeamAcceptJoinRequestAsync(
        WorldPacketHandlerContext context,
        MsgTeamPacket packet,
        WorldSession session,
        TeamSessionStateStore teamStates,
        CancellationToken cancellationToken)
    {
        if (!teamStates.IsLeader(context.Connection, session.Character.Uid) ||
            teamStates.GetForbidJoin(context.Connection) ||
            teamStates.IsFull(context.Connection) ||
            !TryGetVisibleSessionByUid(context, session, packet.Uid, out var member) ||
            !teamStates.TryAddMember(context.Connection, member.Connection, member.Session.Character.Uid))
        {
            return;
        }

        CaptureLegacyTeamMemberInfo(context, teamStates, member.Session, member.Connection);
        await SendTeamAddFramesAsync(context, member.Session, member.Connection, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SetTeamOptionAsync(
        WorldPacketHandlerContext context,
        TeamSessionStateStore teamStates,
        MsgTeamPacket packet,
        Action<TeamSessionStateStore> update,
        CancellationToken cancellationToken)
    {
        if (!teamStates.HasTeam(context.Connection))
        {
            return;
        }

        update(teamStates);
        await SendMsgTeamPacketAsync(context, packet.Type, packet.Uid, cancellationToken)
            .ConfigureAwait(false);
    }
}

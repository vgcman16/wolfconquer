using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask SendTeamAddFramesAsync(
        WorldPacketHandlerContext context,
        WorldSession joined,
        ConquerTcpConnection joinedConnection,
        CancellationToken cancellationToken)
    {
        var teamStates = context.TeamStates ?? new TeamSessionStateStore();
        var memberUid = CheckedUInt32(joined.Character.Uid, nameof(joined.Character.Uid));
        var leaderUid = CheckedUInt32(
            teamStates.GetLeaderUid(joinedConnection) ?? 0,
            "LeaderUid");
        var memberCount = checked((int)teamStates.GetMemberCount(joinedConnection));
        var teamUid = teamStates.GetTeamUid(joinedConnection);

        await joinedConnection.SendAsync(
                new LegacyTeamLeadershipPacket(
                    LegacyTeamLeadershipMode.Teammate,
                    Uid: 0,
                    LeaderUid: 0,
                    Count: 0,
                    Unknown: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await joinedConnection.SendAsync(
                new LegacyTeamLeadershipPacket(
                    LegacyTeamLeadershipMode.Leader,
                    memberUid,
                    leaderUid,
                    memberCount,
                    Unknown: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await joinedConnection.SendAsync(
                new LegacyTeamLeadershipPacket(
                    LegacyTeamLeadershipMode.Leader,
                    leaderUid,
                    leaderUid,
                    memberCount,
                    Unknown: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await joinedConnection.SendAsync(
                new LegacyMsgUpdatePacket(
                    memberUid,
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Team, teamUid)]).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var teamMemberInfo = CreateTeamMemberInfoFromState(context, joinedConnection, memberUid);
        foreach (var recipient in teamStates.SnapshotLegacyMemberRecipients(joinedConnection))
        {
            if (context.Sessions is { } sessions &&
                (!sessions.TryGet(recipient.Connection, out var currentRecipient) || currentRecipient is null ||
                 currentRecipient.Character.Uid != recipient.Uid))
            {
                continue;
            }

            await recipient.Connection.SendAsync(teamMemberInfo.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                $"WorldServer sent MsgTeam add frames {FormatSession(context)} memberUid={memberUid} leaderUid={leaderUid} count={memberCount}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendMsgTeamPacketAsync(
        WorldPacketHandlerContext context,
        MsgTeamType type,
        uint uid,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(new MsgTeamPacket(type, uid).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer sent MsgTeam {FormatSession(context)} type={type} uid={uid}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask BroadcastMsgTeamPacketAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<ConquerTcpConnection> connections,
        MsgTeamType type,
        uint uid,
        CancellationToken cancellationToken)
    {
        var frame = new MsgTeamPacket(type, uid).ToFrameBytes();
        foreach (var connection in connections)
        {
            await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                $"WorldServer broadcast MsgTeam {FormatSession(context)} type={type} uid={uid} count={connections.Count}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendTeamRemovalCleanupAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        uint removedUid,
        int remainingCount,
        CancellationToken cancellationToken)
    {
        await connection.SendAsync(
                new LegacyTeamLeadershipPacket(
                    LegacyTeamLeadershipMode.Leader,
                    removedUid,
                    LeaderUid: 0,
                    remainingCount,
                    Unknown: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                new LegacyMsgUpdatePacket(
                    removedUid,
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Team, 0)]).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer sent MsgTeam removal cleanup {FormatSession(context)} uid={removedUid} remaining={remainingCount}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendTeamLeaderHandoffFramesAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection teamConnection,
        CancellationToken cancellationToken)
    {
        var teamStates = context.TeamStates ?? new TeamSessionStateStore();
        var leaderUid = CheckedUInt32(teamStates.GetLeaderUid(teamConnection) ?? 0, "LeaderUid");
        var memberCount = checked((int)teamStates.GetMemberCount(teamConnection));
        if (leaderUid == 0 || memberCount == 0)
        {
            return;
        }

        var memberInfo = CreateTeamMemberInfoFromState(context, teamConnection).ToFrameBytes();
        var leadership = new LegacyTeamLeadershipPacket(
            LegacyTeamLeadershipMode.Leader,
            leaderUid,
            leaderUid,
            memberCount,
            Unknown: 0).ToFrameBytes();

        var connections = teamStates.GetConnections(teamConnection);
        foreach (var connection in connections)
        {
            await connection.SendAsync(memberInfo, cancellationToken).ConfigureAwait(false);
            await connection.SendAsync(leadership, cancellationToken).ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                $"WorldServer sent MsgTeam leader handoff frames {FormatSession(context)} leaderUid={leaderUid} count={memberCount}.")
            .ConfigureAwait(false);
    }

    private static bool TryGetTeamConnectionByUid(
        WorldPacketHandlerContext context,
        TeamSessionStateStore teamStates,
        ConquerTcpConnection teamConnection,
        uint uid,
        out ConquerTcpConnection connection)
    {
        foreach (var candidate in teamStates.GetConnections(teamConnection))
        {
            if (context.Sessions?.TryGet(candidate, out var session) == true &&
                session?.Character.Uid == uid)
            {
                connection = candidate;
                return true;
            }
        }

        connection = null!;
        return false;
    }

    private static async ValueTask SendTeamCreateMembershipAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var ownerUid = session.Character.Uid;
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, ownerUid, ref session))
        {
            return;
        }

        var characterUid = CheckedUInt32(ownerUid, nameof(ownerUid));
        // Real Create/Join captures MemberInfo before its first publication. An
        // uncaptured manually seeded membership keeps only its legacy live view;
        // it does not manufacture or initialize historical constructor state.
        var entry = context.TeamStates?.TryGetLegacyMemberInfo(context.Connection, ownerUid, out var captured) == true
            ? captured!
            : CreateTeamMemberInfoEntry(session.Character);
        var memberInfo = new LegacyTeamMemberInfoPacket(
            LegacyTeamMemberInfoAction.AddMember,
            Count: 1,
            Unknown: 1,
            [entry]);
        await context.Connection.SendAsync(memberInfo.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, ownerUid, ref session))
        {
            return;
        }

        var leadership = new LegacyTeamLeadershipPacket(
            LegacyTeamLeadershipMode.Leader,
            Uid: characterUid,
            LeaderUid: characterUid,
            Count: 1,
            Unknown: 0);
        await context.Connection.SendAsync(leadership.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer sent legacy team create membership frames {FormatSession(context)} uid={characterUid}.")
            .ConfigureAwait(false);
    }

    private static LegacyTeamMemberInfoPacket CreateTeamMemberInfoFromState(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        long? constructorMemberUid = null)
    {
        var teamStates = context.TeamStates ?? new TeamSessionStateStore();
        var entries = new List<LegacyTeamMemberInfoEntry>();
        foreach (var member in teamStates.SnapshotLegacyMemberRecipients(connection))
        {
            WorldSession? currentMember = null;
            if (context.Sessions is { } sessions &&
                (!sessions.TryGet(member.Connection, out currentMember) || currentMember is null ||
                 currentMember.Character.Uid != member.Uid))
            {
                continue;
            }

            if (member.Uid == constructorMemberUid &&
                teamStates.TryGetLegacyMemberInfo(member.Connection, member.Uid, out var captured))
            {
                entries.Add(captured!);
            }
            else if (currentMember is not null)
            {
                // Existing members retain the prior fresh presentation until all
                // source HP setters maintain their stored member fields. This is
                // not a synthesized historical cache, and never adopts a rebound.
                entries.Add(CreateTeamMemberInfoEntry(currentMember.Character));
            }
        }

        return new LegacyTeamMemberInfoPacket(
            LegacyTeamMemberInfoAction.AddMember,
            checked((byte)entries.Count),
            Unknown: 1,
            entries);
    }

    private static LegacyTeamMemberInfoEntry CreateTeamMemberInfoEntry(CharacterSnapshot character)
    {
        return new LegacyTeamMemberInfoEntry(
            character.Name,
            CheckedUInt32(character.Uid, nameof(character.Uid)),
            BuildLegacyCharacterMesh(character),
            GetTeamMemberHitpoints(character, useMaximum: true),
            GetTeamMemberHitpoints(character, useMaximum: false));
    }

    private static LegacyRelationPacket CreateTeamRelationPacket(
        CharacterSnapshot requester,
        CharacterSnapshot receiver)
    {
        return LegacyRelationPacket.CreateTeamRequest(
            CheckedUInt32(requester.Uid, nameof(requester.Uid)),
            CheckedUInt32(receiver.Uid, nameof(receiver.Uid)),
            CheckedUInt32(requester.Level, nameof(requester.Level)),
            CheckedUInt32(
                checked((long)ReadAttributeUInt64(requester.Attributes, "BattlePower", "BattlePowerValue")),
                "BattlePower"),
            receiver.Name);
    }

    private static uint GetTeamMemberHitpoints(CharacterSnapshot character, bool useMaximum)
    {
        var hitpoints = useMaximum
            ? Math.Max(
                ReadAttributeUInt64(character.Attributes, "MaxHitpoints", "MaxHitPoints", "MaxLife"),
                checked((ulong)character.MinHitPoints))
            : checked((ulong)character.MinHitPoints);

        return hitpoints > ushort.MaxValue ? ushort.MaxValue : (uint)hitpoints;
    }

    private static bool TryGetTeamLeaderSession(
        WorldPacketHandlerContext context,
        ConquerTcpConnection teamConnection,
        out WorldActiveSession leader)
    {
        var teamStates = context.TeamStates ?? new TeamSessionStateStore();
        var leaderUid = teamStates.GetLeaderUid(teamConnection);
        if (leaderUid is null)
        {
            leader = default!;
            return false;
        }

        return TryGetSessionByUid(context, CheckedUInt32(leaderUid.Value, "LeaderUid"), out leader);
    }
}

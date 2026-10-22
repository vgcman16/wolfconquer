using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed record LegacyNativeTeamMembershipEntry(
    LegacyTournamentWatcherClient Actor, LegacyTournamentWatcherClient? Peer,
    bool RequireLeader,
    LegacyTournamentTeam? Team = null,
    IReadOnlyList<LegacyTournamentWatcherClient>? OriginalRecipients = null);

public static partial class WorldPacketHandlers
{
    private static bool TryEnterNativeTeamMembership(
        WorldPacketHandlerContext context, MsgTeamPacket packet,
        out WorldPacketHandlerContext entered)
    {
        entered = context;
        if (packet.Type is not (MsgTeamType.Create or MsgTeamType.AcceptInvitation or MsgTeamType.AcceptJoinRequest))
            return true;
        if (context.TournamentWatcherHolders is not { } holders)
            return context.TeamStates?.NativeClientAuthority is null; // Existing unbound legacy fixtures only.
        if (WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
            !ReferenceEquals(context.TeamStates?.NativeClientAuthority, holders) ||
            context.Session is not { } session || context.Sessions is not { } world ||
            !holders.TryCaptureClient(context.Connection, out var actor) || actor is null ||
            actor.Uid != session.Character.Uid ||
            !LegacyNativeClientPublication.TryCurrent(world, actor.Connection, actor.Uid, actor.Identity, out session))
            return false;
        if (session.Character.MapId == 700 || !LegacyRealmTransferServicesIsOnOwnServer(session) ||
            (packet.Type != MsgTeamType.Create && !IsLegacyTeleportCharacterAlive(session.Character)))
            return false;
        LegacyTournamentWatcherClient? peer = null;
        if (packet.Type != MsgTeamType.Create)
        {
            // Native GamePoll has one live client per UID. Never choose a rebound/duplicate fallback.
            if (world.Snapshot().Count(active => active.Session.Character.Uid == packet.Uid) != 1 ||
                !TryGetVisibleSessionByUid(context, session, packet.Uid, out var selected) ||
                !holders.TryCaptureClient(selected.Connection, out peer) || peer is null ||
                peer.Uid != packet.Uid)
                return false;
        }
        entered = context with
        {
            Session = session,
            NativeTeamMembershipEntry = new(actor, peer, packet.Type == MsgTeamType.AcceptJoinRequest),
        };
        return true;
    }

    private static bool IsNativeTeamAdmissionCurrent(
        WorldPacketHandlerContext context, MsgTeamPacket packet, WorldSession session) =>
        context.NativeTeamMembershipEntry is null ||
        (session.Character.MapId != 700 && LegacyRealmTransferServicesIsOnOwnServer(session) &&
         (packet.Type == MsgTeamType.Create || IsLegacyTeleportCharacterAlive(session.Character)));

    private static bool TryRefreshNativeTeamMembership(
        WorldPacketHandlerContext context, out WorldSession session)
    {
        session = context.Session!;
        if (context.NativeTeamMembershipEntry is not { } entry)
            return session is not null;
        if (context.Sessions is not { } world ||
            !LegacyNativeClientPublication.TryCurrent(world, entry.Actor.Connection,
                entry.Actor.Uid, entry.Actor.Identity, out session))
            return false;
        return entry.Peer is null || LegacyNativeClientPublication.TryCurrent(world,
            entry.Peer.Connection, entry.Peer.Uid, entry.Peer.Identity, out _);
    }

    private static LegacyTournamentWatcherClient? NativeTeamEntryClient(
        WorldPacketHandlerContext context, ConquerTcpConnection connection)
    {
        if (context.NativeTeamMembershipEntry is not { } entry) return null;
        if (ReferenceEquals(entry.Actor.Connection, connection)) return entry.Actor;
        return ReferenceEquals(entry.Peer?.Connection, connection) ? entry.Peer : null;
    }

    private static bool HasTeamAtNativeMembershipEntry(
        WorldPacketHandlerContext context, TeamSessionStateStore teams, ConquerTcpConnection connection) =>
        context.NativeTeamMembershipEntry is null
            ? teams.HasTeam(connection)
            : NativeTeamEntryClient(context, connection) is { } member && teams.HasNativeTeam(member);

    private static bool CreateTeamAtNativeMembershipEntry(
        WorldPacketHandlerContext context, TeamSessionStateStore teams, WorldSession session)
    {
        if (context.NativeTeamMembershipEntry is { } entry)
            return teams.TryCreateNativeTeam(entry.Actor, session.Character.Name);
        teams.CreateTeam(context.Connection, session.Character.Uid);
        return true;
    }

    private static bool AddTeamAtNativeMembershipEntry(
        WorldPacketHandlerContext context, TeamSessionStateStore teams,
        ConquerTcpConnection leaderConnection, ConquerTcpConnection memberConnection, long memberUid)
    {
        if (context.NativeTeamMembershipEntry is null)
            return teams.TryAddMember(leaderConnection, memberConnection, memberUid);
        return NativeTeamEntryClient(context, leaderConnection) is { } leader &&
            NativeTeamEntryClient(context, memberConnection) is { } member &&
            member.Uid == memberUid && teams.TryAddNativeMember(leader, member,
                context.NativeTeamMembershipEntry.RequireLeader);
    }

    private static WorldPacketHandlerContext CaptureNativeTeamPublication(
        WorldPacketHandlerContext context, TeamSessionStateStore teams, ConquerTcpConnection joined)
    {
        if (context.NativeTeamMembershipEntry is not { } entry) return context;
        var client = NativeTeamEntryClient(context, joined);
        if (client is null || !teams.TryCaptureTournamentTeam(client, out var team) || team is null ||
            !teams.TryReadTournamentTeam(team, out var snapshot) || snapshot is null)
            throw new LegacyNativeTeamMembershipRetiredException();
        return context with
        {
            NativeTeamMembershipEntry = entry with { Team = team, OriginalRecipients = snapshot.Members },
        };
    }

    private static async ValueTask SendNativeTeamMembershipFrameAsync(
        WorldPacketHandlerContext context, ConquerTcpConnection recipient,
        ReadOnlyMemory<byte> frame, CancellationToken cancellationToken, long? expectedRecipientUid = null)
    {
        if (context.NativeTeamMembershipEntry is not { } entry)
        { await recipient.SendAsync(frame, cancellationToken).ConfigureAwait(false); return; }
        if (!TryRefreshNativeTeamMembership(context, out _))
            throw new LegacyNativeTeamMembershipRetiredException();
        var original = expectedRecipientUid is { } uid
            ? entry.OriginalRecipients?.SingleOrDefault(client =>
                ReferenceEquals(client.Connection, recipient) && client.Uid == uid)
            : NativeTeamEntryClient(context, recipient);
        if (original is null)
            throw new InvalidOperationException("Original native membership publication recipient is missing.");
        if (!original.Owner.TryRead(original, out _)) return;
        await recipient.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (!TryRefreshNativeTeamMembership(context, out _))
            throw new LegacyNativeTeamMembershipRetiredException();
    }

    private sealed class LegacyNativeTeamMembershipRetiredException : Exception { }
}

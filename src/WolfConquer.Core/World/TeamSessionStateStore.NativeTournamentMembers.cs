using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Actual TeamSession reference, never a leader UID or separately seeded tournament token.
public sealed class LegacyTournamentTeam
{
    internal LegacyTournamentTeam(TeamSessionStateStore owner, object identity, uint uid)
    { Owner = owner; Identity = identity; Uid = uid; }
    internal TeamSessionStateStore Owner { get; }
    internal object Identity { get; }
    public uint Uid { get; }
}

public sealed record LegacyTournamentTeamSnapshot(
    uint Uid, string Name, LegacyTournamentWatcherClient? Leader,
    IReadOnlyList<LegacyTournamentWatcherClient> Members);

public sealed partial class TeamSessionStateStore
{
    private readonly LegacyTournamentWatcherHolders? _nativeClientAuthority;
    private readonly WorldSessionStore? _nativeWorld;

    public TeamSessionStateStore() { }
    public TeamSessionStateStore(WorldSessionStore nativeWorld, LegacyTournamentWatcherHolders nativeClientAuthority)
    {
        ArgumentNullException.ThrowIfNull(nativeWorld);
        ArgumentNullException.ThrowIfNull(nativeClientAuthority);
        _nativeWorld = nativeWorld;
        _nativeClientAuthority = nativeClientAuthority;
    }
    internal LegacyTournamentWatcherHolders? NativeClientAuthority => _nativeClientAuthority;

    // Hold the SAME WorldSessionStore Set/Remove identity gates through a synchronous roster
    // read/commit. Global connection ordering avoids opposite actor/peer ABBA acquisition.
    private bool WithNativeMembers(
        LegacyTournamentWatcherClient first, LegacyTournamentWatcherClient? second, Func<bool> action)
    {
        if (WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
            _nativeWorld is null || !ReferenceEquals(first.Owner, _nativeClientAuthority) ||
            (second is not null && !ReferenceEquals(second.Owner, _nativeClientAuthority))) return false;
        if (second is not null && ReferenceEquals(first.Connection, second.Connection))
        {
            if (!ReferenceEquals(first.Identity, second.Identity)) return false;
            second = null;
        }
        if (second is not null &&
            WorldSessionStore.GetConnectionGateOrder(first.Connection) >
            WorldSessionStore.GetConnectionGateOrder(second.Connection))
            (first, second) = (second, first);
        var accepted = false;
        _nativeWorld.WithCurrentNativeClientIdentity(first.Connection, first.Uid, firstIdentity =>
        {
            if (!ReferenceEquals(firstIdentity, first.Identity)) return;
            if (second is null)
            { lock (_gate) accepted = action(); return; }
            _nativeWorld.WithCurrentNativeClientIdentity(second.Connection, second.Uid, secondIdentity =>
            {
                if (!ReferenceEquals(secondIdentity, second.Identity)) return;
                lock (_gate) accepted = action();
            });
        });
        return accepted;
    }

    public bool TryCreateNativeTeam(LegacyTournamentWatcherClient owner, string teamName)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(teamName);
        return WithNativeMembers(owner, null, () =>
        {
            // Native Create replaces ONLY owner.Team. Even the SAME original client remains
            // in its prior Team.Members roster; other clients still reference that old Team.
            // Do not introduce a stale-roster cleanup policy absent in native MsgTeam.Create.
            _states.Remove(owner.Connection);
            _nextTeamUid = unchecked(_nextTeamUid + 1U); // Native Counter(1).Next starts at2.
            var team = new TeamSession(_nextTeamUid, owner.Uid, teamName) { NativeLeader = owner };
            team.Members.Add(new TeamSessionMember(owner.Connection, owner.Uid) { NativeClient = owner });
            _states.Add(owner.Connection,
                new TeamSessionState(team, owner.Uid, isLeader: true) { NativeClient = owner });
            return true;
        });
    }

    public bool TryAddNativeMember(
        LegacyTournamentWatcherClient teamMember, LegacyTournamentWatcherClient member, bool requireLeader)
    {
        ArgumentNullException.ThrowIfNull(teamMember);
        ArgumentNullException.ThrowIfNull(member);
        return WithNativeMembers(teamMember, member, () =>
        {
            if (!_states.TryGetValue(teamMember.Connection, out var existing) ||
                !ReferenceEquals(existing.NativeClient?.Identity, teamMember.Identity) ||
                (requireLeader && !existing.IsLeader) || existing.Team.ForbidJoin ||
                existing.Team.Members.Count >= MaxMembers ||
                existing.Team.Members.Any(candidate => candidate.Uid == member.Uid))
                return false;
            if (_states.TryGetValue(member.Connection, out var previous))
            {
                if (ReferenceEquals(previous.NativeClient?.Identity, member.Identity)) return false;
                _states.Remove(member.Connection); // Do NOT transfer the retired original's membership.
            }
            existing.Team.Members.Add(new TeamSessionMember(member.Connection, member.Uid)
                { NativeClient = member });
            _states.Add(member.Connection,
                new TeamSessionState(existing.Team, member.Uid, isLeader: false) { NativeClient = member });
            return true;
        });
    }

    public bool HasNativeTeam(LegacyTournamentWatcherClient member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return WithNativeMembers(member, null, () =>
            _states.TryGetValue(member.Connection, out var state) &&
            ReferenceEquals(state.NativeClient?.Identity, member.Identity));
    }

    public bool TryCaptureTournamentTeam(
        LegacyTournamentWatcherClient member, out LegacyTournamentTeam? team)
    {
        ArgumentNullException.ThrowIfNull(member);
        LegacyTournamentTeam? captured = null;
        var accepted = WithNativeMembers(member, null, () =>
        {
            if (!_states.TryGetValue(member.Connection, out var state) ||
                !ReferenceEquals(state.NativeClient?.Identity, member.Identity)) return false;
            captured = new(this, state.Team, state.Team.Uid);
            return true;
        });
        team = captured;
        return accepted;
    }

    // CURRENT GameClient.Team relation for public2291 target lookup, NOT Team.Members.
    // Actual historical roster consumers must use TryReadTournamentTeam.Members and
    // original-client publication guards; do not filter that roster by this current mapping.
    public bool IsTournamentTeamMember(
        LegacyTournamentTeam team, LegacyTournamentWatcherClient member)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(member);
        return WithNativeMembers(member, null, () =>
            ReferenceEquals(team.Owner, this) &&
            _states.TryGetValue(member.Connection, out var state) &&
            ReferenceEquals(state.Team, team.Identity) &&
            ReferenceEquals(state.NativeClient?.Identity, member.Identity));
    }

    public bool TryReadTournamentTeam(
        LegacyTournamentTeam team, out LegacyTournamentTeamSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(team);
        lock (_gate)
        {
            if (!ReferenceEquals(team.Owner, this) || team.Identity is not TeamSession actual)
            { snapshot = null; return false; }
            // ORIGINAL references remain actual roster members; never recapture a rebound occupant.
            snapshot = new(actual.Uid, actual.TeamName, actual.NativeLeader,
                actual.Members.Where(member => member.NativeClient is not null)
                    .Select(member => member.NativeClient!).ToArray());
            return true;
        }
    }

    internal bool InitializeNativeMemberInfo(
        LegacyTournamentWatcherClient client, LegacyTeamMemberInfoEntry entry)
    {
        if (entry.Uid != client.Uid) return false;
        return WithNativeMembers(client, null, () =>
        {
            if (!_states.TryGetValue(client.Connection, out var state) ||
                !ReferenceEquals(state.NativeClient?.Identity, client.Identity)) return false;
            var member = state.Team.Members.FirstOrDefault(member =>
                ReferenceEquals(member.NativeClient?.Identity, client.Identity));
            if (member is null) return false;
            member.LegacyInfo = entry;
            return true;
        });
    }

    private void RefreshNativeTeamLeaderName(TeamSession team)
    {
        var leader = team.NativeLeader;
        if (_nativeWorld is null || leader is null ||
            WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate) return;
        _nativeWorld.WithCurrentNativeClientIdentity(leader.Connection, leader.Uid, identity =>
        {
            if (!ReferenceEquals(identity, leader.Identity) ||
                !_nativeWorld.TryGet(leader.Connection, out var current) || current is null) return;
            lock (_gate)
                if (ReferenceEquals(team.NativeLeader?.Identity, leader.Identity))
                    team.TeamName = current.Character.Name;
        });
    }

    internal IReadOnlyList<LegacyTeamMemberInfoEntry> ReadNativeTeamMemberInfo(LegacyTournamentTeam team)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(team.Owner, this) || team.Identity is not TeamSession actual)
                return Array.Empty<LegacyTeamMemberInfoEntry>();
            return actual.Members.Where(member => member.NativeClient is not null && member.LegacyInfo is not null)
                .Select(member => member.LegacyInfo!).ToArray();
        }
    }

}

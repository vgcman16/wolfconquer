using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class TeamSessionStateStore
{
    private const int MaxMembers = 5;

    private readonly ConditionalWeakTable<ConquerTcpConnection, TeamSessionState> _states = new();
    private readonly object _gate = new();
    private uint _nextTeamUid = 1;

    public void CreateTeam(ConquerTcpConnection connection, long leaderUid)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            if (_states.TryGetValue(connection, out var existingState))
            {
                ClearTeamNoLock(existingState.Team);
            }

            var team = new TeamSession(_nextTeamUid, leaderUid, $"Team{_nextTeamUid}");
            _nextTeamUid++;
            team.Members.Add(new TeamSessionMember(connection, leaderUid));
            _states.Add(connection, new TeamSessionState(team, leaderUid, isLeader: true));
        }
    }

    public void DismissTeam(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            if (_states.TryGetValue(connection, out var state))
            {
                ClearTeamNoLock(state.Team);
            }
        }
    }

    public void ExitTeam(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            if (_states.TryGetValue(connection, out var state))
            {
                var team = state.Team;
                var wasLeader = state.IsLeader;

                RemoveConnectionNoLock(connection, state);
                if (wasLeader)
                {
                    PromoteFirstRemainingMemberNoLock(team);
                }
            }
        }
    }

    public bool TryAddMember(ConquerTcpConnection leaderConnection, ConquerTcpConnection memberConnection, long memberUid)
    {
        if (leaderConnection is null || memberConnection is null)
        {
            return false;
        }

        lock (_gate)
        {
            if (!_states.TryGetValue(leaderConnection, out var leaderState) ||
                _states.TryGetValue(memberConnection, out _) ||
                !leaderState.IsLeader ||
                leaderState.Team.Members.Count >= MaxMembers)
            {
                return false;
            }

            leaderState.Team.Members.Add(new TeamSessionMember(memberConnection, memberUid));
            _states.Add(memberConnection, new TeamSessionState(leaderState.Team, memberUid, isLeader: false));
            return true;
        }
    }

    public IReadOnlyList<ConquerTcpConnection> GetConnections(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            if (!_states.TryGetValue(connection, out var state))
            {
                return Array.Empty<ConquerTcpConnection>();
            }

            var connections = new ConquerTcpConnection[state.Team.Members.Count];
            for (var i = 0; i < state.Team.Members.Count; i++)
            {
                connections[i] = state.Team.Members[i].Connection;
            }

            return connections;
        }
    }

    public bool IsTeamMember(ConquerTcpConnection connection, long memberUid)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            if (!_states.TryGetValue(connection, out var state))
            {
                return false;
            }

            foreach (var member in state.Team.Members)
            {
                if (member.Uid == memberUid)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool AllowsProtectedFloorPickup(
        ConquerTcpConnection picker,
        uint ownerUid,
        FloorItemRuntimePickupKind kind)
    {
        ArgumentNullException.ThrowIfNull(picker);

        lock (_gate)
        {
            if (!_states.TryGetValue(picker, out var state))
            {
                return false;
            }

            if (kind == FloorItemRuntimePickupKind.Money)
            {
                return state.Team.PickupMoney;
            }

            if (!state.Team.PickupItems)
            {
                return false;
            }

            foreach (var member in state.Team.Members)
            {
                if (member.Uid == ownerUid)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public uint GetTeamUid(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            return _states.TryGetValue(connection, out var state) ? state.Team.Uid : 0;
        }
    }

    public string GetTeamName(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            return _states.TryGetValue(connection, out var state) ? state.Team.TeamName : string.Empty;
        }
    }

    public void SetTeamName(ConquerTcpConnection connection, string teamName)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            if (_states.TryGetValue(connection, out var state))
            {
                state.Team.TeamName = teamName;
            }
        }
    }

    public bool IsFull(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            return _states.TryGetValue(connection, out var state) && state.Team.Members.Count >= MaxMembers;
        }
    }

    public bool HasTeam(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            return _states.TryGetValue(connection, out _);
        }
    }

    public bool IsLeader(ConquerTcpConnection connection, long characterUid)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            return _states.TryGetValue(connection, out var state)
                && state.IsLeader
                && state.Team.LeaderUid == characterUid;
        }
    }

    public uint GetMemberCount(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            return _states.TryGetValue(connection, out var state) ? (uint)state.Team.Members.Count : 0;
        }
    }

    public long? GetLeaderUid(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            return _states.TryGetValue(connection, out var state) ? state.Team.LeaderUid : null;
        }
    }

    public void SetForbidJoin(ConquerTcpConnection connection, bool forbidJoin)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            if (_states.TryGetValue(connection, out var state))
            {
                state.Team.ForbidJoin = forbidJoin;
            }
        }
    }

    public bool GetForbidJoin(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            return _states.TryGetValue(connection, out var state) && state.Team.ForbidJoin;
        }
    }

    public void SetPickupMoney(ConquerTcpConnection connection, bool pickupMoney)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            if (_states.TryGetValue(connection, out var state))
            {
                state.Team.PickupMoney = pickupMoney;
            }
        }
    }

    public bool GetPickupMoney(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            return _states.TryGetValue(connection, out var state) && state.Team.PickupMoney;
        }
    }

    public void SetPickupItems(ConquerTcpConnection connection, bool pickupItems)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            if (_states.TryGetValue(connection, out var state))
            {
                state.Team.PickupItems = pickupItems;
            }
        }
    }

    public bool GetPickupItems(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            return _states.TryGetValue(connection, out var state) && state.Team.PickupItems;
        }
    }

    private void ClearTeamNoLock(TeamSession team)
    {
        foreach (var member in team.Members)
        {
            _states.Remove(member.Connection);
        }

        team.Members.Clear();
    }

    private void RemoveConnectionNoLock(ConquerTcpConnection connection, TeamSessionState state)
    {
        for (var i = 0; i < state.Team.Members.Count; i++)
        {
            if (ReferenceEquals(state.Team.Members[i].Connection, connection))
            {
                state.Team.Members.RemoveAt(i);
                break;
            }
        }

        _states.Remove(connection);
    }

    private void PromoteFirstRemainingMemberNoLock(TeamSession team)
    {
        if (team.Members.Count == 0)
        {
            return;
        }

        var leader = team.Members[0];
        team.LeaderUid = leader.Uid;

        foreach (var member in team.Members)
        {
            if (_states.TryGetValue(member.Connection, out var state))
            {
                state.IsLeader = ReferenceEquals(member.Connection, leader.Connection);
            }
        }
    }

    private sealed class TeamSession(uint uid, long leaderUid, string teamName)
    {
        public uint Uid { get; } = uid;

        public long LeaderUid { get; set; } = leaderUid;

        public string TeamName { get; set; } = teamName;

        public List<TeamSessionMember> Members { get; } = [];

        public bool ForbidJoin { get; set; }

        public bool PickupMoney { get; set; } = true;

        public bool PickupItems { get; set; }
    }

    private sealed class TeamSessionMember(ConquerTcpConnection connection, long uid)
    {
        public ConquerTcpConnection Connection { get; } = connection;

        public long Uid { get; } = uid;

        public LegacyTeamMemberInfoEntry? LegacyInfo { get; set; }
    }

    private sealed class TeamSessionState(TeamSession team, long memberUid, bool isLeader)
    {
        public TeamSession Team { get; } = team;

        public long MemberUid { get; } = memberUid;

        public bool IsLeader { get; set; } = isLeader;
    }
}

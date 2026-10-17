using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed class TeamArenaSessionStateStore
{
    private readonly ConditionalWeakTable<ConquerTcpConnection, TeamArenaSessionState> _states = new();
    private readonly List<TeamArenaMatchRegistration> _registered = [];
    private readonly List<TeamArenaPendingMatch> _pendingMatches = [];
    private readonly List<TeamArenaActiveMatch> _activeMatches = [];
    private readonly object _gate = new();
    private ulong _nextRegistrationOrder;

    public LegacyTeamArenaInfoStatus GetStatus(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            return _states.TryGetValue(connection, out var state)
                ? state.Status
                : LegacyTeamArenaInfoStatus.NotSignedUp;
        }
    }

    public void SetStatus(ConquerTcpConnection connection, LegacyTeamArenaInfoStatus status)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            _states.GetOrCreateValue(connection).Status = status;
            if (status != LegacyTeamArenaInfoStatus.WaitingForOpponent)
            {
                RemoveRegisteredNoLock(connection);
            }

            if (status == LegacyTeamArenaInfoStatus.NotSignedUp)
            {
                RemovePendingNoLock(connection);
                RemoveActiveNoLock(connection);
            }
        }
    }

    public uint GetPurchasedPoints(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            return _states.TryGetValue(connection, out var state) ? state.PurchasedPoints : 0;
        }
    }

    public uint AddPurchasedPoints(ConquerTcpConnection connection, uint points)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            var state = _states.GetOrCreateValue(connection);
            state.PurchasedPoints = checked(state.PurchasedPoints + points);
            return state.PurchasedPoints;
        }
    }

    public void AddMatchWin(ConquerTcpConnection connection, uint points)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            var state = _states.GetOrCreateValue(connection);
            state.ArenaPointDelta += points;
            state.TotalWinBonus++;
            state.TodayWinBonus++;
            state.TodayBattlesBonus++;
        }
    }

    public void AddMatchLoss(ConquerTcpConnection connection, uint points)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            var state = _states.GetOrCreateValue(connection);
            state.ArenaPointDelta -= points;
            state.TotalLoseBonus++;
            state.TodayBattlesBonus++;
        }
    }

    public TeamArenaRuntimeStatistics GetRuntimeStatistics(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            return _states.TryGetValue(connection, out var state)
                ? new TeamArenaRuntimeStatistics(
                    state.ArenaPointDelta,
                    state.TotalWinBonus,
                    state.TotalLoseBonus,
                    state.TodayWinBonus,
                    state.TodayBattlesBonus)
                : default;
        }
    }

    public bool RegisterWaiting(
        ConquerTcpConnection connection,
        uint arenaPoints,
        out TeamArenaMatchPair pair)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            _states.GetOrCreateValue(connection).Status = LegacyTeamArenaInfoStatus.WaitingForOpponent;
            RemoveRegisteredNoLock(connection);
            RemovePendingNoLock(connection);
            RemoveActiveNoLock(connection);
            _registered.Add(new TeamArenaMatchRegistration(connection, arenaPoints, _nextRegistrationOrder++));

            var candidates = _registered
                .Where(registration =>
                    _states.TryGetValue(registration.Connection, out var state) &&
                    state.Status == LegacyTeamArenaInfoStatus.WaitingForOpponent)
                .OrderByDescending(registration => registration.ArenaPoints)
                .ThenBy(registration => registration.Order)
                .Take(2)
                .ToArray();
            if (candidates.Length < 2)
            {
                pair = default;
                return false;
            }

            RemoveRegisteredNoLock(candidates[0].Connection);
            RemoveRegisteredNoLock(candidates[1].Connection);
            _states.GetOrCreateValue(candidates[0].Connection).Status = LegacyTeamArenaInfoStatus.WaitingInactive;
            _states.GetOrCreateValue(candidates[1].Connection).Status = LegacyTeamArenaInfoStatus.WaitingInactive;
            pair = new TeamArenaMatchPair(candidates[0].Connection, candidates[1].Connection);
            _pendingMatches.Add(new TeamArenaPendingMatch(pair.First, pair.Second));
            return true;
        }
    }

    public bool TryAcceptCountdown(
        ConquerTcpConnection connection,
        out TeamArenaMatchPair pair)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            for (var index = 0; index < _pendingMatches.Count; index++)
            {
                var match = _pendingMatches[index];
                if (!match.Contains(connection))
                {
                    continue;
                }

                match.Accept(connection);
                _states.GetOrCreateValue(connection).Status = LegacyTeamArenaInfoStatus.WaitingInactive;
                if (!match.Accepted)
                {
                    pair = default;
                    return false;
                }

                _pendingMatches.RemoveAt(index);
                pair = new TeamArenaMatchPair(match.First, match.Second);
                _activeMatches.Add(new TeamArenaActiveMatch(match.First, match.Second));
                return true;
            }

            pair = default;
            return false;
        }
    }

    public bool TryCompleteActiveMatchAsLoser(
        ConquerTcpConnection loser,
        out TeamArenaCompletedMatch completed)
    {
        ArgumentNullException.ThrowIfNull(loser);

        lock (_gate)
        {
            for (var index = 0; index < _activeMatches.Count; index++)
            {
                var match = _activeMatches[index];
                if (!match.Contains(loser) || match.Done)
                {
                    continue;
                }

                _activeMatches.RemoveAt(index);
                var winner = ReferenceEquals(match.First, loser) ? match.Second : match.First;
                _states.GetOrCreateValue(loser).Status = LegacyTeamArenaInfoStatus.NotSignedUp;
                _states.GetOrCreateValue(winner).Status = LegacyTeamArenaInfoStatus.NotSignedUp;
                completed = match.CreateCompletedMatch(winner, loser);
                return true;
            }

            completed = default;
            return false;
        }
    }

    public bool TryEndActiveMatchAsLoser(
        ConquerTcpConnection loser,
        out TeamArenaCompletedMatch ended,
        DateTimeOffset? endedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(loser);

        lock (_gate)
        {
            foreach (var match in _activeMatches)
            {
                if (!match.Contains(loser) || match.Done)
                {
                    continue;
                }

                var winner = ReferenceEquals(match.First, loser) ? match.Second : match.First;
                match.Done = true;
                match.DoneAtUtc = endedAtUtc ?? DateTimeOffset.UtcNow;
                ended = match.CreateCompletedMatch(winner, loser);
                return true;
            }

            ended = default;
            return false;
        }
    }

    public bool IsQualifierTeam(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            RemoveExpiredEndedMatchesNoLock(DateTimeOffset.UtcNow);
            return IsActiveFighterNoLock(connection);
        }
    }

    public bool IsWatcher(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            RemoveExpiredEndedMatchesNoLock(DateTimeOffset.UtcNow);
            return IsWatcherNoLock(connection);
        }
    }

    public int RemoveExpiredEndedMatches(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            return RemoveExpiredEndedMatchesNoLock(nowUtc);
        }
    }

    public IReadOnlyList<TeamArenaMatchPair> GetActiveMatches()
    {
        lock (_gate)
        {
            RemoveExpiredEndedMatchesNoLock(DateTimeOffset.UtcNow);
            var matches = new TeamArenaMatchPair[_activeMatches.Count];
            for (var index = 0; index < matches.Length; index++)
            {
                matches[index] = new TeamArenaMatchPair(
                    _activeMatches[index].First,
                    _activeMatches[index].Second);
            }

            return matches;
        }
    }

    public bool IsActiveFighterOrWatcher(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            return IsActiveFighterNoLock(connection) || IsWatcherNoLock(connection);
        }
    }

    public bool TryAddWatcher(
        ConquerTcpConnection watcher,
        TeamArenaMatchPair pair,
        out TeamArenaWatcherUpdate update)
    {
        ArgumentNullException.ThrowIfNull(watcher);

        lock (_gate)
        {
            if (IsActiveFighterNoLock(watcher) || IsWatcherNoLock(watcher))
            {
                update = default;
                return false;
            }

            foreach (var match in _activeMatches)
            {
                if (match.Done || !match.Matches(pair))
                {
                    continue;
                }

                match.Watchers.Add(watcher);
                update = match.CreateWatcherUpdate();
                return true;
            }

            update = default;
            return false;
        }
    }

    public bool TryRemoveWatcher(
        ConquerTcpConnection watcher,
        out TeamArenaWatcherUpdate update)
    {
        ArgumentNullException.ThrowIfNull(watcher);

        lock (_gate)
        {
            foreach (var match in _activeMatches)
            {
                if (!match.Watchers.Remove(watcher))
                {
                    continue;
                }

                match.Cheerers.Remove(watcher);
                update = match.CreateWatcherUpdate();
                return true;
            }

            update = default;
            return false;
        }
    }

    public bool TryGetWatcherUpdate(
        TeamArenaMatchPair pair,
        out TeamArenaWatcherUpdate update)
    {
        lock (_gate)
        {
            foreach (var match in _activeMatches)
            {
                if (match.Done || !match.Matches(pair))
                {
                    continue;
                }

                update = match.CreateWatcherUpdate();
                return true;
            }

            update = default;
            return false;
        }
    }

    public bool TryAddDamage(
        TeamArenaMatchPair pair,
        bool firstTeam,
        uint damage,
        out TeamArenaScoreUpdate update)
    {
        lock (_gate)
        {
            foreach (var match in _activeMatches)
            {
                if (match.Done || !match.Matches(pair))
                {
                    continue;
                }

                if (firstTeam)
                {
                    match.FirstDamage += damage;
                }
                else
                {
                    match.SecondDamage += damage;
                }

                update = match.CreateScoreUpdate();
                return true;
            }

            update = default;
            return false;
        }
    }

    public bool TryGetScoreUpdate(
        TeamArenaMatchPair pair,
        out TeamArenaScoreUpdate update)
    {
        lock (_gate)
        {
            foreach (var match in _activeMatches)
            {
                if (!match.Matches(pair))
                {
                    continue;
                }

                update = match.CreateScoreUpdate();
                return true;
            }

            update = default;
            return false;
        }
    }

    public bool TryCheer(
        ConquerTcpConnection watcher,
        bool firstTeam,
        out TeamArenaWatcherUpdate update)
    {
        ArgumentNullException.ThrowIfNull(watcher);

        lock (_gate)
        {
            foreach (var match in _activeMatches)
            {
                if (!match.Watchers.Contains(watcher))
                {
                    continue;
                }

                return TryCheerNoLock(match, watcher, firstTeam, out update);
            }

            update = default;
            return false;
        }
    }

    public bool TryCheer(
        ConquerTcpConnection watcher,
        TeamArenaMatchPair pair,
        bool firstTeam,
        out TeamArenaWatcherUpdate update)
    {
        ArgumentNullException.ThrowIfNull(watcher);

        lock (_gate)
        {
            foreach (var match in _activeMatches)
            {
                if (!match.Matches(pair))
                {
                    continue;
                }

                if (!match.Watchers.Contains(watcher))
                {
                    update = default;
                    return false;
                }

                return TryCheerNoLock(match, watcher, firstTeam, out update);
            }

            update = default;
            return false;
        }
    }

    private static bool TryCheerNoLock(
        TeamArenaActiveMatch match,
        ConquerTcpConnection watcher,
        bool firstTeam,
        out TeamArenaWatcherUpdate update)
    {
        if (!match.Cheerers.Add(watcher))
        {
            update = match.CreateWatcherUpdate();
            return false;
        }

        if (firstTeam)
        {
            match.FirstCheers++;
        }
        else
        {
            match.SecondCheers++;
        }

        update = match.CreateWatcherUpdate();
        return true;
    }

    private void RemoveRegisteredNoLock(ConquerTcpConnection connection)
    {
        for (var index = _registered.Count - 1; index >= 0; index--)
        {
            if (ReferenceEquals(_registered[index].Connection, connection))
            {
                _registered.RemoveAt(index);
            }
        }
    }

    private bool IsActiveFighterNoLock(ConquerTcpConnection connection)
    {
        foreach (var match in _activeMatches)
        {
            if (match.Contains(connection))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsWatcherNoLock(ConquerTcpConnection connection)
    {
        foreach (var match in _activeMatches)
        {
            if (match.Watchers.Contains(connection))
            {
                return true;
            }
        }

        return false;
    }

    private void RemoveWatcherNoLock(ConquerTcpConnection connection)
    {
        foreach (var match in _activeMatches)
        {
            match.Watchers.Remove(connection);
            match.Cheerers.Remove(connection);
        }
    }

    private void RemovePendingNoLock(ConquerTcpConnection connection)
    {
        for (var index = _pendingMatches.Count - 1; index >= 0; index--)
        {
            if (_pendingMatches[index].Contains(connection))
            {
                _pendingMatches.RemoveAt(index);
            }
        }
    }

    private void RemoveActiveNoLock(ConquerTcpConnection connection)
    {
        for (var index = _activeMatches.Count - 1; index >= 0; index--)
        {
            if (_activeMatches[index].Contains(connection))
            {
                _activeMatches.RemoveAt(index);
            }
        }
    }

    private int RemoveExpiredEndedMatchesNoLock(DateTimeOffset nowUtc)
    {
        var removed = 0;
        for (var index = _activeMatches.Count - 1; index >= 0; index--)
        {
            var match = _activeMatches[index];
            if (!match.Done ||
                !LegacyQualifierMatchLifecycle.HasExpired(match.DoneAtUtc, nowUtc))
            {
                continue;
            }

            _activeMatches.RemoveAt(index);
            removed++;
        }

        return removed;
    }

    private sealed class TeamArenaSessionState
    {
        public LegacyTeamArenaInfoStatus Status { get; set; } = LegacyTeamArenaInfoStatus.NotSignedUp;

        public uint PurchasedPoints { get; set; }

        public long ArenaPointDelta { get; set; }

        public uint TotalWinBonus { get; set; }

        public uint TotalLoseBonus { get; set; }

        public uint TodayWinBonus { get; set; }

        public uint TodayBattlesBonus { get; set; }
    }

    private sealed record TeamArenaMatchRegistration(
        ConquerTcpConnection Connection,
        uint ArenaPoints,
        ulong Order);

    private sealed class TeamArenaPendingMatch(
        ConquerTcpConnection first,
        ConquerTcpConnection second)
    {
        public ConquerTcpConnection First { get; } = first;

        public ConquerTcpConnection Second { get; } = second;

        public bool FirstAccepted { get; private set; }

        public bool SecondAccepted { get; private set; }

        public bool Accepted => FirstAccepted && SecondAccepted;

        public bool Contains(ConquerTcpConnection connection)
        {
            return ReferenceEquals(connection, First) || ReferenceEquals(connection, Second);
        }

        public void Accept(ConquerTcpConnection connection)
        {
            if (ReferenceEquals(connection, First))
            {
                FirstAccepted = true;
            }
            else if (ReferenceEquals(connection, Second))
            {
                SecondAccepted = true;
            }
        }
    }

    private sealed class TeamArenaActiveMatch(
        ConquerTcpConnection first,
        ConquerTcpConnection second)
    {
        public ConquerTcpConnection First { get; } = first;

        public ConquerTcpConnection Second { get; } = second;

        public bool Contains(ConquerTcpConnection connection)
        {
            return ReferenceEquals(connection, First) || ReferenceEquals(connection, Second);
        }

        public bool Matches(TeamArenaMatchPair pair)
        {
            return ReferenceEquals(First, pair.First) && ReferenceEquals(Second, pair.Second);
        }

        public List<ConquerTcpConnection> Watchers { get; } = [];

        public HashSet<ConquerTcpConnection> Cheerers { get; } = [];

        public uint FirstCheers { get; set; }

        public uint SecondCheers { get; set; }

        public uint FirstDamage { get; set; }

        public uint SecondDamage { get; set; }

        public bool Done { get; set; }

        public DateTimeOffset DoneAtUtc { get; set; }

        public TeamArenaWatcherUpdate CreateWatcherUpdate()
        {
            return new TeamArenaWatcherUpdate(
                new TeamArenaMatchPair(First, Second),
                Watchers.ToArray(),
                FirstCheers,
                SecondCheers);
        }

        public TeamArenaScoreUpdate CreateScoreUpdate()
        {
            return new TeamArenaScoreUpdate(
                new TeamArenaMatchPair(First, Second),
                Watchers.ToArray(),
                FirstDamage,
                SecondDamage);
        }

        public TeamArenaCompletedMatch CreateCompletedMatch(
            ConquerTcpConnection winner,
            ConquerTcpConnection loser)
        {
            return new TeamArenaCompletedMatch(
                new TeamArenaMatchPair(First, Second),
                winner,
                loser,
                Watchers.ToArray(),
                FirstCheers,
                SecondCheers,
                FirstDamage,
                SecondDamage);
        }
    }
}

public readonly record struct TeamArenaMatchPair(
    ConquerTcpConnection First,
    ConquerTcpConnection Second);

public readonly record struct TeamArenaCompletedMatch(
    TeamArenaMatchPair Match,
    ConquerTcpConnection Winner,
    ConquerTcpConnection Loser,
    IReadOnlyList<ConquerTcpConnection> Watchers,
    uint FirstCheers,
    uint SecondCheers,
    uint FirstDamage,
    uint SecondDamage);

public readonly record struct TeamArenaWatcherUpdate(
    TeamArenaMatchPair Match,
    IReadOnlyList<ConquerTcpConnection> Watchers,
    uint FirstCheers,
    uint SecondCheers);

public readonly record struct TeamArenaScoreUpdate(
    TeamArenaMatchPair Match,
    IReadOnlyList<ConquerTcpConnection> Watchers,
    uint FirstDamage,
    uint SecondDamage);

public readonly record struct TeamArenaRuntimeStatistics(
    long ArenaPointDelta,
    uint TotalWinBonus,
    uint TotalLoseBonus,
    uint TodayWinBonus,
    uint TodayBattlesBonus);

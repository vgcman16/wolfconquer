using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed class ArenaSessionStateStore
{
    private readonly ConditionalWeakTable<ConquerTcpConnection, ArenaSessionState> _states = new();
    private readonly List<ArenaMatchRegistration> _registered = [];
    private readonly List<ArenaPendingMatch> _pendingMatches = [];
    private readonly List<ArenaActiveMatch> _activeMatches = [];
    private readonly object _gate = new();
    private ulong _nextRegistrationOrder;

    public MsgArenaInfoStatus GetStatus(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            return _states.TryGetValue(connection, out var state)
                ? state.Status
                : MsgArenaInfoStatus.NotSignedUp;
        }
    }

    public void SetStatus(ConquerTcpConnection connection, MsgArenaInfoStatus status)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            _states.GetOrCreateValue(connection).Status = status;
            if (status != MsgArenaInfoStatus.WaitingForOpponent)
            {
                RemoveRegisteredNoLock(connection);
            }

            if (status == MsgArenaInfoStatus.NotSignedUp)
            {
                RemovePendingNoLock(connection);
                RemoveActiveNoLock(connection);
                RemoveWatcherNoLock(connection);
            }
        }
    }

    public bool RegisterWaiting(
        ConquerTcpConnection connection,
        uint arenaPoints,
        out ArenaMatchPair pair)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            _states.GetOrCreateValue(connection).Status = MsgArenaInfoStatus.WaitingForOpponent;
            RemoveRegisteredNoLock(connection);
            RemovePendingNoLock(connection);
            _registered.Add(new ArenaMatchRegistration(connection, arenaPoints, _nextRegistrationOrder++));

            var candidates = _registered
                .Where(registration =>
                    _states.TryGetValue(registration.Connection, out var state) &&
                    state.Status == MsgArenaInfoStatus.WaitingForOpponent)
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
            _pendingMatches.Add(new ArenaPendingMatch(candidates[0].Connection, candidates[1].Connection));
            pair = new ArenaMatchPair(candidates[0].Connection, candidates[1].Connection);
            return true;
        }
    }

    public bool TryAcceptCountdown(
        ConquerTcpConnection connection,
        out ArenaMatchPair pair)
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

                if (match.Declined)
                {
                    pair = default;
                    return false;
                }

                match.Accept(connection);
                if (!match.Accepted)
                {
                    pair = default;
                    return false;
                }

                _pendingMatches.RemoveAt(index);
                pair = new ArenaMatchPair(match.First, match.Second);
                _activeMatches.Add(new ArenaActiveMatch(match.First, match.Second));
                return true;
            }

            pair = default;
            return false;
        }
    }

    public bool TryDeclinePendingMatch(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            foreach (var match in _pendingMatches)
            {
                if (!match.Contains(connection))
                {
                    continue;
                }

                match.Decline(connection);
                _states.GetOrCreateValue(connection).Status = MsgArenaInfoStatus.WaitingForOpponent;
                return true;
            }

            return false;
        }
    }

    public bool TryCompleteDeclinedPendingMatch(
        ConquerTcpConnection connection,
        out ArenaCompletedMatch completed)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            for (var index = 0; index < _pendingMatches.Count; index++)
            {
                var match = _pendingMatches[index];
                if (!match.Contains(connection) ||
                    !match.Declined ||
                    match.DeclinedBy is not { } loser)
                {
                    continue;
                }

                _pendingMatches.RemoveAt(index);
                var winner = ReferenceEquals(match.First, loser) ? match.Second : match.First;
                _states.GetOrCreateValue(loser).Status = MsgArenaInfoStatus.NotSignedUp;
                _states.GetOrCreateValue(winner).Status = MsgArenaInfoStatus.NotSignedUp;
                completed = new ArenaCompletedMatch(
                    new ArenaMatchPair(match.First, match.Second),
                    winner,
                    loser,
                    [],
                    FirstCheers: 0,
                    SecondCheers: 0,
                    FirstDamage: 0,
                    SecondDamage: 0);
                return true;
            }

            completed = default;
            return false;
        }
    }

    public void AddMatchWin(ConquerTcpConnection connection, uint points)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            var state = _states.GetOrCreateValue(connection);
            state.ArenaPointDelta += points;
            state.TodayWinBonus++;
            state.TotalWinBonus++;
        }
    }

    public void AddMatchLoss(ConquerTcpConnection connection, uint points, bool subtractPoints)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            var state = _states.GetOrCreateValue(connection);
            if (subtractPoints)
            {
                state.ArenaPointDelta -= points;
            }

            state.TodayBattlesBonus++;
            state.TotalLoseBonus++;
        }
    }

    public ArenaRuntimeStatistics GetRuntimeStatistics(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            return _states.TryGetValue(connection, out var state)
                ? new ArenaRuntimeStatistics(
                    state.ArenaPointDelta,
                    state.TodayWinBonus,
                    state.TotalWinBonus,
                    state.TodayBattlesBonus,
                    state.TotalLoseBonus)
                : default;
        }
    }

    public bool TryCompleteActiveMatchAsLoser(
        ConquerTcpConnection loser,
        out ArenaCompletedMatch completed)
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
                _states.GetOrCreateValue(loser).Status = MsgArenaInfoStatus.NotSignedUp;
                _states.GetOrCreateValue(winner).Status = MsgArenaInfoStatus.NotSignedUp;
                completed = match.CreateCompletedMatch(winner, loser);
                return true;
            }

            completed = default;
            return false;
        }
    }

    public bool TryEndActiveMatchAsLoser(
        ConquerTcpConnection loser,
        out ArenaCompletedMatch ended,
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

    public bool IsQualifierFighter(ConquerTcpConnection connection)
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

    public IReadOnlyList<ArenaMatchPair> GetActiveMatches()
    {
        lock (_gate)
        {
            RemoveExpiredEndedMatchesNoLock(DateTimeOffset.UtcNow);
            var matches = new ArenaMatchPair[_activeMatches.Count];
            for (var index = 0; index < matches.Length; index++)
            {
                matches[index] = new ArenaMatchPair(
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
            RemoveExpiredEndedMatchesNoLock(DateTimeOffset.UtcNow);
            return IsActiveFighterNoLock(connection) || IsWatcherNoLock(connection);
        }
    }

    public bool TryAddWatcher(
        ConquerTcpConnection watcher,
        ArenaMatchPair pair,
        out ArenaWatcherUpdate update)
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
                if (!match.Matches(pair))
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
        out ArenaWatcherUpdate update)
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
        ArenaMatchPair pair,
        out ArenaWatcherUpdate update)
    {
        lock (_gate)
        {
            foreach (var match in _activeMatches)
            {
                if (!match.Matches(pair))
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
        ConquerTcpConnection attacker,
        ConquerTcpConnection defender,
        uint damage,
        out ArenaScoreUpdate update)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);

        lock (_gate)
        {
            foreach (var match in _activeMatches)
            {
                if (match.Done ||
                    !match.Contains(attacker) ||
                    !match.Contains(defender) ||
                    ReferenceEquals(attacker, defender))
                {
                    continue;
                }

                if (ReferenceEquals(attacker, match.First))
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

    public bool TryCheer(
        ConquerTcpConnection watcher,
        bool firstFighter,
        out ArenaWatcherUpdate update)
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

                return TryCheerNoLock(match, watcher, firstFighter, out update);
            }

            update = default;
            return false;
        }
    }

    public bool TryCheer(
        ConquerTcpConnection watcher,
        ArenaMatchPair pair,
        bool firstFighter,
        out ArenaWatcherUpdate update)
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

                return TryCheerNoLock(match, watcher, firstFighter, out update);
            }

            update = default;
            return false;
        }
    }

    public bool TryGetScoreUpdate(
        ArenaMatchPair pair,
        out ArenaScoreUpdate update)
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

    private static bool TryCheerNoLock(
        ArenaActiveMatch match,
        ConquerTcpConnection watcher,
        bool firstFighter,
        out ArenaWatcherUpdate update)
    {
        if (!match.Cheerers.Add(watcher))
        {
            update = match.CreateWatcherUpdate();
            return false;
        }

        if (firstFighter)
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

    private sealed class ArenaSessionState
    {
        public MsgArenaInfoStatus Status { get; set; } = MsgArenaInfoStatus.NotSignedUp;

        public long ArenaPointDelta { get; set; }

        public uint TodayWinBonus { get; set; }

        public uint TotalWinBonus { get; set; }

        public uint TodayBattlesBonus { get; set; }

        public uint TotalLoseBonus { get; set; }
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

    private sealed record ArenaMatchRegistration(
        ConquerTcpConnection Connection,
        uint ArenaPoints,
        ulong Order);

    private sealed class ArenaPendingMatch(
        ConquerTcpConnection first,
        ConquerTcpConnection second)
    {
        public ConquerTcpConnection First { get; } = first;

        public ConquerTcpConnection Second { get; } = second;

        public bool FirstAccepted { get; private set; }

        public bool SecondAccepted { get; private set; }

        public bool Accepted => FirstAccepted && SecondAccepted;

        public ConquerTcpConnection? DeclinedBy { get; private set; }

        public bool Declined => DeclinedBy is not null;

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

        public void Decline(ConquerTcpConnection connection)
        {
            if (Contains(connection))
            {
                DeclinedBy = connection;
            }
        }
    }

    private sealed class ArenaActiveMatch(
        ConquerTcpConnection first,
        ConquerTcpConnection second)
    {
        public ConquerTcpConnection First { get; } = first;

        public ConquerTcpConnection Second { get; } = second;

        public bool Contains(ConquerTcpConnection connection)
        {
            return ReferenceEquals(connection, First) || ReferenceEquals(connection, Second);
        }

        public bool Matches(ArenaMatchPair pair)
        {
            return ReferenceEquals(First, pair.First) && ReferenceEquals(Second, pair.Second);
        }

        public uint FirstDamage { get; set; }

        public uint SecondDamage { get; set; }

        public HashSet<ConquerTcpConnection> Watchers { get; } = [];

        public HashSet<ConquerTcpConnection> Cheerers { get; } = [];

        public uint FirstCheers { get; set; }

        public uint SecondCheers { get; set; }

        public bool Done { get; set; }

        public DateTimeOffset DoneAtUtc { get; set; }

        public ArenaScoreUpdate CreateScoreUpdate()
        {
            return new ArenaScoreUpdate(
                new ArenaMatchPair(First, Second),
                FirstDamage,
                SecondDamage);
        }

        public ArenaWatcherUpdate CreateWatcherUpdate()
        {
            return new ArenaWatcherUpdate(
                new ArenaMatchPair(First, Second),
                Watchers.ToArray(),
                FirstCheers,
                SecondCheers);
        }

        public ArenaCompletedMatch CreateCompletedMatch(
            ConquerTcpConnection winner,
            ConquerTcpConnection loser)
        {
            return new ArenaCompletedMatch(
                new ArenaMatchPair(First, Second),
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

public readonly record struct ArenaMatchPair(
    ConquerTcpConnection First,
    ConquerTcpConnection Second);

public readonly record struct ArenaCompletedMatch(
    ArenaMatchPair Match,
    ConquerTcpConnection Winner,
    ConquerTcpConnection Loser,
    IReadOnlyList<ConquerTcpConnection> Watchers,
    uint FirstCheers,
    uint SecondCheers,
    uint FirstDamage,
    uint SecondDamage);

public readonly record struct ArenaScoreUpdate(
    ArenaMatchPair Match,
    uint FirstDamage,
    uint SecondDamage);

public readonly record struct ArenaWatcherUpdate(
    ArenaMatchPair Match,
    IReadOnlyList<ConquerTcpConnection> Watchers,
    uint FirstCheers,
    uint SecondCheers);

public readonly record struct ArenaRuntimeStatistics(
    long ArenaPointDelta,
    uint TodayWinBonus,
    uint TotalWinBonus,
    uint TodayBattlesBonus,
    uint TotalLoseBonus);

using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed class LegacyTeamPkSessionStateStore
{
    private readonly List<LegacyTeamPkActiveMatch> _activeMatches = [];
    private readonly object _gate = new();

    public void ActivateMatch(
        ConquerTcpConnection firstTeam,
        ConquerTcpConnection secondTeam,
        uint dynamicId)
    {
        ArgumentNullException.ThrowIfNull(firstTeam);
        ArgumentNullException.ThrowIfNull(secondTeam);

        lock (_gate)
        {
            RemoveActiveNoLock(firstTeam);
            RemoveActiveNoLock(secondTeam);
            _activeMatches.Add(new LegacyTeamPkActiveMatch(firstTeam, secondTeam, dynamicId));
        }
    }

    public IReadOnlyList<LegacyTeamPkMatchPair> GetActiveMatches()
    {
        lock (_gate)
        {
            RemoveExpiredEndedMatchesNoLock(DateTimeOffset.UtcNow);
            var matches = new LegacyTeamPkMatchPair[_activeMatches.Count];
            for (var index = 0; index < matches.Length; index++)
            {
                matches[index] = new LegacyTeamPkMatchPair(
                    _activeMatches[index].First,
                    _activeMatches[index].Second);
            }

            return matches;
        }
    }

    public bool TryAddDamage(
        LegacyTeamPkMatchPair pair,
        bool firstTeam,
        uint damage,
        out LegacyTeamPkScoreUpdate update)
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
                    match.FirstPoints += damage;
                }
                else
                {
                    match.SecondPoints += damage;
                }

                update = match.CreateScoreUpdate();
                return true;
            }

            update = default;
            return false;
        }
    }

    public bool TryEndActiveMatchAsLoser(
        ConquerTcpConnection loserTeam,
        DateTimeOffset? endedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(loserTeam);

        lock (_gate)
        {
            foreach (var match in _activeMatches)
            {
                if (!match.Contains(loserTeam) || match.Done)
                {
                    continue;
                }

                match.Done = true;
                match.DoneAtUtc = endedAtUtc ?? DateTimeOffset.UtcNow;
                return true;
            }

            return false;
        }
    }

    public bool IsQualifierTeam(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            RemoveExpiredEndedMatchesNoLock(DateTimeOffset.UtcNow);
            return _activeMatches.Any(match => match.Contains(connection));
        }
    }

    public int RemoveExpiredEndedMatches(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            return RemoveExpiredEndedMatchesNoLock(nowUtc);
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

    private sealed class LegacyTeamPkActiveMatch(
        ConquerTcpConnection first,
        ConquerTcpConnection second,
        uint dynamicId)
    {
        public ConquerTcpConnection First { get; } = first;

        public ConquerTcpConnection Second { get; } = second;

        public uint DynamicId { get; } = dynamicId;

        public uint FirstPoints { get; set; }

        public uint SecondPoints { get; set; }

        public bool Done { get; set; }

        public DateTimeOffset DoneAtUtc { get; set; }

        public bool Contains(ConquerTcpConnection connection)
        {
            return ReferenceEquals(connection, First) || ReferenceEquals(connection, Second);
        }

        public bool Matches(LegacyTeamPkMatchPair pair)
        {
            return ReferenceEquals(First, pair.First) && ReferenceEquals(Second, pair.Second);
        }

        public LegacyTeamPkScoreUpdate CreateScoreUpdate()
        {
            return new LegacyTeamPkScoreUpdate(
                new LegacyTeamPkMatchPair(First, Second),
                DynamicId,
                FirstPoints,
                SecondPoints);
        }
    }
}

public readonly record struct LegacyTeamPkMatchPair(
    ConquerTcpConnection First,
    ConquerTcpConnection Second);

public readonly record struct LegacyTeamPkScoreUpdate(
    LegacyTeamPkMatchPair Match,
    uint DynamicId,
    uint FirstPoints,
    uint SecondPoints);

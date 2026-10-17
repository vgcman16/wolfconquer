using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed class LegacyElitePkSessionStateStore
{
    private readonly List<LegacyElitePkActiveMatch> _activeMatches = [];
    private readonly object _gate = new();

    public void ActivateMatch(
        ConquerTcpConnection first,
        ConquerTcpConnection second,
        uint dynamicId)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        lock (_gate)
        {
            RemoveActiveNoLock(first);
            RemoveActiveNoLock(second);
            _activeMatches.Add(new LegacyElitePkActiveMatch(first, second, dynamicId));
        }
    }

    public bool TryAddDamage(
        ConquerTcpConnection attacker,
        ConquerTcpConnection defender,
        uint damage,
        out LegacyElitePkScoreUpdate update)
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
        ConquerTcpConnection loser,
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

                match.Done = true;
                match.DoneAtUtc = endedAtUtc ?? DateTimeOffset.UtcNow;
                return true;
            }

            return false;
        }
    }

    public bool IsQualifierFighter(ConquerTcpConnection connection)
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

    private sealed class LegacyElitePkActiveMatch(
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

        public LegacyElitePkScoreUpdate CreateScoreUpdate()
        {
            return new LegacyElitePkScoreUpdate(
                new LegacyElitePkMatchPair(First, Second),
                DynamicId,
                FirstPoints,
                SecondPoints);
        }
    }
}

public readonly record struct LegacyElitePkMatchPair(
    ConquerTcpConnection First,
    ConquerTcpConnection Second);

public readonly record struct LegacyElitePkScoreUpdate(
    LegacyElitePkMatchPair Match,
    uint DynamicId,
    uint FirstPoints,
    uint SecondPoints);

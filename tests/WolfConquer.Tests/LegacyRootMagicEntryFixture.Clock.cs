namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private sealed partial class RootMagicEntryFixture
    {
        // Wraps the existing manual provider; callbacks, scheduling, disposal,
        // and time still belong to its real timers. Labels only observe which
        // actual runtime worker created each one-shot Task.Delay.
        private sealed class RootRuntimeClock(DateTimeOffset origin) : TimeProvider
        {
            private readonly ManualRuntimeTimeProvider inner = new(origin);
            private readonly AsyncLocal<string?> owner = new();
            private readonly object gate = new();
            private readonly Dictionary<long, Observation> observations = [];
            private long serial;

            private sealed class Observation(string label)
            {
                internal readonly string Label = label;
                internal bool Active;
                internal long Fires;
                internal TimeSpan Period;
            }

            private sealed class OwnerScope(AsyncLocal<string?> owner, string? before) : IDisposable
            {
                public void Dispose() => owner.Value = before;
            }
            internal IDisposable Identify(string label)
            {
                var before = owner.Value;
                owner.Value = label;
                return new OwnerScope(owner, before);
            }

            internal int ScheduledTimerCount => inner.ScheduledTimerCount;
            internal void Advance(TimeSpan amount) => inner.Advance(amount);
            public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();
            public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;
            public override long GetTimestamp() => inner.GetTimestamp();
            public override long TimestampFrequency => inner.TimestampFrequency;

            public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            {
                ArgumentNullException.ThrowIfNull(callback);
                long id;
                lock (gate)
                {
                    id = ++serial;
                    observations.Add(id, new(owner.Value ?? "unlabelled") { Active = dueTime != Timeout.InfiniteTimeSpan, Period = period });
                }
                var timer = inner.CreateTimer(value =>
                {
                    lock (gate)
                    {
                        var observation = observations[id];
                        observation.Fires++;
                        if (observation.Period == Timeout.InfiniteTimeSpan) observation.Active = false;
                    }
                    callback(value);
                }, state, dueTime, period);
                return new ObservedTimer(this, id, timer);
            }

            internal long ActiveGeneration(string label)
            {
                lock (gate)
                    return observations.Where(pair => pair.Value.Label == label && pair.Value.Active)
                        .Select(pair => pair.Key).DefaultIfEmpty(0).Max();
            }
            internal string Describe()
            {
                lock (gate)
                    return string.Join(", ", observations.GroupBy(row => row.Value.Label).Select(group =>
                        $"{group.Key}:created={group.Count()} active={group.Count(row => row.Value.Active)} " +
                        $"last={group.Max(row => row.Key)} fired={group.Sum(row => row.Value.Fires)}"));
            }
            private void Changed(long id, TimeSpan due, TimeSpan period)
            {
                lock (gate)
                {
                    observations[id].Active = due != Timeout.InfiniteTimeSpan;
                    observations[id].Period = period;
                }
            }
            private sealed class ObservedTimer(RootRuntimeClock owner, long id, ITimer actual) : ITimer
            {
                public bool Change(TimeSpan dueTime, TimeSpan period)
                {
                    if (!actual.Change(dueTime, period)) return false;
                    owner.Changed(id, dueTime, period);
                    return true;
                }
                public void Dispose() { actual.Dispose(); owner.Changed(id, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan); }
                public async ValueTask DisposeAsync()
                {
                    await actual.DisposeAsync().ConfigureAwait(false);
                    owner.Changed(id, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }
            }
        }
    }
}

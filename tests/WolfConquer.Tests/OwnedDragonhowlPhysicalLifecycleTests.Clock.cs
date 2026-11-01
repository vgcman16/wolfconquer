namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlPhysicalLifecycleTests
{
    private sealed class RetirementClock : TimeProvider
    {
        private readonly object gate = new();
        private readonly List<ActualDelay> delays = [];
        private TaskCompletionSource changed = NewSignal();
        private DateTimeOffset now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        private long timestamp;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override DateTimeOffset GetUtcNow() { lock (gate) return now; }
        public override long GetTimestamp() { lock (gate) return timestamp; }
        internal int Count { get { lock (gate) return delays.Count; } }
        internal bool AllDisposed { get { lock (gate) return delays.All(delay => delay.Disposed); } }

        // Only real Task.Delay registrations supply these callbacks. The clock
        // neither calls a Frame tick directly nor creates a status registration.
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ArgumentNullException.ThrowIfNull(callback);
            lock (gate)
            {
                var delay = new ActualDelay(this, callback, state, now, dueTime, period);
                delays.Add(delay); Pulse(); return delay;
            }
        }

        internal void FireRegisteredGlobalDelay(int oneBasedIndex, TimeSpan advance)
        {
            ActualDelay delay;
            lock (gate)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(advance, TimeSpan.Zero);
                now += advance; timestamp = checked(timestamp + advance.Ticks);
                delay = delays[oneBasedIndex - 1];
                Assert.False(delay.Disposed); Assert.False(delay.Fired);
                Assert.Equal(TimeSpan.FromSeconds(1), delay.Due);
                Assert.Equal(Timeout.InfiniteTimeSpan, delay.Period);
                Assert.True(now >= delay.RegisteredAt + delay.Due, "Cannot fire a real delay before it is due.");
                delay.Fired = true;
            }
            delay.Invoke();
        }

        internal async Task WaitForCountAsync(int count)
        {
            while (true)
            {
                Task signal;
                lock (gate) { if (delays.Count >= count) return; signal = changed.Task; }
                await signal;
            }
        }

        private void Pulse() { var previous = changed; changed = NewSignal(); previous.TrySetResult(); }
        private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        private sealed class ActualDelay(RetirementClock owner, TimerCallback callback, object? state,
            DateTimeOffset registeredAt, TimeSpan due, TimeSpan period) : ITimer
        {
            internal DateTimeOffset RegisteredAt { get; private set; } = registeredAt;
            internal TimeSpan Due { get; private set; } = due;
            internal TimeSpan Period { get; private set; } = period;
            internal bool Disposed { get; private set; }
            internal bool Fired { get; set; }
            internal void Invoke() => callback(state);
            public bool Change(TimeSpan newDue, TimeSpan newPeriod)
            {
                lock (owner.gate)
                {
                    if (Disposed) return false;
                    RegisteredAt = owner.now; Due = newDue; Period = newPeriod; owner.Pulse(); return true;
                }
            }
            public void Dispose() { lock (owner.gate) { Disposed = true; owner.Pulse(); } }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}

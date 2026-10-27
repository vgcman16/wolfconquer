namespace WolfConquer.Tests;

public sealed partial class LegacyPoisonedRuntimeExpiryTests
{
    private static readonly TimeSpan RuntimeTickInterval = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan WorkerBound = TimeSpan.FromSeconds(5);

    // These are the actual Task.Delay registrations made by the runtime workers.
    // The clock does not invoke the expiry method or substitute a worker callback.
    private sealed class PoisonExpiryTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private readonly object gate = new();
        private readonly List<PoisonTimer> timers = [];
        private TaskCompletionSource changed = NewSignal();
        private DateTimeOffset now = initial;
        private long timestamp;

        internal int TimerCount { get { lock (gate) return timers.Count; } }
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override DateTimeOffset GetUtcNow() { lock (gate) return now; }
        public override long GetTimestamp() { lock (gate) return timestamp; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ArgumentNullException.ThrowIfNull(callback);
            var timer = new PoisonTimer(this, callback, state, dueTime, period);
            lock (gate)
            {
                timers.Add(timer);
                Pulse();
            }
            return timer;
        }

        internal async Task WaitForTimerCountAsync(int count)
        {
            while (true)
            {
                Task signal;
                lock (gate)
                {
                    if (timers.Count >= count) return;
                    signal = changed.Task;
                }
                await signal;
            }
        }

        internal void FireGlobalTick()
        {
            PoisonTimer timer;
            lock (gate)
            {
                Assert.Equal(2, timers.Count);
                timer = timers[1];
                Assert.False(timer.Disposed);
                Assert.False(timer.Fired);
                Assert.Equal(RuntimeTickInterval, timer.DueTime);
                Assert.Equal(Timeout.InfiniteTimeSpan, timer.Period);
                Assert.False(timers[0].Fired);
                now += RuntimeTickInterval;
                timestamp = checked(timestamp + RuntimeTickInterval.Ticks);
                timer.Fired = true;
            }
            timer.Invoke();
        }

        internal void AssertAllDisposed()
        {
            lock (gate) Assert.All(timers, timer => Assert.True(timer.Disposed));
        }

        private void Pulse()
        {
            var previous = changed;
            changed = NewSignal();
            previous.TrySetResult();
        }

        private static TaskCompletionSource NewSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private sealed class PoisonTimer(PoisonExpiryTimeProvider owner, TimerCallback callback, object? state,
            TimeSpan dueTime, TimeSpan period) : ITimer
        {
            internal TimeSpan DueTime { get; private set; } = dueTime;
            internal TimeSpan Period { get; private set; } = period;
            internal bool Disposed { get; private set; }
            internal bool Fired { get; set; }
            internal void Invoke() => callback(state);

            public bool Change(TimeSpan due, TimeSpan interval)
            {
                lock (owner.gate)
                {
                    if (Disposed) return false;
                    DueTime = due;
                    Period = interval;
                    owner.Pulse();
                    return true;
                }
            }

            public void Dispose()
            {
                lock (owner.gate)
                {
                    Disposed = true;
                    owner.Pulse();
                }
            }

            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}

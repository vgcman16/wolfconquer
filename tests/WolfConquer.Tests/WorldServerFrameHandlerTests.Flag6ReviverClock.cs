namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    // Test-only scheduling of the real Task.Delay callbacks created by the two
    // runtime owners. No frames, sessions, ticker values or send results are injected.
    // RuntimeTicks starts player first, then global. The test EARNS that identity
    // by observing XP=1 plus the player's next registered delay before firing global.
    private sealed class Flag6ReviverScheduledTickClock : TimeProvider
    {
        private readonly object gate = new();
        private readonly List<ScheduledTimer> timers = new();
        private readonly Dictionary<int, TaskCompletionSource<bool>> registrations = new();
        private readonly DateTimeOffset origin;
        private DateTimeOffset now;

        internal Flag6ReviverScheduledTickClock(DateTimeOffset origin)
        {
            this.origin = origin.ToUniversalTime();
            now = this.origin;
        }

        public override TimeZoneInfo LocalTimeZone => TimeProvider.System.LocalTimeZone;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override DateTimeOffset GetUtcNow() { lock (gate) return now; }
        public override long GetTimestamp() { lock (gate) return (now - origin).Ticks; }

        internal int RegisteredCount { get { lock (gate) return timers.Count; } }

        internal Task RegisteredAsync(int ordinal)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
            lock (gate)
            {
                if (timers.Count > ordinal) return Task.CompletedTask;
                if (!registrations.TryGetValue(ordinal, out var ready))
                {
                    ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    registrations.Add(ordinal, ready);
                }
                return ready.Task;
            }
        }

        internal bool IsScheduled(int ordinal)
        {
            lock (gate) return timers[ordinal].Scheduled && !timers[ordinal].Disposed;
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ArgumentNullException.ThrowIfNull(callback);
            // This bounded clock serves the fixture's genuine 20ms Task.Delay calls only.
            Assert.Equal(TimeSpan.FromMilliseconds(20), dueTime);
            Assert.Equal(Timeout.InfiniteTimeSpan, period);
            var timer = new ScheduledTimer(this, callback, state);
            TaskCompletionSource<bool>? ready;
            lock (gate)
            {
                int ordinal = timers.Count;
                timers.Add(timer);
                ChangeLocked(timer, dueTime, period);
                registrations.TryGetValue(ordinal, out ready);
            }
            ready?.TrySetResult(true);
            return timer;
        }

        internal void FireScheduled(int ordinal)
        {
            ScheduledTimer timer;
            lock (gate)
            {
                timer = timers[ordinal];
                Assert.False(timer.Disposed);
                Assert.True(timer.Scheduled);
                Assert.Equal(Timeout.InfiniteTimeSpan, timer.Period);
                if (timer.Due > now) now = timer.Due;
                timer.Scheduled = false;
            }
            // Never invoke a genuine BCL callback while holding the registration gate.
            timer.Callback(timer.State);
        }

        private bool Change(ScheduledTimer timer, TimeSpan dueTime, TimeSpan period)
        {
            lock (gate)
            {
                if (timer.Disposed) return false;
                ChangeLocked(timer, dueTime, period);
                return true;
            }
        }

        private void ChangeLocked(ScheduledTimer timer, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime < Timeout.InfiniteTimeSpan) throw new ArgumentOutOfRangeException(nameof(dueTime));
            if (period < Timeout.InfiniteTimeSpan) throw new ArgumentOutOfRangeException(nameof(period));
            timer.Period = period;
            timer.Scheduled = dueTime != Timeout.InfiniteTimeSpan;
            timer.Due = timer.Scheduled ? now + dueTime : DateTimeOffset.MaxValue;
        }

        private void Dispose(ScheduledTimer timer)
        {
            lock (gate) { timer.Disposed = true; timer.Scheduled = false; }
        }

        private sealed class ScheduledTimer(Flag6ReviverScheduledTickClock owner, TimerCallback callback, object? state) : ITimer
        {
            internal TimerCallback Callback { get; } = callback;
            internal object? State { get; } = state;
            internal bool Scheduled;
            internal bool Disposed;
            internal DateTimeOffset Due;
            internal TimeSpan Period;
            public bool Change(TimeSpan dueTime, TimeSpan period) => owner.Change(this, dueTime, period);
            public void Dispose() => owner.Dispose(this);
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}

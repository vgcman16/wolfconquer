namespace WolfConquer.Tests;

public sealed partial class LegacyRuntimeReviverRaceTests
{
    // Only genuine Task.Delay registrations provide these callbacks. No fake
    // player/global invocation, callback replacement or start sentinel exists.
    private sealed class OrderedTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private readonly object gate = new();
        private readonly List<ManualTimer> timers = [];
        private TaskCompletionSource changed = NewSignal();
        private DateTimeOffset now = initial;
        public override DateTimeOffset GetUtcNow() { lock (gate) return now; }
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        internal int Count { get { lock (gate) return timers.Count; } }
        internal bool AllDisposed { get { lock (gate) return timers.All(timer => timer.Disposed); } }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ArgumentNullException.ThrowIfNull(callback);
            var timer = new ManualTimer(this, callback, state, dueTime, period);
            lock (gate) { timers.Add(timer); Pulse(); }
            return timer;
        }

        internal void Fire(int oneBasedIndex, TimeSpan advance)
        {
            ManualTimer timer;
            lock (gate)
            {
                now += advance;
                timer = timers[oneBasedIndex - 1];
                if (timer.Disposed || timer.Fired || timer.DueTime != TimeSpan.FromMilliseconds(20) ||
                    timer.Period != Timeout.InfiniteTimeSpan) throw new InvalidOperationException("Not an actual unfired runtime delay.");
                timer.Fired = true;
            }
            timer.Invoke();
        }

        internal async Task WaitForCountAsync(int count)
        {
            while (true)
            {
                Task signal;
                lock (gate) { if (timers.Count >= count) return; signal = changed.Task; }
                await signal;
            }
        }

        internal async Task WaitForDisposedAsync()
        {
            while (true)
            {
                Task signal;
                lock (gate) { if (timers.All(timer => timer.Disposed)) return; signal = changed.Task; }
                await signal;
            }
        }

        private void Pulse() { var prior = changed; changed = NewSignal(); prior.TrySetResult(); }
        private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        private sealed class ManualTimer(OrderedTimeProvider owner, TimerCallback callback, object? state,
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
                    DueTime = due; Period = interval; owner.Pulse(); return true;
                }
            }
            public void Dispose() { lock (owner.gate) { Disposed = true; owner.Pulse(); } }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}

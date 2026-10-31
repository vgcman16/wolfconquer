using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedWindWalkerQueryTests
{
    private sealed record WindNativeRow(int Key, LegacyNativeStatusFlagsBigVector32.Flag Row, int Seconds,
        int Invoker, DateTime Timer, DateTime InvokeTimer, bool RemoveOnDead);

    private static WindNativeRow[] NativeRows(ConcurrentDictionary<int, LegacyNativeStatusFlagsBigVector32.Flag> rows) =>
        rows.ToArray().Select(value => new WindNativeRow(value.Key, value.Value, value.Value.Secounds,
            value.Value.InvokerSecouds, value.Value.Timer, value.Value.TimerInvoker, value.Value.RemoveOnDead)).ToArray();

    private static void AssertNativeRows(WindNativeRow[] expected,
        ConcurrentDictionary<int, LegacyNativeStatusFlagsBigVector32.Flag> actual)
    {
        // ToArray avoids invoking the deliberately hostile comparer again.
        var rows = actual.ToArray(); Assert.Equal(expected.Length, rows.Length);
        foreach (var before in expected)
        {
            var after = Assert.Single(rows, value => value.Key == before.Key).Value;
            Assert.Same(before.Row, after); Assert.Equal(before.Key, after.Key); Assert.Equal(before.Seconds, after.Secounds);
            Assert.Equal(before.Invoker, after.InvokerSecouds); Assert.Equal(before.Timer, after.Timer);
            Assert.Equal(before.InvokeTimer, after.TimerInvoker); Assert.Equal(before.RemoveOnDead, after.RemoveOnDead);
        }
    }

    // Adversarial failure fixture only. A comparer can change escaped legacy
    // fields, but cannot issue an accepting proof or choose a commit result.
    private sealed class WindInterferenceComparer(int targetLookup, Action interfere, LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, LegacyNativeStatusFlagsBigVector32 vector) : IEqualityComparer<int>
    {
        private bool armed;
        private int lookups;
        internal bool Fired { get; private set; }
        internal bool FiredUnderActors { get; private set; }
        internal int FiredAtLookup { get; private set; }
        internal Exception? InterferenceFailure { get; private set; }
        internal void Arm() { lookups = 0; armed = true; }
        internal void Disarm() => armed = false;
        public bool Equals(int left, int right) => left == right;
        public int GetHashCode(int value)
        {
            if (armed && !Fired && value == Fan && ++lookups == targetLookup)
            {
                Fired = true; FiredAtLookup = lookups;
                FiredUnderActors = Monitor.IsEntered(vector.SyncRoot) && Monitor.IsEntered(client.SpellAdmissionSyncRoot) &&
                    Monitor.IsEntered(player.SpellAdmissionSyncRoot);
                try { interfere(); } catch (Exception failure) { InterferenceFailure = failure; }
            }
            return value;
        }
    }
}

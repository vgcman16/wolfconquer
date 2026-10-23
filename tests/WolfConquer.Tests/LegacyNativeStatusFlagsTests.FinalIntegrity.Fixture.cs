using System.Collections.Concurrent;
using Flags = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeStatusFlagsTests
{
    private static readonly string[] FinalIntegrityDriftKinds =
    ["seconds", "invoker", "timer", "invoke-timer", "death", "row-key", "deadline", "invoke-deadline",
        "word", "word-ref", "registry", "row-ref", "dictionary-key", "null-row"];

    // A real ConcurrentDictionary comparer is the exact potentially reentrant
    // boundary. It returns normally after a deliberate raw stimulus; no fake
    // status provider/commit callback or catching assertion is used.
    private sealed class FinalIntegrityComparer : IEqualityComparer<int>
    {
        internal bool Armed;
        internal bool ReturnedAfterStimulus;
        internal bool StimulusSucceeded = true;
        internal int TriggerOnRead;
        internal bool GateHeld;
        internal Action? Stimulus;
        internal List<int> Reads { get; } = [];
        public bool Equals(int left, int right) => left == right;
        public int GetHashCode(int value)
        {
            if (Armed)
            {
                Reads.Add(value);
                if (Reads.Count == TriggerOnRead)
                {
                    Armed = false;
                    Stimulus!();
                    ReturnedAfterStimulus = true;
                }
            }
            return value;
        }
    }

    private sealed class FinalIntegrityFixture
    {
        internal Flags Vector { get; } = new(64);
        internal FinalIntegrityComparer Comparer { get; } = new();
        internal int[] Keys { get; }
        internal VectorState? AfterStimulus { get; private set; }
        internal Flags.Flag[]? CacheAfterStimulus { get; private set; }

        internal FinalIntegrityFixture()
        {
            Assert.True(Vector.TryAdd(6, 7, false, 3));
            Assert.True(Vector.TryAdd(7, 11, true, 5));
            foreach (var row in Vector.ArrayFlags.Values)
                row.Timer = row.TimerInvoker = new DateTime(2000, 1, 1);
            Vector.ArrayFlags = new ConcurrentDictionary<int, Flags.Flag>(Vector.ArrayFlags, Comparer);
            Keys = Vector.ArrayFlags.ToArray().Select(pair => pair.Key).ToArray();
        }

        internal void Arm(string drift, int triggerOnRead)
        {
            var first = Vector.ArrayFlags.ToArray()[0];
            Comparer.TriggerOnRead = triggerOnRead;
            Comparer.Stimulus = () =>
            {
                Comparer.GateHeld = Monitor.IsEntered(Vector.SyncRoot);
                switch (drift)
                {
                    case "seconds": first.Value.Secounds++; break;
                    case "invoker": first.Value.InvokerSecouds++; break;
                    case "timer": first.Value.Timer = first.Value.Timer.AddTicks(1); break;
                    case "invoke-timer": first.Value.TimerInvoker = first.Value.TimerInvoker.AddTicks(1); break;
                    case "death": first.Value.RemoveOnDead = !first.Value.RemoveOnDead; break;
                    case "row-key": first.Value.Key = 17; break;
                    case "deadline": first.Value.Timer = DateTime.MaxValue; break;
                    case "invoke-deadline": first.Value.TimerInvoker = DateTime.MaxValue; break;
                    case "word": Vector.bits[0] ^= 1u << 17; break;
                    case "word-ref": Vector.bits = (uint[])Vector.bits.Clone(); break;
                    case "registry": Vector.ArrayFlags = new(Vector.ArrayFlags); break;
                    case "row-ref": Vector.ArrayFlags[first.Key] = CopyFinalIntegrityRow(first.Value, first.Key); break;
                    case "dictionary-key":
                        Comparer.StimulusSucceeded = Vector.ArrayFlags.TryRemove(first.Key, out _);
                        Vector.ArrayFlags[17] = CopyFinalIntegrityRow(first.Value, 17);
                        Vector.bits[0] = (Vector.bits[0] & ~(1u << first.Key)) | (1u << 17);
                        break;
                    case "null-row": Vector.ArrayFlags[first.Key] = null!; break;
                    default: throw new ArgumentOutOfRangeException(nameof(drift));
                }
                AfterStimulus = CaptureVector(Vector);
                CacheAfterStimulus = ReadPrivateCache(Vector);
            };
            Comparer.Armed = true;
        }

        internal void AssertOnlyStimulusRemains()
        {
            Assert.True(Comparer.ReturnedAfterStimulus); Assert.True(Comparer.GateHeld);
            Assert.True(Comparer.StimulusSucceeded);
            var after = Assert.IsType<VectorState>(AfterStimulus);
            AssertVectorUnchanged(Vector, after);
            Assert.Same(CacheAfterStimulus, ReadPrivateCache(Vector));
        }
    }

    private static Flags.Flag CopyFinalIntegrityRow(Flags.Flag row, int key) => new(key,
        row.Secounds, row.RemoveOnDead, row.InvokerSecouds) { Timer = row.Timer, TimerInvoker = row.TimerInvoker };
}

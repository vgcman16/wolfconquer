using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using RefineVector = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    private static readonly FieldInfo RefinementBin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly FieldInfo RefinementCache = typeof(RefineVector).GetField("Array", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo RefinementSeal = typeof(LegacyNativePacket).GetField("seal", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly FieldInfo RefinementSendQueue = typeof(LegacyNativeSecuritySocket).GetField("SendQueue", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private sealed class RefinementPool : IDisposable
    {
        private readonly object? prior = RefinementBin.GetValue(null);
        private readonly List<LegacyNativePacket> owned = [];
        internal ConcurrentQueue<LegacyNativePacket> Queue { get; } = new();
        internal RefinementPool() => RefinementBin.SetValue(null, Queue);
        internal unsafe LegacyNativePacket Make(int capacity = 512)
        {
            var packet = new LegacyNativePacket(capacity);
            Marshal.Copy(Enumerable.Repeat((byte)0xCB, capacity).ToArray(), 0, (IntPtr)packet.Memory, capacity);
            packet.Size = 7; packet.Seek(2); owned.Add(packet); return packet;
        }
        internal LegacyNativePacket Enqueue(int capacity = 512)
        {
            var packet = Make(capacity); Queue.Enqueue(packet); return packet;
        }
        public void Dispose()
        {
            RefinementBin.SetValue(null, prior);
            foreach (var packet in owned) packet.Dispose();
        }
    }

    private sealed record RefinementSavedRow(RefineVector.Flag Row, int Key, int Seconds, int Invoker,
        DateTime Timer, DateTime InvokeTimer, bool RemoveOnDeath);
    private sealed record RefinementStateSnapshot(RefineVector? Vector, object? Registrations,
        KeyValuePair<int, RefineVector.Flag>[] Entries, uint[]? Words, uint[]? WordBytes, object? Cache,
        RefinementSavedRow[] Rows, ushort Stamina, byte Booster, byte Killer, ushort Xp)
    {
        internal static RefinementStateSnapshot Capture(LegacyNativePlayerRuntime player)
        {
            var vector = player.BitVector;
            var entries = vector?.ArrayFlags?.ToArray() ?? [];
            return new(vector, vector?.ArrayFlags, entries, vector?.bits, vector?.bits?.ToArray(),
                vector is null ? null : RefinementCache.GetValue(vector),
                entries.Where(pair => pair.Value is not null).Select(pair => new RefinementSavedRow(pair.Value,
                    pair.Value.Key, pair.Value.Secounds, pair.Value.InvokerSecouds, pair.Value.Timer,
                    pair.Value.TimerInvoker, pair.Value.RemoveOnDead)).ToArray(),
                player.Stamina, player.XPBooster, player.XPKiller, player.XPCount);
        }

        internal void Unchanged(LegacyNativePlayerRuntime player)
        {
            Assert.Same(Vector, player.BitVector); Assert.Equal(Stamina, player.Stamina);
            Assert.Equal(Booster, player.XPBooster); Assert.Equal(Killer, player.XPKiller); Assert.Equal(Xp, player.XPCount);
            if (Vector is null) return;
            Assert.Same(Registrations, Vector.ArrayFlags);
            Assert.Equal(Entries, Vector.ArrayFlags?.ToArray() ?? []);
            Assert.Same(Words, Vector.bits); Assert.Equal(WordBytes, Vector.bits);
            Assert.Same(Cache, RefinementCache.GetValue(Vector));
            foreach (var saved in Rows)
            {
                Assert.Equal(saved.Key, saved.Row.Key); Assert.Equal(saved.Seconds, saved.Row.Secounds);
                Assert.Equal(saved.Invoker, saved.Row.InvokerSecouds); Assert.Equal(saved.Timer, saved.Row.Timer);
                Assert.Equal(saved.InvokeTimer, saved.Row.TimerInvoker); Assert.Equal(saved.RemoveOnDeath, saved.Row.RemoveOnDead);
            }
        }
    }

    private static bool AdmitRefinement(Scope scope, string kind) => kind switch
    {
        "mirror" => LegacyNativePerfectionEffects.TryMirrorOfSin(scope.Player),
        "killing" => LegacyNativePerfectionEffects.TryKillingFlash(scope.Client),
        "stamina" => LegacyNativePerfectionEffects.TryLightOfStamina(scope.Client),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static unsafe byte[]? RefinementStorage(LegacyNativePacket packet)
    {
        if (packet.Memory == null) return null;
        var bytes = new byte[packet.Capacity]; Marshal.Copy((IntPtr)packet.Memory, bytes, 0, bytes.Length); return bytes;
    }

    private sealed class RefinementComparer(RefinementPool pool, int finalReads) : IEqualityComparer<int>
    {
        internal bool Armed, Fired;
        internal int ReadsAfterRent;
        internal Action? OnFinalRead;
        internal List<int> Trace { get; } = [];
        public bool Equals(int x, int y) => x == y;
        public int GetHashCode(int value)
        {
            if (Armed)
            {
                Trace.Add(value);
                if (pool.Queue.IsEmpty && value == (int)LegacyNativeMsgUpdate.Flags.TeamLeader)
                {
                    ReadsAfterRent++;
                    if (!Fired && ReadsAfterRent == finalReads)
                    {
                        Fired = true; OnFinalRead?.Invoke();
                    }
                }
            }
            return value;
        }
    }
}

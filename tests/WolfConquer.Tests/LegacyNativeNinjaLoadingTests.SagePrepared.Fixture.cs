using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Vector = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    private static readonly FieldInfo SagePreparedBin = typeof(LegacyNativePacketRecycle)
        .GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly FieldInfo SagePreparedSeal = typeof(LegacyNativePacket)
        .GetField("seal", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly FieldInfo SagePreparedCache = typeof(Vector)
        .GetField("Array", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo SagePreparedSendQueue = typeof(LegacyNativeSecuritySocket)
        .GetField("SendQueue", BindingFlags.NonPublic | BindingFlags.Instance)!;

    // Only real allocated packets and the genuine recycle queue. No replacement
    // rental callback, serializer, accepting saver or transport result is injected.
    private sealed class SagePreparedPool : IDisposable
    {
        private readonly object? previous = SagePreparedBin.GetValue(null);
        private readonly List<LegacyNativePacket> owned = [];
        internal ConcurrentQueue<LegacyNativePacket> Queue { get; } = new();
        internal IReadOnlyList<LegacyNativePacket> Packets => owned;

        internal SagePreparedPool() => SagePreparedBin.SetValue(null, Queue);
        internal unsafe LegacyNativePacket Packet(int capacity)
        {
            var packet = new LegacyNativePacket(capacity);
            Marshal.Copy(Enumerable.Repeat((byte)0xD3, capacity).ToArray(), 0, (IntPtr)packet.Memory, capacity);
            packet.Size = 93; packet.Seek(11);
            owned.Add(packet);
            return packet;
        }
        internal void Enqueue(LegacyNativePacket? packet) => Queue.Enqueue(packet!);
        public void Dispose()
        {
            SagePreparedBin.SetValue(null, previous);
            foreach (var packet in owned) packet.Dispose();
        }
    }

    private sealed record SagePreparedRow(Vector.Flag Row, int Key, int Seconds, int Invoker,
        DateTime Timer, DateTime InvokeTimer, bool RemoveOnDeath);
    private sealed record SagePreparedState(Vector Vector, object Registrations,
        KeyValuePair<int, Vector.Flag>[] Entries, uint[] Words, uint[] Bytes, object? Cache,
        SagePreparedRow[] Rows, byte Booster, byte Killer)
    {
        internal static SagePreparedState Capture(LegacyNativePlayerRuntime player)
        {
            var vector = player.BitVector;
            var entries = vector.ArrayFlags.ToArray();
            return new(vector, vector.ArrayFlags, entries, vector.bits, vector.bits.ToArray(),
                SagePreparedCache.GetValue(vector), entries.Select(pair => new SagePreparedRow(pair.Value,
                    pair.Value.Key, pair.Value.Secounds, pair.Value.InvokerSecouds, pair.Value.Timer,
                    pair.Value.TimerInvoker, pair.Value.RemoveOnDead)).ToArray(), player.XPBooster, player.XPKiller);
        }
        internal void Unchanged(LegacyNativePlayerRuntime player)
        {
            Assert.Same(Registrations, Vector.ArrayFlags); Assert.Equal(Entries, Vector.ArrayFlags.ToArray());
            Assert.Same(Words, Vector.bits); Assert.Equal(Bytes, Vector.bits);
            Assert.Same(Cache, SagePreparedCache.GetValue(Vector));
            foreach (var saved in Rows)
            {
                Assert.Equal(saved.Key, saved.Row.Key); Assert.Equal(saved.Seconds, saved.Row.Secounds);
                Assert.Equal(saved.Invoker, saved.Row.InvokerSecouds); Assert.Equal(saved.Timer, saved.Row.Timer);
                Assert.Equal(saved.InvokeTimer, saved.Row.TimerInvoker); Assert.Equal(saved.RemoveOnDeath, saved.Row.RemoveOnDead);
            }
            Assert.Equal(Booster, player.XPBooster); Assert.Equal(Killer, player.XPKiller);
        }
    }

    private static unsafe byte[]? SagePreparedStorage(LegacyNativePacket packet)
    {
        if (packet.Memory == null) return null;
        var bytes = new byte[packet.Capacity];
        Marshal.Copy((IntPtr)packet.Memory, bytes, 0, bytes.Length);
        return bytes;
    }

    private static byte[] SagePreparedSealed(byte[] ordinary, string? seal)
    {
        if (seal is null) return ordinary;
        return ordinary.Concat(System.Text.Encoding.UTF8.GetBytes(seal).Take(8)).ToArray();
    }
}

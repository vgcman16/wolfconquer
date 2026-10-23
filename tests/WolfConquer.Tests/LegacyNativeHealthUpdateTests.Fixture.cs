using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeHealthUpdateTests
{
    // Exercises the actual socket enqueue and packet recycler, without a socket
    // pump, host adoption, Team facade, or writable production HP substitute.
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo BinField = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly FieldInfo HealthField = typeof(LegacyNativePlayerRuntime)
            .GetField("_minhitpoints", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private readonly object? previousBin = BinField.GetValue(null);
        private readonly LegacyNativeSafeRandom previousRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime previousReset = LegacyNativePoolRandom.ResetRandom;
        internal NativeSpellTestFixture Native { get; }
        internal List<LegacyNativePacket> Borrowed { get; } = new();
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal Queue<byte[]> Queue => Native.Queue;

        internal unsafe Scope(int packets = 1)
        {
            // Bind before native Player birth, which consumes the real shared RNG.
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(79);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            Native = new NativeSpellTestFixture();
            var ownedQueue = new ConcurrentQueue<LegacyNativePacket>();
            for (int i = 0; i < packets; i++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
                    0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
                packet.Size = 321;
                packet.Seek(237);
                Borrowed.Add(packet);
                ownedQueue.Enqueue(packet);
            }
            BinField.SetValue(null, ownedQueue);
        }

        internal void SetPreexistingHealth(uint maximum, int current)
        {
            Client.Status.MaxHitpoints = maximum;
            HealthField.SetValue(Player, current);
        }

        public void Dispose()
        {
            BinField.SetValue(null, previousBin);
            foreach (LegacyNativePacket packet in Borrowed)
            {
                packet.Dispose();
            }
            Native.Dispose();
            LegacyNativePoolRandom.GetRandom = previousRandom;
            LegacyNativePoolRandom.ResetRandom = previousReset;
        }
    }

    // Independent literal protobuf contract builder, not the production codec.
    // UID is required; scalar enum/value zero fields are omitted. The second
    // native frame repeats the first MaxHP entry before adding its HP entry.
    private static byte[] Frame(uint uid, uint maximum, int? current)
    {
        var body = new List<byte> { 0x08 };
        Varint(body, uid);
        Entry(body, 1, maximum);
        if (current.HasValue)
        {
            Entry(body, 0, unchecked((ulong)(long)current.Value));
        }
        byte[] result = new byte[body.Count + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(result, checked((ushort)result.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(2), 2176);
        body.CopyTo(result, 4);
        return result;
    }

    private static void Entry(List<byte> body, uint type, ulong value)
    {
        var entry = new List<byte>();
        if (type != 0) { entry.Add(0x08); Varint(entry, type); }
        if (value != 0) { entry.Add(0x10); Varint(entry, value); }
        body.Add(0x12);
        Varint(body, (ulong)entry.Count);
        body.AddRange(entry);
    }

    private static void Varint(List<byte> body, ulong value)
    {
        while (value >= 0x80)
        {
            body.Add((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }
        body.Add((byte)value);
    }
}

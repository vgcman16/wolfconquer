using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeLevelDependencyTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo BinField = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly FieldInfo ClassField = typeof(LegacyNativePlayerRuntime)
            .GetField("_class", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private readonly object? previousBin = BinField.GetValue(null);
        private readonly LegacyNativeSafeRandom previousRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime previousReset = LegacyNativePoolRandom.ResetRandom;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal Queue<byte[]> Queue => Native.Queue;
        internal List<LegacyNativePacket> Borrowed { get; } = new();

        internal unsafe Scope()
        {
            // Bind before physical Player birth, not after its real seed draw.
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(37);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            Native = new NativeSpellTestFixture();
            var packets = new ConcurrentQueue<LegacyNativePacket>();
            for (int i = 0; i < 8; i++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
                    0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
                packet.Size = 321;
                packet.Seek(237);
                Borrowed.Add(packet);
                packets.Enqueue(packet);
            }
            BinField.SetValue(null, packets);
        }

        internal void PreexistingClass(uint value) => ClassField.SetValue(Player, value);

        public void Dispose()
        {
            BinField.SetValue(null, previousBin);
            foreach (LegacyNativePacket packet in Borrowed) packet.Dispose();
            Native.Dispose();
            LegacyNativePoolRandom.GetRandom = previousRandom;
            LegacyNativePoolRandom.ResetRandom = previousReset;
        }
    }

    private static byte[] Update(uint type, ulong value)
    {
        var query = new List<byte> { 0x08 };
        Varint(query, type);
        if (value != 0) { query.Add(0x10); Varint(query, value); }
        var payload = new List<byte> { 0x08, 17, 0x12 };
        Varint(payload, (ulong)query.Count);
        payload.AddRange(query);
        return Frame(2176, payload.ToArray());
    }

    private static byte[] Frame(ushort opcode, byte[] payload)
    {
        byte[] frame = new byte[payload.Length + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)frame.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), opcode);
        payload.CopyTo(frame, 4);
        return frame;
    }

    private static byte[] Box(string? text)
    {
        byte[] encoded = text is null ? [] : Encoding.UTF8.GetBytes(text);
        byte[] payload = new byte[11 + encoded.Length];
        payload[4] = 0xFF; payload[5] = 0xFF;
        payload[7] = 6; payload[8] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(9), checked((ushort)encoded.Length));
        encoded.CopyTo(payload, 11);
        return Frame(2255, payload);
    }

    private static void Varint(List<byte> bytes, ulong value)
    {
        while (value >= 128)
        {
            bytes.Add((byte)((value & 127) | 128));
            value >>= 7;
        }
        bytes.Add((byte)value);
    }

    // Independent strict wire reader for the literal required MsgTalk contract.
    private static string[] OverflowGoldMessage(byte[] frame)
    {
        Assert.Equal(frame.Length, BinaryPrimitives.ReadUInt16LittleEndian(frame));
        Assert.Equal((ushort)2179, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
        int position = 4;
        ulong[] expected = [16776960uL, 2600uL, 0uL, 0uL, 0uL, 0uL, 0uL, 1uL, 0uL, 0uL];
        for (int field = 1; field <= 10; field++)
        {
            Assert.Equal((ulong)(field << 3), ReadVarint(frame, ref position));
            Assert.Equal(expected[field - 1], ReadVarint(frame, ref position));
        }
        var content = new string[7];
        for (int i = 0; i < content.Length; i++)
        {
            Assert.Equal(114uL, ReadVarint(frame, ref position));
            int length = checked((int)ReadVarint(frame, ref position));
            content[i] = Encoding.UTF8.GetString(frame.AsSpan(position, length));
            position += length;
        }
        Assert.Equal(frame.Length, position);
        return content;
    }

    private static ulong ReadVarint(byte[] bytes, ref int position)
    {
        ulong value = 0;
        for (int shift = 0; shift <= 63; shift += 7)
        {
            Assert.InRange(position, 0, bytes.Length - 1);
            byte current = bytes[position++];
            value |= (ulong)(current & 127) << shift;
            if ((current & 128) == 0) return value;
        }
        throw new InvalidOperationException("Malformed fixture packet varint.");
    }
}

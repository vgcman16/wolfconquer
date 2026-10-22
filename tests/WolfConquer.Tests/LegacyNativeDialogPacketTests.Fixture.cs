using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Reply = WolfConquer.Core.World.LegacyNativeNpcReply;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeDialogPacketTests : IDisposable
{
    private readonly LegacyNativeSafeRandom previousRandom = LegacyNativePoolRandom.GetRandom;

    public LegacyNativeDialogPacketTests()
    {
        // Restore the actual shared generator after genuine fixture Player birth.
        LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
    }

    public void Dispose() => LegacyNativePoolRandom.GetRandom = previousRandom;

    private sealed class Scope : IDisposable
    {
        internal NativeSpellTestFixture Native { get; } = new();
        internal LegacyNativePacket Packet => Native.Packet;
        internal LegacyNativePacket Second { get; }

        internal Scope()
        {
            Second = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
            Fill(Packet); Fill(Second);
        }

        internal static unsafe void Fill(LegacyNativePacket packet)
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(), 0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
            packet.Size = 321; packet.Seek(237);
        }

        internal unsafe void Input(byte[] bytes, int position = 237)
        {
            Assert.InRange(bytes.Length, 4, 512); Fill(Packet);
            Marshal.Copy(bytes, 0, (IntPtr)Packet.Memory, bytes.Length); Packet.Size = bytes.Length; Packet.Seek(position);
        }

        internal unsafe byte[] Raw(int offset, int length)
        {
            Assert.InRange(offset, 0, LegacyNativePacket.MAX_SIZE); Assert.InRange(length, 0, LegacyNativePacket.MAX_SIZE - offset);
            var bytes = new byte[length]; Marshal.Copy((IntPtr)(Packet.Memory + offset), bytes, 0, length); return bytes;
        }

        internal void Sent(params byte[][] expected)
        {
            Assert.Equal(expected.Length, Native.Queue.Count);
            for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], Native.Queue.ElementAt(i));
        }

        internal void Untouched()
        {
            Assert.Equal(321, Packet.Size); Assert.Equal(237, Packet.Position);
            Assert.All(NativeSpellTestFixture.Bytes(Packet), value => Assert.Equal((byte)0xA5, value));
        }

        public void Dispose() { Second.Dispose(); Native.Dispose(); } // Native restores the exact prior seal.
    }

    private static byte[] Frame(ushort opcode, byte[] payload)
    {
        var bytes = new byte[payload.Length + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)bytes.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), opcode); payload.CopyTo(bytes, 4); return bytes;
    }

    private static byte[] Npc(Reply.InteractTypes type, string? text, ushort inputLength = 0, byte option = 0, bool display = true)
    {
        byte[] value = text is null ? [] : Encoding.UTF8.GetBytes(text);
        var payload = new byte[8 + (display ? 3 + value.Length : 0)];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), inputLength); payload[6] = option; payload[7] = (byte)type;
        if (display)
        {
            payload[8] = 1; BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(9), checked((ushort)value.Length));
            value.CopyTo(payload, 11);
        }
        return Frame(2255, payload);
    }
}

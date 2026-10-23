using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeLoginWireTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Seal = typeof(LegacyNativePacket).GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? oldSeal = Seal.GetValue(null);
        internal readonly LegacyNativePacket Packet;
        internal unsafe Scope(bool sealedFrame)
        {
            Packet = new(LegacyNativePacket.MAX_SIZE);
            if (sealedFrame) LegacyNativePacket.SealString = "TQServer";
            else Seal.SetValue(null, null);
            Reset();
        }
        internal unsafe void Reset()
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xa5, LegacyNativePacket.MAX_SIZE).ToArray(), 0, (IntPtr)Packet.Memory, LegacyNativePacket.MAX_SIZE);
            Packet.Seek(237); Packet.Size = 321;
        }
        internal unsafe byte[] Bytes(int offset, int length)
        {
            Assert.InRange(offset, 0, LegacyNativePacket.MAX_SIZE);
            Assert.InRange(length, 0, LegacyNativePacket.MAX_SIZE - offset);
            byte[] bytes = new byte[length];
            Marshal.Copy((IntPtr)(Packet.Memory + offset), bytes, 0, length);
            return bytes;
        }
        internal void AssertFrame(byte[] expected)
        {
            Assert.Equal(expected.Length, Packet.Size); Assert.Equal(4, Packet.Position);
            Assert.Equal(expected, Bytes(0, expected.Length));
            Assert.All(Bytes(expected.Length, 16), b => Assert.Equal((byte)0xa5, b));
        }
        public void Dispose()
        {
            Seal.SetValue(null, oldSeal); Packet.Dispose();
        }
    }

    private static byte[] Frame(ushort opcode, byte[] payload, bool sealedFrame)
    {
        byte[] bytes = new byte[4 + payload.Length + (sealedFrame ? 8 : 0)];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)(4 + payload.Length)));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), opcode); payload.CopyTo(bytes, 4);
        if (sealedFrame) Encoding.ASCII.GetBytes("TQServer").CopyTo(bytes, bytes.Length - 8);
        return bytes;
    }

    private static byte[] Words(params uint[] words)
    {
        byte[] bytes = new byte[words.Length * 4];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4, 4), words[i]);
        return bytes;
    }
}

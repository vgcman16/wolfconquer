using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeNpcServerReplayTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Seal = typeof(LegacyNativePacket)
            .GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? priorSeal = Seal.GetValue(null);
        internal LegacyNativePacket Packet { get; } = new(LegacyNativePacket.MAX_SIZE);

        internal Scope()
        {
            Seal.SetValue(null, null);
            Fill();
        }

        internal unsafe void Fill()
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
                0, (IntPtr)Packet.Memory, LegacyNativePacket.MAX_SIZE);
            Packet.Size = 321; Packet.Seek(237);
        }

        internal unsafe void Seed(byte[] input, int position)
        {
            Assert.InRange(position, 0, LegacyNativePacket.MAX_SIZE);
            Assert.InRange(input.Length, 0, LegacyNativePacket.MAX_SIZE - position);
            Fill(); Marshal.Copy(input, 0, (IntPtr)(Packet.Memory + position), input.Length);
            Packet.Size = position + input.Length; Packet.Seek(position);
        }

        internal unsafe byte[] Raw(int offset, int count)
        {
            Assert.InRange(offset, 0, LegacyNativePacket.MAX_SIZE);
            Assert.InRange(count, 0, LegacyNativePacket.MAX_SIZE - offset);
            var bytes = new byte[count]; Marshal.Copy((IntPtr)(Packet.Memory + offset), bytes, 0, count);
            return bytes;
        }

        public void Dispose()
        {
            Packet.Dispose(); Seal.SetValue(null, priorSeal);
        }
    }

    private static byte[] Record(uint uid, ushort mesh, byte option, byte type, byte action, ushort declaredLength, byte[] value)
    {
        var bytes = new byte[11 + value.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, uid);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), mesh);
        bytes[6] = option; bytes[7] = type; bytes[8] = action;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(9), declaredLength);
        value.CopyTo(bytes, 11);
        return bytes;
    }
}

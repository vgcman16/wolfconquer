using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeQuestPacketTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Seal = typeof(LegacyNativePacket).GetField("seal", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? oldSeal = Seal.GetValue(null);
        internal LegacyNativePacket Packet { get; }
        internal Scope()
        {
            Packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE); Seal.SetValue(null, null); Fill();
        }
        internal unsafe void Fill()
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(), 0,
                (IntPtr)Packet.Memory, LegacyNativePacket.MAX_SIZE);
            Packet.Size = 321; Packet.Seek(237);
        }
        internal unsafe void Input(byte[] bytes, int position = 4)
        {
            Fill(); Marshal.Copy(bytes, 0, (IntPtr)Packet.Memory, bytes.Length); Packet.Size = bytes.Length; Packet.Seek(position);
        }
        internal void UseSeal(string seal) => LegacyNativePacket.SealString = seal;
        internal unsafe byte[] Raw(int offset, int length)
        {
            var bytes = new byte[length]; Marshal.Copy((IntPtr)(Packet.Memory + offset), bytes, 0, length); return bytes;
        }
        public void Dispose() { Packet.Dispose(); Seal.SetValue(null, oldSeal); }
    }
    private static byte[] Frame(ushort opcode, params uint[] words)
    {
        var bytes = new byte[4 + words.Length * 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)bytes.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), opcode);
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 + i * 4), words[i]);
        return bytes;
    }
}

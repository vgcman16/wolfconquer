using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

internal sealed class NativeInteractionTestScope : IDisposable
{
    private static readonly FieldInfo Seal = typeof(LegacyNativePacket).GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!;
    private readonly object? previous;
    internal LegacyNativePacket Packet { get; } = new(LegacyNativePacket.MAX_SIZE);

    internal NativeInteractionTestScope(bool sealedFrame = false)
    {
        previous = Seal.GetValue(null);
        Seal.SetValue(null, sealedFrame ? "TQServer" : null);
        Packet.ZeroFill(16383);
        Packet.Seek(0);
    }

    internal static unsafe byte[] Frame(LegacyNativePacket packet)
    {
        Assert.InRange(packet.Size, 0, 512); // Every source-shaped message here is bounded well below16KB.
        var frame = new byte[packet.Size];
        Marshal.Copy((IntPtr)packet.Memory, frame, 0, frame.Length);
        return frame;
    }

    // Independent test-only reader for this contract's scalar varints, not a generic production codec.
    internal static (int Field, ulong Value, int Bytes)[] Fields(byte[] payload)
    {
        var fields = new List<(int, ulong, int)>();
        int offset = 0;
        while (offset < payload.Length)
        {
            ulong tag = Read(payload, ref offset);
            Assert.Equal(0UL, tag & 7);
            int start = offset;
            ulong value = Read(payload, ref offset);
            fields.Add(((int)(tag >> 3), value, offset - start));
        }
        return fields.ToArray();
    }

    private static ulong Read(byte[] payload, ref int offset)
    {
        ulong value = 0;
        for (int shift = 0; shift < 70; shift += 7)
        {
            Assert.InRange(offset, 0, payload.Length - 1);
            byte current = payload[offset++];
            if (shift == 63) Assert.InRange(current, (byte)0, (byte)1);
            value |= (ulong)(current & 127) << shift;
            if ((current & 128) == 0) return value;
        }
        throw new InvalidOperationException("Malformed test payload.");
    }

    public void Dispose()
    {
        Packet.Dispose();
        Seal.SetValue(null, previous);
    }
}

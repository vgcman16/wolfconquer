#nullable disable
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Native framing plus a separate fixed projection of twelve CQ_COMBAT_GEAR contracts.
// Implicit message conversions use the genuine owner in the Object partial; typed
// Encode projections remain contract-test controls, not readiness or host adoption.
internal static partial class LegacyNativeCombatGearWire
{
    internal static byte[] Encode(LegacyNativeMsgCombatGear message, ushort opcode)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return null;
        ArgumentNullException.ThrowIfNull(message);
        using MemoryStream output = new MemoryStream();
        Serialize(output, message);
        return WrapObjectPayload(output, opcode);
    }

    internal static byte[] Encode(LegacyNativeMsgCombatGearSkin message, ushort opcode)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return null;
        ArgumentNullException.ThrowIfNull(message);
        using MemoryStream output = new MemoryStream();
        Serialize(output, message);
        return WrapObjectPayload(output, opcode);
    }

    internal static byte[] Encode(LegacyNativeMsgAirPowerUp message, ushort opcode)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return null;
        ArgumentNullException.ThrowIfNull(message);
        using MemoryStream output = new MemoryStream();
        Serialize(output, message);
        return WrapObjectPayload(output, opcode);
    }

    // Native TQCombatGear.Packet31..43 MemoryStream overload. Its input already
    // contains its own header; object framing instead reserves four bytes.
    internal static byte[] WrapStream(MemoryStream stream)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return null;
        ArgumentNullException.ThrowIfNull(stream);
        long length = stream.Length;
        if (length is < 4 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(stream), "Native frame must contain its four-byte header and fit the UInt16 length.");
        byte[] array = CopyFrameBytes(stream, length);
        byte[] array2 = new byte[checked((int)length + 8)];
        Buffer.BlockCopy(array, 0, array2, 0, array.Length);
        Write(checked((ushort)length), 0, array2);
        Write("TQServer", array2.Length - 8, array2);
        return array2;
    }

    private static byte[] WrapObjectPayload(MemoryStream stream, ushort opcode)
    {
        ArgumentNullException.ThrowIfNull(stream);
        long length = stream.Length;
        if (length is < 0 or > ushort.MaxValue - 4)
            throw new ArgumentOutOfRangeException(nameof(stream), "Native object payload plus its four-byte header must fit the UInt16 length.");
        byte[] array = CopyFrameBytes(stream, length);
        byte[] array2 = new byte[checked(12 + (int)length)];
        Buffer.BlockCopy(array, 0, array2, 4, array.Length);
        Write(checked((ushort)(length + 4)), 0, array2);
        Write(opcode, 2, array2);
        Write("TQServer", array2.Length - 8, array2);
        return array2;
    }

    private static byte[] CopyFrameBytes(MemoryStream stream, long expectedLength)
    {
        byte[] bytes = stream.ToArray();
        if (bytes is null || bytes.LongLength != expectedLength)
            throw new InvalidOperationException("Native frame stream changed while copying.");
        return bytes;
    }

    private static void Write(ushort arg, int offset, byte[] buffer)
    {
        if (buffer != null && offset <= buffer.Length - 1 && buffer.Length >= offset + 2)
        {
            buffer[offset] = (byte)arg;
            buffer[offset + 1] = (byte)(arg >> 8);
        }
    }

    private static void Write(string arg, int offset, byte[] buffer)
    {
        if (buffer != null && offset <= buffer.Length - 1)
        {
            byte[] bytes = Encoding.Default.GetBytes(arg);
            if (buffer.Length >= offset + arg.Length)
                Array.Copy(bytes, 0, buffer, offset, arg.Length);
        }
    }

    private static void Field(Stream output, uint number, ulong value)
    {
        Varint(output, (ulong)number << 3);
        Varint(output, value);
    }

    private static void Nested(Stream output, uint number, MemoryStream value)
    {
        byte[] bytes = value.ToArray();
        Varint(output, ((ulong)number << 3) | 2u);
        Varint(output, (ulong)bytes.Length);
        output.Write(bytes, 0, bytes.Length);
    }

    private static void Varint(Stream output, ulong value)
    {
        while (value >= 0x80)
        {
            output.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        output.WriteByte((byte)value);
    }
}

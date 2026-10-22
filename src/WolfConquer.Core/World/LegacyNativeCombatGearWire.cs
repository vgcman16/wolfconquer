#nullable disable
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Fixed projection of the twelve explicitly registered CQ_COMBAT_GEAR contracts, NOT
// a generic serializer or proof that native metadata Load/registration completed.
// Native m_Load is only a wire gate, never inferred readiness or a host binding.
internal static partial class LegacyNativeCombatGearWire
{
    internal static byte[] Encode(LegacyNativeMsgCombatGear message, ushort opcode)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return null;
        using MemoryStream output = new MemoryStream();
        Serialize(output, message);
        return WrapObjectPayload(output, opcode);
    }

    internal static byte[] Encode(LegacyNativeMsgCombatGearSkin message, ushort opcode)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return null;
        using MemoryStream output = new MemoryStream();
        Serialize(output, message);
        return WrapObjectPayload(output, opcode);
    }

    internal static byte[] Encode(LegacyNativeMsgAirPowerUp message, ushort opcode)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return null;
        using MemoryStream output = new MemoryStream();
        Serialize(output, message);
        return WrapObjectPayload(output, opcode);
    }

    // Native TQCombatGear.Packet31..43 MemoryStream overload. Its input already
    // contains its own header; the object overload below instead reserves four bytes.
    internal static byte[] WrapStream(MemoryStream stream)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return null;
        byte[] array = stream.ToArray();
        byte[] array2 = new byte[array.Length + 8];
        Buffer.BlockCopy(array, 0, array2, 0, array.Length);
        Write((ushort)array.Length, 0, array2);
        Write("TQServer", array2.Length - 8, array2);
        return array2;
    }

    private static byte[] WrapObjectPayload(MemoryStream stream, ushort opcode)
    {
        byte[] array = stream.ToArray();
        byte[] array2 = new byte[12 + array.Length];
        Buffer.BlockCopy(array, 0, array2, 4, array.Length);
        Write((ushort)(array2.Length - 8), 0, array2);
        Write(opcode, 2, array2);
        Write("TQServer", array2.Length - 8, array2);
        return array2;
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

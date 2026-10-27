using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class D13RandomDailyTaskPacket
{
    /// <summary>Writes present scalars1..7 ascending, then ordered unpacked UInt32 field8, as the native writer does.</summary>
    public byte[] ToPayloadBytes()
    {
        var bytes = new List<byte>(_encodedLength);
        LegacyProtoWire.WriteVarintField(bytes, 1, Action);
        WriteOptional(bytes, 2, Fields.Field2);
        WriteOptional(bytes, 3, Fields.Field3);
        WriteOptional(bytes, 4, Fields.Field4);
        WriteOptional(bytes, 5, Fields.Field5);
        if (Fields.Field6 is { } signed)
            LegacyProtoWire.WriteVarintField(bytes, 6, unchecked((ulong)(long)signed));
        WriteOptional(bytes, 7, Fields.Field7);
        foreach (var value in _values8)
            LegacyProtoWire.WriteVarintField(bytes, 8, value);
        return bytes.ToArray();
    }

    public byte[] ToFrameBytes()
    {
        var payload = ToPayloadBytes();
        var writer = new PacketWriter(Opcode, payload.Length, ushort.MaxValue);
        payload.AsSpan().CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static void WriteOptional(List<byte> bytes, int field, uint? value)
    {
        if (value is { } present)
            LegacyProtoWire.WriteVarintField(bytes, field, present);
    }

    private static int GetEncodedLength(uint action, D13RandomDailyTaskFields fields, ReadOnlySpan<uint> values8)
    {
        var length = 1 + VarintLength(action);
        length += OptionalLength(fields.Field2) + OptionalLength(fields.Field3) +
            OptionalLength(fields.Field4) + OptionalLength(fields.Field5) + OptionalLength(fields.Field7);
        if (fields.Field6 is { } signed)
            length += 1 + VarintLength(unchecked((ulong)(long)signed));
        foreach (var value in values8)
            length += 1 + VarintLength(value);
        return length;
    }

    private static int OptionalLength(uint? value) => value is { } present ? 1 + VarintLength(present) : 0;

    private static int VarintLength(ulong value)
    {
        var length = 1;
        while (value >= 0x80)
        {
            value >>= 7;
            length++;
        }
        return length;
    }
}
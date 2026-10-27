using System.Buffers;
using System.Runtime.InteropServices;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class D13RandomDailyTaskPacket
{
    /// <summary>Reads only the inspected schema; unsupported fields and malformed inputs return no packet.</summary>
    /// <remarks>Expects a coherent decoder-produced frame; checks opcode and payload, not arbitrary header/frame consistency.</remarks>
    public static bool TryParseKnownFrame(PacketFrame frame, out D13RandomDailyTaskPacket? packet)
    {
        packet = null;
        return frame.Header.Opcode == Opcode && frame.Payload.Length <= MaximumPayloadBytes &&
            TryParseKnownPayload(frame.Payload.ToArray(), out packet);
    }

    public static bool TryParseKnownPayload(ReadOnlySpan<byte> payload, out D13RandomDailyTaskPacket? packet)
    {
        packet = null;
        if (payload.Length > MaximumPayloadBytes) return false;
        var reader = new LegacyProtoWire.Reader(payload);
        var scalars = new uint?[8];
        int? field6 = null;
        var values8 = new List<uint>();
        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tag) || tag is 0 or > uint.MaxValue) return false;
            var field = (int)(tag >> 3);
            var wire = (int)(tag & 7);
            if (field is >= 1 and <= 7)
            {
                if (wire != LegacyProtoWire.VarintWireType) return false;
                if (field == 6)
                {
                    if (!TryReadInt32(ref reader, out var value)) return false;
                    field6 = value;
                }
                else
                {
                    if (!reader.TryReadUInt32(out var value)) return false;
                    scalars[field] = value;
                }
            }
            else if (field == 8)
            {
                if (!TryReadRepeated(ref reader, wire, values8)) return false;
            }
            else return false;
        }
        if (scalars[1] is not { } action) return false;
        var fields = new D13RandomDailyTaskFields(scalars[2], scalars[3], scalars[4], scalars[5], field6, scalars[7]);
        var values = CollectionsMarshal.AsSpan(values8);
        // Packed input and five-byte negative Int32 may expand in native canonical output.
        if (GetEncodedLength(action, fields, values) > MaximumPayloadBytes) return false;
        packet = new D13RandomDailyTaskPacket(action, fields, values);
        return true;
    }

    private static bool TryReadInt32(ref LegacyProtoWire.Reader reader, out int value)
    {
        value = 0;
        if (!reader.TryReadVarint(out var raw)) return false;
        // Preserve low DWORD storage bits and genuine negative64 sign extension;
        // unrelated widened values are refused under this bounded managed policy.
        if (raw > uint.MaxValue && raw < 0xFFFF_FFFF_8000_0000UL) return false;
        value = unchecked((int)(uint)raw);
        return true;
    }

    private static bool TryReadRepeated(ref LegacyProtoWire.Reader reader, int wire, List<uint> values)
    {
        if (wire == LegacyProtoWire.VarintWireType)
            return TryAppendUInt32(ref reader, values);
        if (wire != LegacyProtoWire.LengthDelimitedWireType || !reader.TryReadLengthDelimited(out var packed)) return false;
        var nested = new LegacyProtoWire.Reader(packed);
        while (!nested.End)
            if (!TryAppendUInt32(ref nested, values)) return false;
        return true;
    }

    private static bool TryAppendUInt32(ref LegacyProtoWire.Reader reader, List<uint> values)
    {
        if (values.Count == MaximumRepeatedValues || !reader.TryReadUInt32(out var value)) return false;
        values.Add(value);
        return true;
    }
}
using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class Chinese7935ItemInfoView
{
    /// <remarks>Requires a coherent decoder-produced frame; no arbitrary frame/header reconciliation is performed.</remarks>
    public static bool TryParseKnownFrame(PacketFrame frame, out Chinese7935ItemInfoView? view)
    {
        view = null;
        return frame.Header.Opcode == Opcode && frame.Payload.Length <= MaximumPayloadBytes &&
            TryParseKnownPayload(frame.Payload.ToArray(), out view);
    }

    public static bool TryParseKnownPayload(ReadOnlySpan<byte> payload, out Chinese7935ItemInfoView? view)
    {
        view = null;
        if (payload.Length > MaximumPayloadBytes) return false;
        var reader = new LegacyProtoWire.Reader(payload);
        var scalars = new ulong?[44];
        byte[]? data27 = null, data28 = null;
        var values31 = new List<int>();
        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tag) || tag is 0 or > uint.MaxValue) return false;
            var field = (int)(tag >> 3);
            var wire = (int)(tag & 7);
            var kind = FieldKind(field);
            if (kind == Chinese7935ItemInfoFieldKind.Bytes)
            {
                if (wire != LegacyProtoWire.LengthDelimitedWireType || !reader.TryReadLengthDelimited(out var data)) return false;
                if (field == 27) data27 = data.ToArray();
                else data28 = data.ToArray();
            }
            else if (kind == Chinese7935ItemInfoFieldKind.RepeatedInt32)
            {
                if (!TryReadRepeated(ref reader, wire, values31)) return false;
            }
            else
            {
                if (kind == Chinese7935ItemInfoFieldKind.Unsupported || wire != LegacyProtoWire.VarintWireType ||
                    !TryReadScalar(ref reader, kind, out var value)) return false;
                scalars[field] = value;
            }
        }
        if (scalars[1] is null || scalars[2] is null || scalars[3] is null) return false;
        view = new(scalars, data27, data28, values31.ToArray());
        return true;
    }

    private static bool TryReadScalar(ref LegacyProtoWire.Reader reader, Chinese7935ItemInfoFieldKind kind, out ulong? value)
    {
        value = null;
        if (kind == Chinese7935ItemInfoFieldKind.Unsupported || !reader.TryReadVarint(out var raw)) return false;
        if (kind == Chinese7935ItemInfoFieldKind.UInt32 && raw > uint.MaxValue) return false;
        if (kind == Chinese7935ItemInfoFieldKind.Int32)
        {
            if (!TryNarrowInt32(raw, out var signed)) return false;
            raw = unchecked((uint)signed);
        }
        value = raw;
        return true;
    }

    private static bool TryReadRepeated(ref LegacyProtoWire.Reader reader, int wire, List<int> values)
    {
        if (wire == LegacyProtoWire.VarintWireType)
            return TryAppendInt32(ref reader, values);
        if (wire != LegacyProtoWire.LengthDelimitedWireType || !reader.TryReadLengthDelimited(out var packed)) return false;
        var nested = new LegacyProtoWire.Reader(packed);
        while (!nested.End)
            if (!TryAppendInt32(ref nested, values)) return false;
        return true;
    }

    private static bool TryAppendInt32(ref LegacyProtoWire.Reader reader, List<int> values)
    {
        if (values.Count == MaximumRepeated31Values || !reader.TryReadVarint(out var raw) || !TryNarrowInt32(raw, out var value)) return false;
        values.Add(value);
        return true;
    }

    private static bool TryNarrowInt32(ulong raw, out int value)
    {
        value = 0;
        // Accept uint32 storage bits and proper ten-byte negative sign extension; refuse unrelated widened values.
        if (raw > uint.MaxValue && raw < 0xFFFF_FFFF_8000_0000UL) return false;
        value = unchecked((int)(uint)raw);
        return true;
    }
}

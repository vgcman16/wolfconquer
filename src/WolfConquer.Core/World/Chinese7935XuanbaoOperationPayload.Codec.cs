using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class Chinese7935XuanbaoOperationPayload
{
    public byte[] ToPayloadBytes()
    {
        var bytes = new List<byte>(EncodedLength());
        LegacyProtoWire.WriteVarintField(bytes, 1, Operation);
        var scalars = new[] { Fields.Field2, Fields.Field3, Fields.Field4, Fields.Field5, Fields.Field6, Fields.Field7 };
        for (var index = 0; index < scalars.Length; index++)
            if (scalars[index] is { } value) LegacyProtoWire.WriteVarintField(bytes, index + 2, value);
        if (_data8 is not null) LegacyProtoWire.WriteBytesField(bytes, 8, _data8);
        foreach (var child in _accInfo) LegacyProtoWire.WriteBytesField(bytes, 9, WriteChild(child));
        if (Fields.Field10 is { } last) LegacyProtoWire.WriteVarintField(bytes, 10, last);
        return bytes.ToArray();
    }

    /// <summary>Reads only this inspected schema; unsupported extension fields fail without an output.</summary>
    /// <remarks>Expects a coherent decoder-produced frame; checks its opcode and payload, not frame/header consistency.</remarks>
    public static bool TryParseKnownFrame(PacketFrame frame, out Chinese7935XuanbaoOperationPayload? packet)
    {
        packet = null;
        return frame.Header.Opcode == Opcode && frame.Payload.Length <= MaximumPayloadBytes &&
            TryParseKnownPayload(frame.Payload.ToArray(), out packet);
    }

    public static bool TryParseKnownPayload(ReadOnlySpan<byte> payload, out Chinese7935XuanbaoOperationPayload? packet)
    {
        packet = null;
        if (payload.Length > MaximumPayloadBytes) return false;
        var reader = new LegacyProtoWire.Reader(payload);
        uint?[] scalars = new uint?[11];
        byte[]? data8 = null;
        var children = new List<Chinese7935XuanbaoAccInfo>();
        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tag) || tag == 0 || tag > 0xFFFF_FFFFUL) return false;
            var field = tag >> 3;
            var wire = tag & 7;
            if (field is >= 1 and <= 7 or 10)
            {
                if (wire != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out var value)) return false;
                scalars[(int)field] = value;
            }
            else if (field == 8)
            {
                if (wire != LegacyProtoWire.LengthDelimitedWireType || !reader.TryReadLengthDelimited(out var data)) return false;
                data8 = data.ToArray();
            }
            else if (field == 9)
            {
                if (wire != LegacyProtoWire.LengthDelimitedWireType || children.Count == MaximumAccInfoEntries ||
                    !reader.TryReadLengthDelimited(out var data) || !TryReadChild(data, out var child)) return false;
                children.Add(child);
            }
            else return false;
        }
        if (scalars[1] is not { } operation) return false;
        packet = new Chinese7935XuanbaoOperationPayload(operation,
            new(scalars[2], scalars[3], scalars[4], scalars[5], scalars[6], scalars[7], scalars[10]),
            data8 is null ? (ReadOnlyMemory<byte>?)null : new ReadOnlyMemory<byte>(data8), children.ToArray());
        return true;
    }

    private static bool TryReadChild(ReadOnlySpan<byte> bytes, out Chinese7935XuanbaoAccInfo child)
    {
        child = default;
        uint? first = null, second = null;
        var reader = new LegacyProtoWire.Reader(bytes);
        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tag) || tag is not (8 or 16) || !reader.TryReadUInt32(out var value)) return false;
            if (tag == 8) first = value;
            else second = value;
        }
        child = new(first, second);
        return true;
    }

    private static byte[] WriteChild(Chinese7935XuanbaoAccInfo child)
    {
        var bytes = new List<byte>(Chinese7935XuanbaoWireSize.Child(child));
        if (child.Field1 is { } first) LegacyProtoWire.WriteVarintField(bytes, 1, first);
        if (child.Field2 is { } second) LegacyProtoWire.WriteVarintField(bytes, 2, second);
        return bytes.ToArray();
    }
}

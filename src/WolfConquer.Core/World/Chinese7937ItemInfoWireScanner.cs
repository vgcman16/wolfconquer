namespace WolfConquer.Core.World;

internal readonly record struct Chinese7937ItemWireRange(int FieldNumber, int WireType, int Offset, int Length)
{
    internal bool IsKnown => FieldNumber <= 43;
}

/// <summary>Finite structural wire delimiting only; nested group bytes never become top-level item fields.</summary>
internal static class Chinese7937ItemInfoWireScanner
{
    internal const int MaximumPayloadBytes = Chinese7937ItemInfoEnvelope.MaximumPayloadBytes;
    internal const int MaximumExtensions = Chinese7937ItemInfoEnvelope.MaximumExtensions;
    internal const int MaximumGroupDepth = Chinese7937ItemInfoEnvelope.MaximumGroupDepth;

    internal static bool TryScan(ReadOnlySpan<byte> payload, out Chinese7937ItemWireRange[] ranges,
        out Chinese7937ItemInfoEnvelopeFailure failure)
    {
        ranges = [];
        if (payload.Length > MaximumPayloadBytes)
        { failure = Chinese7937ItemInfoEnvelopeFailure.InputBounds; return false; }
        var reader = new Cursor(payload);
        List<Chinese7937ItemWireRange> prepared = [];
        var extensionCount = 0;
        while (!reader.End)
        {
            var start = reader.Offset;
            if (!reader.TryReadTag(out var field, out var wire) || wire == 4)
            { failure = Chinese7937ItemInfoEnvelopeFailure.InvalidWire; return false; }
            if (field > 43 && ++extensionCount > MaximumExtensions)
            { failure = Chinese7937ItemInfoEnvelopeFailure.ExtensionBounds; return false; }
            if (!reader.TrySkipValue(field, wire, 0, out failure)) return false;
            prepared.Add(new(field, wire, start, reader.Offset - start));
        }
        ranges = prepared.ToArray();
        failure = Chinese7937ItemInfoEnvelopeFailure.None;
        return true;
    }

    private ref struct Cursor(ReadOnlySpan<byte> payload)
    {
        private readonly ReadOnlySpan<byte> bytes = payload;
        private int offset;
        internal readonly int Offset => offset;
        internal readonly bool End => offset == bytes.Length;

        internal bool TryReadTag(out int field, out int wire)
        {
            field = 0; wire = 0;
            if (!TryReadVarint(out var tag) || tag is 0 or > uint.MaxValue || tag >> 3 == 0) return false;
            field = (int)(tag >> 3);
            wire = (int)(tag & 7);
            return true;
        }

        internal bool TrySkipValue(int field, int wire, int groupDepth, out Chinese7937ItemInfoEnvelopeFailure failure)
        {
            failure = Chinese7937ItemInfoEnvelopeFailure.InvalidWire;
            switch (wire)
            {
                case 0: return TryReadVarint(out _);
                case 1: return TrySkipBytes(sizeof(ulong));
                case 2:
                    if (!TryReadVarint(out var length) || length > (ulong)(bytes.Length - offset)) return false;
                    return TrySkipBytes((int)length);
                case 3:
                    if (groupDepth == MaximumGroupDepth)
                    { failure = Chinese7937ItemInfoEnvelopeFailure.ExtensionBounds; return false; }
                    return TrySkipGroup(field, groupDepth + 1, out failure);
                case 5: return TrySkipBytes(sizeof(uint));
                default: return false;
            }
        }

        private bool TrySkipGroup(int openingField, int groupDepth, out Chinese7937ItemInfoEnvelopeFailure failure)
        {
            failure = Chinese7937ItemInfoEnvelopeFailure.InvalidWire;
            while (!End)
            {
                if (!TryReadTag(out var field, out var wire)) return false;
                if (wire == 4) return field == openingField;
                if (!TrySkipValue(field, wire, groupDepth, out failure)) return false;
            }
            return false;
        }

        private bool TrySkipBytes(int length)
        {
            if (length < 0 || length > bytes.Length - offset) return false;
            offset += length;
            return true;
        }

        private bool TryReadVarint(out ulong value)
        {
            value = 0;
            for (var index = 0; index < 10; index++)
            {
                if (offset == bytes.Length) return false;
                var current = bytes[offset++];
                if (index == 9 && (current & 0xfe) != 0) return false;
                value |= (ulong)(current & 0x7f) << (index * 7);
                if ((current & 0x80) == 0) return true;
            }
            return false;
        }
    }
}

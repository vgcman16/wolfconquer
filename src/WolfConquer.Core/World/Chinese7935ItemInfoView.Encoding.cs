namespace WolfConquer.Core.World;

public sealed partial class Chinese7935ItemInfoView
{
    /// <summary>Writes only the known schema in native field order, with unpacked repeated31.</summary>
    public byte[] ToPayloadBytes()
    {
        if (!TryToPayloadBytes(out var bytes))
            throw new InvalidOperationException("Canonical item2350 output exceeds the finite payload bound.");
        return bytes!;
    }

    public bool TryToPayloadBytes(out byte[]? payload)
    {
        payload = null;
        var length = KnownEncodedLength();
        if (length > MaximumPayloadBytes) return false;
        var bytes = new List<byte>(length);
        for (var field = 1; field <= 43; field++)
        {
            if (field == 27 || field == 28)
            {
                var data = field == 27 ? _data27 : _data28;
                if (data is not null) LegacyProtoWire.WriteBytesField(bytes, field, data);
            }
            else if (field == 31)
            {
                foreach (var value in _values31)
                    LegacyProtoWire.WriteVarintField(bytes, field, unchecked((ulong)(long)value));
            }
            else if (_scalars[field] is { } value)
                LegacyProtoWire.WriteVarintField(bytes, field, CanonicalScalar(field, value));
        }
        payload = bytes.ToArray();
        return true;
    }

    private int KnownEncodedLength()
    {
        var length = 0;
        for (var field = 1; field <= 43; field++)
        {
            var tagLength = VarintLength((ulong)field << 3);
            if (field == 27 || field == 28)
            {
                var data = field == 27 ? _data27 : _data28;
                if (data is not null)
                    length = checked(length + tagLength + VarintLength((ulong)data.Length) + data.Length);
            }
            else if (field == 31)
            {
                foreach (var value in _values31)
                    length = checked(length + tagLength + VarintLength(unchecked((ulong)(long)value)));
            }
            else if (_scalars[field] is { } value)
                length = checked(length + tagLength + VarintLength(CanonicalScalar(field, value)));
        }
        return length;
    }

    private static ulong CanonicalScalar(int field, ulong value) =>
        FieldKind(field) == Chinese7935ItemInfoFieldKind.Int32 ? unchecked((ulong)(long)(int)(uint)value) : value;

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

namespace WolfConquer.Core.World;

public sealed partial class Chinese7935ItemInfoView
{
    // Forty known singular scalar fields, including the three required fields.
    public const int MaximumOptionalScalarFields = 37;

    /// <summary>Constructs complete owned known-schema data; does not choose a gameplay action or native owner.</summary>
    public static Chinese7935ItemInfoView Create(uint itemUid, uint field2, uint field3,
        ReadOnlySpan<Chinese7935ItemInfoScalar> optionalScalars = default,
        ReadOnlyMemory<byte>? data27 = null, ReadOnlyMemory<byte>? data28 = null,
        ReadOnlySpan<int> values31 = default)
    {
        if (optionalScalars.Length > MaximumOptionalScalarFields)
            throw new ArgumentOutOfRangeException(nameof(optionalScalars));
        if (values31.Length > MaximumRepeated31Values)
            throw new ArgumentOutOfRangeException(nameof(values31));
        if (data27 is { Length: > MaximumPayloadBytes } || data28 is { Length: > MaximumPayloadBytes })
            throw new ArgumentException("Raw item2350 bytes exceed the finite payload bound.");

        var captured = optionalScalars.ToArray();
        var scalars = PrepareScalars(itemUid, field2, field3, captured);
        var repeated = values31.ToArray();
        var view = new Chinese7935ItemInfoView(scalars, data27?.ToArray(), data28?.ToArray(), repeated);
        if (view.KnownEncodedLength() > MaximumPayloadBytes)
            throw new ArgumentException("Canonical item2350 output exceeds the finite payload bound.");
        return view;
    }

    private static ulong?[] PrepareScalars(uint itemUid, uint field2, uint field3,
        ReadOnlySpan<Chinese7935ItemInfoScalar> optionalScalars)
    {
        var scalars = new ulong?[44];
        scalars[1] = itemUid;
        scalars[2] = field2;
        scalars[3] = field3;
        foreach (var scalar in optionalScalars)
        {
            var kind = FieldKind(scalar.Field);
            if (scalar.Field is < 4 or > 43 ||
                kind is not (Chinese7935ItemInfoFieldKind.UInt32 or Chinese7935ItemInfoFieldKind.Int32 or Chinese7935ItemInfoFieldKind.UInt64) ||
                kind != scalar.Kind || scalars[scalar.Field].HasValue)
                throw new ArgumentException("Optional item2350 scalars must have unique matching known fields.", nameof(optionalScalars));
            scalars[scalar.Field] = scalar.Bits;
        }
        return scalars;
    }
}

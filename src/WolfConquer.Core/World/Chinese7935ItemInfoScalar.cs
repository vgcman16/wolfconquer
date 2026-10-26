namespace WolfConquer.Core.World;

/// <summary>A typed scalar in the inspected item2350 schema; not an item owner or action request.</summary>
public readonly record struct Chinese7935ItemInfoScalar
{
    private Chinese7935ItemInfoScalar(int field, Chinese7935ItemInfoFieldKind kind, ulong bits)
    {
        Field = field;
        Kind = kind;
        Bits = bits;
    }

    public int Field { get; }
    public Chinese7935ItemInfoFieldKind Kind { get; }
    internal ulong Bits { get; }

    public static Chinese7935ItemInfoScalar UInt32(int field, uint value)
    {
        RequireKind(field, Chinese7935ItemInfoFieldKind.UInt32);
        return new(field, Chinese7935ItemInfoFieldKind.UInt32, value);
    }

    public static Chinese7935ItemInfoScalar Int32(int field, int value)
    {
        RequireKind(field, Chinese7935ItemInfoFieldKind.Int32);
        return new(field, Chinese7935ItemInfoFieldKind.Int32, unchecked((uint)value));
    }

    public static Chinese7935ItemInfoScalar UInt64(int field, ulong value)
    {
        RequireKind(field, Chinese7935ItemInfoFieldKind.UInt64);
        return new(field, Chinese7935ItemInfoFieldKind.UInt64, value);
    }

    private static void RequireKind(int field, Chinese7935ItemInfoFieldKind kind)
    {
        if (Chinese7935ItemInfoView.FieldKind(field) != kind)
            throw new ArgumentException("The scalar type does not match the known item2350 field.", nameof(field));
    }
}

namespace WolfConquer.Core.World;

public enum Chinese7935ItemInfoFieldKind
{
    Unsupported,
    UInt32,
    Int32,
    UInt64,
    Bytes,
    RepeatedInt32,
}

public sealed partial class Chinese7935ItemInfoView
{
    public static Chinese7935ItemInfoFieldKind FieldKind(int field) => field switch
    {
        >= 1 and <= 10 or >= 12 and <= 21 or 23 or 26 or 29 or 32 or 33 or 42 => Chinese7935ItemInfoFieldKind.UInt32,
        11 or 22 or 24 or 25 or 30 or 34 or 35 or 37 or 38 or 39 or 40 => Chinese7935ItemInfoFieldKind.Int32,
        36 or 41 or 43 => Chinese7935ItemInfoFieldKind.UInt64,
        27 or 28 => Chinese7935ItemInfoFieldKind.Bytes,
        31 => Chinese7935ItemInfoFieldKind.RepeatedInt32,
        _ => Chinese7935ItemInfoFieldKind.Unsupported,
    };
}

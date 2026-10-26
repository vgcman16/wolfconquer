namespace WolfConquer.Core.World;

/// <summary>Optional uint32 fields of the inspected Chinese 7935-chain Xuanbao payload.</summary>
/// <remarks>Numbers are wire tags; unproved gameplay meanings are deliberately unnamed.</remarks>
public readonly record struct Chinese7935XuanbaoFields(
    uint? Field2 = null,
    uint? Field3 = null,
    uint? Field4 = null,
    uint? Field5 = null,
    uint? Field6 = null,
    uint? Field7 = null,
    uint? Field10 = null);

/// <summary>The two optional uint32 fields of native CXuanBaoAccInfoPB.</summary>
public readonly record struct Chinese7935XuanbaoAccInfo(uint? Field1 = null, uint? Field2 = null);

internal static class Chinese7935XuanbaoWireSize
{
    internal static int Varint(uint value)
    {
        var length = 1;
        while (value >= 128) { value >>= 7; length++; }
        return length;
    }

    internal static int Optional(uint? value) => value.HasValue ? 1 + Varint(value.Value) : 0;
    internal static int Child(Chinese7935XuanbaoAccInfo child) => Optional(child.Field1) + Optional(child.Field2);
}

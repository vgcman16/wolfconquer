namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static uint CheckedUInt32(long value, string name)
    {
        if (value is < 0 or > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy action field cannot be outside uint range.");
        }

        return (uint)value;
    }

    private static uint? TryGetLegacyParityMapId(long value)
    {
        return value is >= 0 and <= uint.MaxValue ? (uint)value : null;
    }

    private static ulong CheckedUInt64(long value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy action field cannot be outside ulong range.");
        }

        return (ulong)value;
    }

    private static int CheckedInt32(long value, string name)
    {
        if (value is < int.MinValue or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy action field cannot be outside int range.");
        }

        return (int)value;
    }

    private static ushort CheckedUInt16(long value, string name)
    {
        if (value is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy action field cannot be outside ushort range.");
        }

        return (ushort)value;
    }
}

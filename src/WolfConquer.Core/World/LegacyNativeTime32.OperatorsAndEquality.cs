#nullable disable

namespace WolfConquer.Core.World;

internal partial struct LegacyNativeTime32
{
    public override bool Equals(object obj)
    {
        return (obj is LegacyNativeTime32 time) ? (time == this) : base.Equals(obj);
    }

    public override string ToString()
    {
        return int_0.ToString();
    }

    public override int GetHashCode()
    {
        return int_0;
    }

    public static bool operator ==(LegacyNativeTime32 t1, LegacyNativeTime32 t2)
    {
        return t1.int_0 == t2.int_0;
    }

    public static bool operator !=(LegacyNativeTime32 t1, LegacyNativeTime32 t2)
    {
        return t1.int_0 != t2.int_0;
    }

    public static bool operator >(LegacyNativeTime32 t1, LegacyNativeTime32 t2)
    {
        return t1.int_0 > t2.int_0;
    }

    public static bool operator <(LegacyNativeTime32 t1, LegacyNativeTime32 t2)
    {
        return t1.int_0 < t2.int_0;
    }

    public static bool operator >=(LegacyNativeTime32 t1, LegacyNativeTime32 t2)
    {
        return t1.int_0 >= t2.int_0;
    }

    public static bool operator <=(LegacyNativeTime32 t1, LegacyNativeTime32 t2)
    {
        return t1.int_0 <= t2.int_0;
    }

    public static LegacyNativeTime32 operator -(LegacyNativeTime32 t1, LegacyNativeTime32 t2)
    {
        return new LegacyNativeTime32(unchecked(t1.int_0 - t2.int_0));
    }
}

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeRoleCore
{
    // Complete native Role.Core216..219 overload. This is the real shared Pool RNG,
    // not Calculate.Base's separate MyRandom owner or a forced-result provider.
    public static bool Rate(int value)
    {
        return value > LegacyNativePoolRandom.GetRandom.Next() % 100;
    }
}

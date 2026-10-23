namespace WolfConquer.Core.World;

// Native Calculate.Base shared RNG/Rate; complete sibling method families live in partials.
// Its shared RNG is distinct from Pool and BaseFunc.
internal static partial class LegacyNativeAttackBase
{
    public static LegacyNativeSafeRandom MyRandom = new LegacyNativeSafeRandom();

    public static bool Rate(int value)
    {
        return value > MyRandom.Next() % 100;
    }

    public static bool Rate(double value)
    {
        return value > (double)MyRandom.Next() % 100.0;
    }
}

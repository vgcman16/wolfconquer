namespace WolfConquer.Core.World;

internal static partial class LegacyNativeRoleCore
{
    // Complete native Role.Core89..93 overload, using the actual shared RNG.
    // The draw precedes modulo, including invalid discriminants and their faults.
    public static bool Rate(int value, int discriminant)
    {
        int num = LegacyNativePoolRandom.GetRandom.Next() % discriminant;
        return value > num;
    }
}

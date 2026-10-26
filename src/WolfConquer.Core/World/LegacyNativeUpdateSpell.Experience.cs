namespace WolfConquer.Core.World;

internal partial class LegacyNativeUpdateSpell
{
    private static bool TryPrepareSpellExperience(int current, uint damage, uint rate, out int experience)
    {
        experience = 0;
        if (current < 0)
            return false;

        // Both factors are UInt32; their complete product fits UInt64. The
        // signed Int32 record and wire field define the admissible XP range.
        ulong scaledDamage = (ulong)damage * rate;
        if (scaledDamage > (ulong)(int.MaxValue - current))
            return false;

        experience = current + (int)scaledDamage;
        return true;
    }
}

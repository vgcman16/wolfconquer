namespace WolfConquer.Core.World;

public static partial class LegacyMagicSupportRuntime
{
    private static bool IsRecruitSupportSort(LegacyMagicSort sort)
    {
        return (int)sort == 2;
    }

    private static bool IsAddManaSupportSpell(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AddMana && spellId == 1_195;
    }

    private static bool IsReviveSupportSpell(LegacyMagicSort sort, ushort spellId)
    {
        return (int)sort == 7 && (spellId == 0 ||
            spellId is 1_050 or 1_100 or 16_610);
    }

    private static bool IsSingleTargetDetachSpell(ushort spellId)
    {
        return spellId == 6_004;
    }

    private static bool IsSingleTargetHealingSpell(ushort spellId)
    {
        return spellId is 1_005 or 1_190;
    }

    private static bool IsDispatchXpSort(LegacyMagicSort sort, ushort spellId)
    {
        return (int)sort == 11 ||
            spellId is 1_040 or 12_280;
    }

    private static bool IsSummonPetSort(LegacyMagicSort sort)
    {
        return (int)sort == 23;
    }
}

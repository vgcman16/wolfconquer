namespace WolfConquer.Core.World;

// Closed DATA combinations shared by preflight and the real native query.
internal static class LegacyEquipmentMagicArithmetic
{
    internal static LegacyNativeMagicQueryFields Start(long spirit) => new(checked((uint)spirit), 0, 0, 0);

    internal static LegacyNativeMagicQueryFields Add(LegacyNativeMagicQueryFields current, LegacyNativeMagicQueryFields add) => current with
    {
        MagicAttack = checked(current.MagicAttack + add.MagicAttack),
        MagicPercent = checked(current.MagicPercent + add.MagicPercent),
        SkillCStrike = checked(current.SkillCStrike + add.SkillCStrike),
        MagicDamageIncrease = checked(current.MagicDamageIncrease + add.MagicDamageIncrease),
    };

    internal static LegacyNativeMagicQueryFields Witchery(LegacyNativeMagicQueryFields current, byte level)
    {
        if (current.MagicAttack >= 15_000) return current;
        uint addition = level switch
        { 1 => 2_000U, 2 => 2_300U, 3 => 2_600U, 4 => 3_000U, 5 => 3_500U,
            6 => 4_000U, 7 => 4_500U, 8 => 5_000U, 9 => 6_000U, _ => 0U };
        return current with { MagicAttack = Math.Min(15_000U, checked(current.MagicAttack + addition)) };
    }

    internal static LegacyNativeMagicQueryFields NatureChant(LegacyNativeMagicQueryFields current, byte level)
    {
        uint percent = level switch
        { 1 => 13U, 2 => 14U, 3 => 15U, 4 => 16U, 5 => 17U, 6 => 18U, 7 => 19U,
            8 => 22U, 9 => 24U, _ => 0U };
        return current with { MagicAttack = checked(current.MagicAttack + checked(current.MagicAttack / 100 * percent)) };
    }

    internal static LegacyNativeMagicQueryFields BeforeGear(LegacyNativeMagicQueryFields current, bool mount, bool garment) =>
        Add(current, new(checked((mount ? 1_000U : 0U) + (garment ? 600U : 0U) + 1), 0, 0, current.ThunderGemValue));
}

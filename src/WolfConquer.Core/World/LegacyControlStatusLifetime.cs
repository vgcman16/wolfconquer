namespace WolfConquer.Core.World;

internal static class LegacyControlStatusLifetime
{
    internal const string FrightenedExpiresAtUtcAttribute = "LegacyFrightenedExpiresAtUtc";
    internal const string FrightenedRemoveOnDeadAttribute = "LegacyFrightenedRemoveOnDead";

    // Dust Sigil and boss Dizzy both map to flag 0x3A, so they must share one timer.
    internal const string DizzyExpiresAtUtcAttribute = "LegacyDustSigilStunnExpiresAtUtc";
    internal const string DizzyRemoveOnDeadAttribute = "LegacyDustSigilStunnRemoveOnDead";

    internal static string GetExpiresAtUtcAttribute(LegacyBossStatusEffect effect)
    {
        return effect.StatusFlag switch
        {
            LegacyBossStatusEffect.FrightenedFlag => FrightenedExpiresAtUtcAttribute,
            LegacyBossStatusEffect.DizzyFlag => DizzyExpiresAtUtcAttribute,
            _ => throw new ArgumentOutOfRangeException(nameof(effect)),
        };
    }

    internal static string GetRemoveOnDeadAttribute(LegacyBossStatusEffect effect)
    {
        return effect.StatusFlag switch
        {
            LegacyBossStatusEffect.FrightenedFlag => FrightenedRemoveOnDeadAttribute,
            LegacyBossStatusEffect.DizzyFlag => DizzyRemoveOnDeadAttribute,
            _ => throw new ArgumentOutOfRangeException(nameof(effect)),
        };
    }
}

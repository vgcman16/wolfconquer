namespace WolfConquer.Core.World;

public readonly record struct LegacyBossStatusEffect(
    int StatusFlag,
    int BlockingFlag,
    uint DurationSeconds,
    LegacyGameUpdateDataType DataType)
{
    public const int FrightenedFlag = 0x36;
    public const int DizzyFlag = 0x3A;

    private const uint DefaultDurationSeconds = 3;

    public static bool TryGetForSpell(ushort spellId, out LegacyBossStatusEffect effect)
    {
        switch (spellId)
        {
            case 7_013:
            case 7_014:
            case 10_362:
            case 10_531:
                effect = Frightened(DefaultDurationSeconds);
                return true;
            case 7_015:
            case 7_017:
            case 11_322:
            case 30_013:
            case 30_014:
                effect = Dizzy(DefaultDurationSeconds);
                return true;
            default:
                effect = default;
                return false;
        }
    }

    public static LegacyBossStatusEffect Frightened(uint durationSeconds)
    {
        return new LegacyBossStatusEffect(
            FrightenedFlag,
            DizzyFlag,
            durationSeconds,
            LegacyGameUpdateDataType.Flustered);
    }

    public static LegacyBossStatusEffect Dizzy(uint durationSeconds)
    {
        return new LegacyBossStatusEffect(
            DizzyFlag,
            FrightenedFlag,
            durationSeconds,
            LegacyGameUpdateDataType.Stun);
    }
}

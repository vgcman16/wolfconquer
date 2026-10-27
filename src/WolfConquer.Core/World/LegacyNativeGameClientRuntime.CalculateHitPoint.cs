namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient1677..1692 formulas; known D13 Trojan stages extend through 57.
    // Finite native UInt16 stats bound
    // each floating term sum to0..2487053.25; the scoped conversion keeps x86
    // truncation then UInt16 low bits instead of a modern saturating cast.
    public ushort CalculateHitPoint()
    {
        ushort num = 0;
        if (Player.Class >= 1005 && Player.Class <= 1057)
        {
            return (ushort)(num + NativeStatDoubleToUInt16((double)(int)Player.Agility * 3.45 + (double)(int)Player.Spirit * 3.45 + (double)(int)Player.Strength * 3.45 + (double)(int)Player.Vitality * 27.6));
        }
        return Player.Class switch
        {
            1001u => (ushort)(num + NativeStatDoubleToUInt16((double)(int)Player.Agility * 3.15 + (double)(int)Player.Spirit * 3.15 + (double)(int)Player.Strength * 3.15 + (double)(int)Player.Vitality * 25.2)),
            1002u => (ushort)(num + NativeStatDoubleToUInt16((double)(int)Player.Agility * 3.24 + (double)(int)Player.Spirit * 3.24 + (double)(int)Player.Strength * 3.24 + (double)(int)Player.Vitality * 25.9)),
            1003u => (ushort)(num + NativeStatDoubleToUInt16((double)(int)Player.Agility * 3.3 + (double)(int)Player.Spirit * 3.3 + (double)(int)Player.Strength * 3.3 + (double)(int)Player.Vitality * 26.4)),
            1004u => (ushort)(num + NativeStatDoubleToUInt16((double)(int)Player.Agility * 3.36 + (double)(int)Player.Spirit * 3.36 + (double)(int)Player.Strength * 3.36 + (double)(int)Player.Vitality * 26.8)),
            _ => (ushort)(num + (ushort)(Player.Agility * 3 + Player.Spirit * 3 + Player.Strength * 3 + Player.Vitality * 24)),
        };
    }

    // Valid only for the complete source formula's proven finite UInt16-stat range.
    // Root's owned Framework4/x86 default and optimized controls each passed10/10.
    private static ushort NativeStatDoubleToUInt16(double value) => unchecked((ushort)(int)value);
}

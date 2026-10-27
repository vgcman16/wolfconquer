namespace WolfConquer.Core.World;

internal partial class LegacyNativePerfectionEffect
{
    // Preserve this physical owner's identity. These plain field stores do not
    // promise an atomic sixteen-field observation by unrelated concurrent readers.
    internal void ApplyPrepared(LegacyNativePerfectionEffect prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ToxinEraser = prepared.ToxinEraser;
        LightOfStamina = prepared.LightOfStamina;
        DivineGuard = prepared.DivineGuard;
        CalmWind = prepared.CalmWind;
        LuckyStrike = prepared.LuckyStrike;
        StrikeLock = prepared.StrikeLock;
        FreeSoul = prepared.FreeSoul;
        StraightLife = prepared.StraightLife;
        DrainingTouch = prepared.DrainingTouch;
        BloodSpawn = prepared.BloodSpawn;
        CoreStrike = prepared.CoreStrike;
        KillingFlash = prepared.KillingFlash;
        MirrorOfSin = prepared.MirrorOfSin;
        InvisibleArrow = prepared.InvisibleArrow;
        ShieldBreak = prepared.ShieldBreak;
        AbsoluteLuck = prepared.AbsoluteLuck;
    }
}

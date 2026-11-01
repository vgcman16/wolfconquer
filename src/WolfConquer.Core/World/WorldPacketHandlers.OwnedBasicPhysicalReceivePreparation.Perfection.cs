namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class OwnedBasicPhysicalReceiveData
    {
        private string? OwnedPhysicalPerfectionBranch()
        {
            var actor = ExpectedActor.Character; var target = ExpectedTarget.Character;
            if (Positive(ReadLegacyPlayerReceiveMirrorOfSinPercent(actor), actorPerfection!.MirrorOfSin) ||
                Positive(ReadLegacyPlayerReceiveCalmWindPercent(actor), actorPerfection.CalmWind) ||
                Positive(ReadLegacyPlayerReceiveKillingFlashPercent(actor), actorPerfection.KillingFlash) ||
                Positive(ReadLegacyPlayerReceiveDrainingTouchPercent(actor), actorPerfection.DrainingTouch)) return "attackerPerfectionProc";
            var prestigeAllows = ReadLegacyPlayerReceivePrestigeLevel(target) >= ReadLegacyPlayerReceivePrestigeLevel(actor) ||
                targetPerfection!.Prestige >= actorPerfection.Prestige;
            if (prestigeAllows && (Positive(ReadLegacyPlayerReceiveLightOfStaminaPercent(actor), actorPerfection.LightOfStamina) ||
                Positive(ReadLegacyPlayerReceiveDivineGuardPercent(actor), actorPerfection.DivineGuard))) return "prestigePerfectionProc";
            if (Positive(ReadLegacyPlayerReceiveBloodSpawnPercent(target), targetPerfection!.BloodSpawn)) return "targetBloodSpawnProc";
            return null;
        }

        private static bool Positive(double managed, double native) => managed > 0 || native > 0;
    }

    // A negative applicability witness, not a Perfection publisher. Keep both
    // the real query cache and escaped physical owner exact; no zero substitute
    // may hide an applicable source proc from the pre-writer refusal.
    private sealed class OwnedPhysicalPerfectionInputs
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativePerfectionEffect physical;
        private readonly PerfectionValues before;
        private readonly LegacyNativePerfectionSnapshot cached;
        internal readonly uint Prestige;
        internal double LightOfStamina => Math.Max(before.LightOfStamina, cached.LightOfStamina);
        internal double DivineGuard => Math.Max(before.DivineGuard, cached.DivineGuard);
        internal double CalmWind => Math.Max(before.CalmWind, cached.CalmWind);
        internal double DrainingTouch => Math.Max(before.DrainingTouch, cached.DrainingTouch);
        internal double BloodSpawn => Math.Max(before.BloodSpawn, cached.BloodSpawn);
        internal double KillingFlash => Math.Max(before.KillingFlash, cached.KillingFlash);
        internal double MirrorOfSin => Math.Max(before.MirrorOfSin, cached.MirrorOfSin);

        internal OwnedPhysicalPerfectionInputs(LegacyOrdinarySpellBinding binding)
        {
            this.binding = binding;
            physical = binding.Client.PerfectionStatus ?? throw new InvalidDataException("ordinaryPhysicalPerfectionOwnerMissing");
            before = PerfectionValues.Read(physical);
            cached = LegacyNativePerfectionClient.Read(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity);
            Prestige = LegacyNativePerfectionClient.Prestige(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity);
            if (!before.IsFiniteNonnegative || !IsCurrentPlain())
                throw new InvalidDataException("ordinaryPhysicalPerfectionInputInvalid");
        }

        internal bool IsCurrentPlain() => ReferenceEquals(binding.Client.PerfectionStatus, physical) &&
            PerfectionValues.Read(physical) == before &&
            ReferenceEquals(LegacyNativePerfectionClient.Read(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity), cached) &&
            LegacyNativePerfectionClient.Prestige(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity) == Prestige;
    }

    private readonly record struct PerfectionValues(double ToxinEraser, double LightOfStamina, double DivineGuard,
        double CalmWind, double LuckyStrike, double StrikeLock, double FreeSoul, double StraightLife, double DrainingTouch,
        double BloodSpawn, double CoreStrike, double KillingFlash, double MirrorOfSin, double InvisibleArrow,
        double ShieldBreak, double AbsoluteLuck)
    {
        internal static PerfectionValues Read(LegacyNativePerfectionEffect value) => new(value.ToxinEraser,
            value.LightOfStamina, value.DivineGuard, value.CalmWind, value.LuckyStrike, value.StrikeLock, value.FreeSoul,
            value.StraightLife, value.DrainingTouch, value.BloodSpawn, value.CoreStrike, value.KillingFlash, value.MirrorOfSin,
            value.InvisibleArrow, value.ShieldBreak, value.AbsoluteLuck);
        internal bool IsFiniteNonnegative => Valid(ToxinEraser) && Valid(LightOfStamina) && Valid(DivineGuard) &&
            Valid(CalmWind) && Valid(LuckyStrike) && Valid(StrikeLock) && Valid(FreeSoul) && Valid(StraightLife) &&
            Valid(DrainingTouch) && Valid(BloodSpawn) && Valid(CoreStrike) && Valid(KillingFlash) && Valid(MirrorOfSin) &&
            Valid(InvisibleArrow) && Valid(ShieldBreak) && Valid(AbsoluteLuck);
        private static bool Valid(double value) => double.IsFinite(value) && value >= 0;
    }
}

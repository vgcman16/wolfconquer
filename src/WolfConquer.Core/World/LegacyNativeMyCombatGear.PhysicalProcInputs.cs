using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    // A read witness from the real constructor and published ordinary-admission
    // origin. The Frame separately owns the request, table publisher and file.
    internal sealed class PhysicalProcInputs
    {
        private readonly LegacyNativeMyCombatGear gear;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly CombatGearSnapshot saved;
        private readonly MailboxGearState native;
        private readonly byte[] image;

        private PhysicalProcInputs(LegacyNativeMyCombatGear gear, LegacyOrdinarySpellBinding binding,
            CombatGearSnapshot saved, MailboxGearState native, byte[] image)
        { this.gear = gear; this.binding = binding; this.saved = saved; this.native = native; this.image = image; }

        internal static PhysicalProcInputs? Capture(LegacyOrdinarySpellBinding binding, WorldSession current)
        {
            if (!Held(binding) || binding.Client.CombatGear is not { } gear ||
                !gear.IsCombatOwnedBy(binding.Client) || !gear.IsAdmissionOriginCurrent(binding, current) ||
                current.CombatGear is not { } saved) return null;
            var copy = CloneMailboxGearSnapshot(saved);
            var native = MailboxGearState.Capture(gear, copy.Items.Count, copy.BlackJadeBag.Count);
            var image = MailboxGearImage(copy);
            if (!MailboxGearImage(native.ToSnapshot(copy)).AsSpan().SequenceEqual(image)) return null;
            var result = new PhysicalProcInputs(gear, binding, saved, native, image);
            return result.IsCurrentPlain(current) ? result : null;
        }

        internal bool IsCurrentPlain(WorldSession current) => Held(binding) &&
            ReferenceEquals(current.CombatGear, saved) && gear.IsCombatOwnedBy(binding.Client) &&
            gear.IsAdmissionOriginCurrent(binding, current) &&
            native.IsCurrent(gear, native.Scalars.PirateExp, native.Air.Points) &&
            MailboxGearImage(CloneMailboxGearSnapshot(saved)).AsSpan().SequenceEqual(image);

        internal bool MatchesSavedImage(byte[] bytes) =>
            LegacyTradeJournal.TryReadCompleteMailboxGear(bytes, out var parsed) && parsed is not null &&
            MailboxGearImage(parsed).AsSpan().SequenceEqual(image);

        private static bool Held(LegacyOrdinarySpellBinding binding) =>
            Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) &&
            Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot);
    }
}

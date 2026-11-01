using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    private sealed class DragonhowlFlashingSlot { internal OwnedDragonhowlFlashingLease? Current; }
    private static readonly ConditionalWeakTable<LegacyOrdinarySpellBinding, DragonhowlFlashingSlot> DragonhowlFlashingSlots = new();

    internal static bool HasOwnedDragonhowlFlashing(LegacyOrdinarySpellBinding binding) =>
        DragonhowlFlashingSlots.TryGetValue(binding, out var slot) && slot.Current is not null;

    internal static bool TryCaptureOwnedDragonhowlFlashing(LegacyOrdinarySpellBinding binding,
        WorldSession expected, out OwnedDragonhowlFlashingLease? lease)
    {
        lease = null;
        if (!DragonhowlFlashingSlots.TryGetValue(binding, out var slot) || slot.Current is not { } current ||
            !current.IsCurrentPlain(expected)) return false;
        lease = current; return true;
    }

    internal sealed partial class OwnedDragonhowlFlashingLease
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativeStatusFlagsBigVector32 vector;
        private readonly DragonhowlFlashingSlot slot;
        private readonly Flag row;
        private readonly LegacyCanAttackFlashingRegistration registration;
        private readonly TimeProvider clock;
        private bool published, retired;

        private OwnedDragonhowlFlashingLease(LegacyOrdinarySpellBinding binding,
            LegacyNativeStatusFlagsBigVector32 vector, Flag row,
            LegacyCanAttackFlashingRegistration registration, TimeProvider clock)
        {
            this.binding = binding; this.vector = vector; this.row = row;
            this.registration = registration; this.clock = clock;
            slot = DragonhowlFlashingSlots.GetValue(binding, static _ => new());
            if (slot.Current is not null) throw InvalidLease();
        }

        internal static OwnedDragonhowlFlashingLease Prepare(LegacyOrdinarySpellBinding binding,
            LegacyNativeStatusFlagsBigVector32 vector, Flag row,
            LegacyCanAttackFlashingRegistration registration, TimeProvider clock) =>
            new(binding, vector, row, registration, clock);

        internal LegacyCanAttackFlashingRegistration Registration => registration;
        internal bool IsDueNow => LegacyCanAttackFlashingRegistration.LocalTime(clock, clock.GetUtcNow()) >= registration.ExpiresAtLocal;
        internal bool UsesClock(TimeProvider expected) => ReferenceEquals(clock, expected);
        internal bool IsPreparedPlain() => !published && !retired && slot.Current is null && RowMatches();

        internal sealed class OriginPublication
        {
            private readonly OwnedDragonhowlFlashingLease lease;
            private OriginPublication(OwnedDragonhowlFlashingLease lease) => this.lease = lease;
            internal static OriginPublication Prepare(OwnedDragonhowlFlashingLease lease,
                DragonhowlFlashingPreparation native, WorldPacketHandlers.OwnedDragonhowlPreparation plan,
                int index, WorldSession expected, WorldSession proposed)
            {
                if (!lease.IsPreparedPlain() || !native.IsPreparedLease(lease) ||
                    !ReferenceEquals(proposed.CanAttackFlashingRegistration, lease.registration) ||
                    !lease.binding.Store.IsActualDragonhowlFlashingPhase(plan, index, lease.binding,
                        expected, proposed, native)) throw InvalidLease();
                return new(lease);
            }
            internal bool IsCurrentPlain() => lease.IsPreparedPlain();
            internal void PublishPrepared()
            {
                lease.slot.Current = lease;
                lease.published = true;
            }
        }

        internal bool IsCurrentPlain(WorldSession expected) => IsNativeCurrentPlain() &&
            expected.AccountId == binding.AccountId && expected.Character.Uid == binding.Uid &&
            ReferenceEquals(expected.NativeGameClient, binding.Client) &&
            ReferenceEquals(expected.CanAttackFlashingRegistration, registration) &&
            registration.IsValidFor(expected.Character) &&
            LegacyStatusFlagDeathRegistration.TryRead(expected.Character, 0, out var remove) && remove;

        internal bool IsNativeCurrentPlain()
        {
            if (!published || retired || !ReferenceEquals(slot.Current, this) || !RowMatches() ||
                !NativeOwnerCurrent() || !vector.TryCapture(out var current) || current is null ||
                !ReferenceEquals(FindCapturedFlag(current, 0), row)) return false;
            return vector.HasUnchangedCapturedGraph(current);
        }

        private bool NativeOwnerCurrent() => WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate &&
            Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
            Monitor.IsEntered(vector.SyncRoot) && !binding.IsRetired &&
            ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) && ReferenceEquals(binding.Client.Player, binding.Player) &&
            ReferenceEquals(binding.Player.Owner, binding.Client) && ReferenceEquals(binding.Player.BitVector, vector) &&
            binding.Player.UID == binding.Uid && binding.Store.IsBoundSpellOwnerCurrent(binding);

        private bool RowMatches() => row.Key == 0 && row.Secounds == 30 && row.InvokerSecouds == 0 &&
            row.Timer == registration.Timer && row.TimerInvoker == default && row.RemoveOnDead;

        private static InvalidOperationException InvalidLease() => new("ownedDragonhowlFlashingRegistrationChanged");
    }
}

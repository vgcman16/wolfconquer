using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    internal sealed partial class OwnedDragonhowlFlashingLease
    {
        internal ExpiryPublication PrepareExpiry(WorldServerFrameHandler frame,
            WorldServerFrameHandler.OwnedDragonhowlFlashingExpiry purpose, WorldSession before, WorldSession after) =>
            ExpiryPublication.Prepare(this, frame, purpose, before, after);

        internal sealed class ExpiryPublication
        {
            private readonly OwnedDragonhowlFlashingLease lease;
            private readonly WorldServerFrameHandler frame;
            private readonly WorldServerFrameHandler.OwnedDragonhowlFlashingExpiry purpose;
            private readonly WorldSession before, proposed;
            private readonly State beforeNative, afterNative;
            private readonly PreparedState staged;
            private bool committed;

            private ExpiryPublication(OwnedDragonhowlFlashingLease lease, WorldServerFrameHandler frame,
                WorldServerFrameHandler.OwnedDragonhowlFlashingExpiry purpose, WorldSession before, WorldSession proposed)
            {
                this.lease = lease; this.frame = frame; this.purpose = purpose; this.before = before; this.proposed = proposed;
                if (!lease.IsCurrentPlain(before) || !lease.IsDueNow ||
                    !frame.IsActualDragonhowlFlashingExpiry(purpose, lease.binding, before, lease) ||
                    !HasRemovedProjection(proposed) || !lease.vector.TryCapture(out var captured) || captured is null)
                    throw InvalidLease();
                beforeNative = captured;
                var rows = captured.Rows.Where(value => value.Key != 0).ToArray();
                var registrations = new ConcurrentDictionary<int, Flag>(rows.Select(value =>
                    new KeyValuePair<int, Flag>(value.Key, value.Row)), captured.Registrations.Comparer);
                var words = (uint[])captured.SnapshotWords.Clone(); words[0] &= ~1U;
                afterNative = new(registrations, words, (uint[])words.Clone(), rows);
                staged = new(captured, registrations, words, registrations.Values.ToArray());
                if (!IsCurrentPlain()) throw InvalidLease();
            }

            internal static ExpiryPublication Prepare(OwnedDragonhowlFlashingLease lease, WorldServerFrameHandler frame,
                WorldServerFrameHandler.OwnedDragonhowlFlashingExpiry purpose, WorldSession before, WorldSession proposed) =>
                new(lease, frame, purpose, before, proposed);

            internal bool IsPreparedFor(LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession after) =>
                ReferenceEquals(binding, lease.binding) && ReferenceEquals(expected, before) &&
                ReferenceEquals(after.Character, proposed.Character) && HasRemovedProjection(after) && IsCurrentPlain();
            internal bool IsCurrentPlain() => !committed && lease.IsCurrentPlain(before) && lease.IsDueNow &&
                lease.vector.HasUnchangedCapturedGraph(beforeNative) &&
                frame.IsActualDragonhowlFlashingExpiry(purpose, lease.binding, before, lease);
            internal bool ConsumeBeforePublication() => IsCurrentPlain() &&
                frame.ConsumeDragonhowlFlashingExpiry(purpose, lease.binding, before, lease);
            internal void PublishPrepared()
            {
                lease.vector.CommitPreparedState(staged);
                lease.slot.Current = null; lease.retired = true; committed = true;
            }
            internal bool IsPublishedCurrentPlain() => committed && lease.retired && lease.slot.Current is null &&
                lease.NativeOwnerCurrent() && lease.vector.HasUnchangedCapturedGraph(afterNative);

            private static bool HasRemovedProjection(WorldSession after) => after.CanAttackFlashingRegistration is null &&
                (LegacyWindWalkerStatusProjection.Read(after.Character)[0] & 1UL) == 0 &&
                !after.Character.Attributes.Keys.Any(key => key.Equals(LegacyStatusFlagDeathRegistration.Key(0), StringComparison.OrdinalIgnoreCase));
        }
    }
}

using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    internal static OwnedDragonhowlFlashingLease? CaptureOwnedPhysicalDeathFlashing(
        LegacyOrdinarySpellBinding binding, WorldSession expected, WindWalkerSnapshot snapshot)
    {
        var vector = binding.Player.BitVector;
        if (!Monitor.IsEntered(vector.SyncRoot) || !vector.IsWindWalkerCurrentPlain(snapshot) ||
            !vector.windWalkerSnapshots.TryGetValue(snapshot, out var before)) throw InvalidDeathFlashing();
        if (FindCapturedFlag(before, 0) is null)
        {
            if (HasOwnedDragonhowlFlashing(binding)) throw InvalidDeathFlashing();
            return null;
        }
        // Neither a raw native row nor a managed saved bit can issue a lease.
        if (!TryCaptureOwnedDragonhowlFlashing(binding, expected, out var lease) || lease is null ||
            !vector.HasUnchangedCapturedGraph(before)) throw InvalidDeathFlashing();
        return lease;
    }

    internal sealed partial class OwnedDragonhowlFlashingLease
    {
        internal DeathRetirement PrepareDeathRetirement(
            WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation, WorldSession canonical) =>
            DeathRetirement.Prepare(this, preparation, canonical);

        internal sealed class DeathRetirement
        {
            private readonly OwnedDragonhowlFlashingLease lease;
            private readonly WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation;
            private readonly WorldSession canonical;
            private readonly LegacyNativeAttributeMapProof canonicalFields;
            private bool published;

            private DeathRetirement(OwnedDragonhowlFlashingLease lease,
                WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation, WorldSession canonical)
            {
                this.lease = lease; this.preparation = preparation; this.canonical = canonical;
                canonicalFields = LegacyNativeAttributeMapProof.CaptureRaw(canonical.Character.Attributes);
            }

            internal static DeathRetirement Prepare(OwnedDragonhowlFlashingLease lease,
                WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation, WorldSession canonical)
            {
                if (!preparation.TryReadNativeFlashingDeathProposal(lease.binding, canonical, out var actual) ||
                    !ReferenceEquals(actual, lease)) throw InvalidDeathFlashing();
                var result = new DeathRetirement(lease, preparation, canonical);
                if (!result.IsCurrentPlain()) throw InvalidDeathFlashing();
                return result;
            }

            internal bool IsCurrentPlain() => !published && canonicalFields.IsCurrentPlain() &&
                preparation.TryReadNativeFlashingDeathProposal(lease.binding, canonical, out var actual) &&
                ReferenceEquals(actual, lease);

            // The actual joint death publication has consumed all before proofs
            // and committed the single composed native graph. No lookups remain.
            internal void PublishPrepared()
            {
                lease.slot.Current = null;
                lease.retired = true;
                published = true;
            }

            internal bool IsPublishedCurrentPlain() => published && lease.published && lease.retired &&
                lease.slot.Current is null && lease.NativeOwnerCurrent() && canonicalFields.IsCurrentPlain() &&
                canonical.CanAttackFlashingRegistration is null &&
                !LegacyPlayerDeathState.HasStatusFlag(canonical.Character.Attributes, 0);
        }
    }

    // This participant does not commit a second full vector. Its prepared graph
    // is the actual194 transition's after graph with only the owned row0 removed.
    private sealed class OwnedPhysicalDeathFlashingPublication
    {
        private readonly LegacyNativeStatusFlagsBigVector32 vector;
        private readonly State before;
        private readonly OwnedDragonhowlFlashingLease lease;
        private readonly OwnedDragonhowlFlashingLease.DeathRetirement retirement;
        internal readonly State After;
        internal readonly PreparedState Staged;

        private OwnedPhysicalDeathFlashingPublication(LegacyNativeStatusFlagsBigVector32 vector,
            State before, State windAfter, OwnedDragonhowlFlashingLease lease,
            WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation, WorldSession canonical)
        {
            this.vector = vector; this.before = before; this.lease = lease;
            retirement = lease.PrepareDeathRetirement(preparation, canonical);
            var original = FindCapturedFlag(before, 0);
            if (original is null || !ReferenceEquals(original, FindCapturedFlag(windAfter, 0)) ||
                !ReferenceEquals(windAfter.Registrations.Comparer, EqualityComparer<int>.Default)) throw InvalidDeathFlashing();
            var registrations = new ConcurrentDictionary<int, Flag>(windAfter.Registrations,
                windAfter.Registrations.Comparer);
            if (!registrations.TryRemove(0, out var removed) || !ReferenceEquals(removed, original)) throw InvalidDeathFlashing();
            var words = (uint[])windAfter.SnapshotWords.Clone();
            words[0] &= ~1u;
            After = new(registrations, words, (uint[])words.Clone(), windAfter.Rows.Where(row => row.Key != 0).ToArray());
            Staged = new(before, registrations, words, registrations.Values.ToArray());
            if (!IsCurrentPlain()) throw InvalidDeathFlashing();
        }

        internal static OwnedPhysicalDeathFlashingPublication? Prepare(LegacyOrdinarySpellBinding binding,
            WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation, WorldSession canonical,
            LegacyNativeStatusFlagsBigVector32 vector, WindWalkerSnapshot captured, State? windAfter)
        {
            if (!preparation.TryReadNativeFlashingDeathProposal(binding, canonical, out var lease)) throw InvalidDeathFlashing();
            if (lease is null) return null;
            if (!vector.windWalkerSnapshots.TryGetValue(captured, out var before) ||
                !vector.HasUnchangedCapturedGraph(before) ||
                !WindWalkerGraphMatchesPlain(windAfter ?? before)) throw InvalidDeathFlashing();
            return new(vector, before, windAfter ?? before, lease, preparation, canonical);
        }

        internal bool IsCurrentPlain() => lease.IsNativeCurrentPlain() && retirement.IsCurrentPlain() &&
            vector.HasUnchangedCapturedGraph(before) && WindWalkerGraphMatchesPlain(After);

        internal void PublishPrepared() => retirement.PublishPrepared();

        internal bool IsPublishedCurrentPlain() => vector.HasUnchangedCapturedGraph(After) &&
            retirement.IsPublishedCurrentPlain();
    }

    private static InvalidOperationException InvalidDeathFlashing() => new("ordinaryPhysicalDeathFlashingChanged");
}

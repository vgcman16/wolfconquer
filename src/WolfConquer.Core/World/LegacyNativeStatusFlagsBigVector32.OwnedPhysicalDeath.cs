namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    internal bool IsOwnedPhysicalDeathWindWalkerPreparationCurrent(WindWalkerChange change) =>
        Monitor.IsEntered(SyncRoot) && windWalkerChanges.TryGetValue(change, out var prepared) &&
        windWalkerOrigins.TryGetValue(change, out var origin) && !origin.Committed &&
        origin.Kind == WindWalkerPreparationKind.Query && HasUnchangedCapturedGraph(prepared.Before) &&
        WindWalkerGraphMatchesPlain(prepared.After);

    // A separate actual-death purpose. It never manufactures the ordinary
    // query's completed transport token or accepts a desired-bit argument.
    internal sealed class OwnedPhysicalDeathWindWalkerPublication
    {
        private readonly LegacyNativeStatusFlagsBigVector32 vector;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation;
        private readonly WorldSession canonical;
        private readonly WindWalkerSnapshot before;
        private readonly WindWalkerChange? change;
        private readonly WindWalkerPreparedState? transition;
        private readonly PreparedState? staged;
        private readonly PreparedChange? nativeChange;
        private readonly PreparedState? nativePrepared;
        private readonly WindWalkerOrigin? origin;
        private readonly WorldSessionStore.OwnedDeathWindWalkerRegistration registration;
        private readonly OwnedPhysicalDeathFlashingPublication? flashing;
        private bool consumed, published;

        private OwnedPhysicalDeathWindWalkerPublication(LegacyOrdinarySpellBinding binding,
            WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation, WorldSession canonical)
        {
            this.binding = binding; this.preparation = preparation; this.canonical = canonical;
            if (!preparation.TryReadNativeWindWalkerProposal(binding, canonical, out vector, out before, out change) ||
                !ReferenceEquals(binding.Player.BitVector, vector)) throw InvalidDeathWindWalker();
            if (change is not null)
            {
                if (!vector.IsOwnedPhysicalDeathWindWalkerPreparationCurrent(change) ||
                    !vector.windWalkerChanges.TryGetValue(change, out transition) ||
                    !vector.windWalkerOrigins.TryGetValue(change, out origin)) throw InvalidDeathWindWalker();
                if (transition.Change is { } native)
                {
                    if (!vector.pendingChanges.TryGetValue(native, out nativePrepared)) throw InvalidDeathWindWalker();
                    nativeChange = native; staged = nativePrepared;
                }
            }
            flashing = OwnedPhysicalDeathFlashingPublication.Prepare(binding, preparation, canonical,
                vector, before, transition?.After);
            if (flashing is not null) staged = flashing.Staged;
            registration = WorldSessionStore.OwnedDeathWindWalkerRegistration.Prepare(binding, preparation, canonical);
            if (!IsCurrentPlain()) throw InvalidDeathWindWalker();
        }

        internal static OwnedPhysicalDeathWindWalkerPublication Prepare(LegacyOrdinarySpellBinding binding,
            WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation, WorldSession canonical) =>
            new(binding, preparation, canonical);

        internal bool IsCurrentPlain() => !published && NativeOwnerCurrentPlain() &&
            preparation.IsCurrentPlain(binding) && vector.IsWindWalkerCurrentPlain(before) &&
            (change is null || vector.IsOwnedPhysicalDeathWindWalkerPreparationCurrent(change)) &&
            (consumed || nativeChange is null || vector.pendingChanges.TryGetValue(nativeChange, out var pending) &&
                ReferenceEquals(pending, nativePrepared)) &&
            (flashing is null || flashing.IsCurrentPlain()) && registration.IsCurrentPlain();

        // Preparation (including whole-proc preflight) only observes the actual
        // staged native graph. The real writer alone consumes its one-use proof.
        internal bool ConsumeBeforeWriterPlain()
        {
            if (consumed || !IsCurrentPlain()) return false;
            if (nativeChange is not null && (!vector.TryTakeCurrent(nativeChange, out var actual) ||
                !ReferenceEquals(actual, nativePrepared))) return false;
            consumed = true;
            // TryTakeCurrent can invoke the actual registration comparer. Keep
            // the complete plain before proof after that last possible callout.
            return IsCurrentPlain();
        }

        internal bool IsPreparedFor(LegacyOrdinarySpellBinding actualBinding,
            WorldPacketHandlers.OwnedPhysicalDeathPreparation actualPreparation, WorldSession actualCanonical) =>
            ReferenceEquals(binding, actualBinding) && ReferenceEquals(preparation, actualPreparation) &&
            ReferenceEquals(canonical, actualCanonical) && IsCurrentPlain();

        // All before proofs are consumed by the actual joint Store owner before
        // any native store. This continuation has no lookup/callback/allocation.
        internal void PublishPrepared()
        {
            if (!consumed) throw InvalidDeathWindWalker();
            if (staged is not null) vector.CommitPreparedState(staged);
            if (origin is not null) origin.Committed = true;
            flashing?.PublishPrepared();
            registration.PublishPrepared();
            published = true;
        }

        internal bool IsPublishedCurrentPlain() => published && NativeOwnerCurrentPlain() &&
            (flashing is not null ? flashing.IsPublishedCurrentPlain() :
                transition is not null ? vector.HasUnchangedCapturedGraph(transition.After) : vector.IsWindWalkerCurrentPlain(before)) &&
            (origin is null || origin.Committed) && registration.IsPublishedCurrentPlain();

        private bool NativeOwnerCurrentPlain() => WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate &&
            Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
            Monitor.IsEntered(vector.SyncRoot) && !binding.IsRetired &&
            ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) && ReferenceEquals(binding.Client.Player, binding.Player) &&
            ReferenceEquals(binding.Player.Owner, binding.Client) && ReferenceEquals(binding.Player.BitVector, vector) &&
            binding.Player.UID == binding.Uid && binding.Store.IsBoundSpellOwnerCurrent(binding);

        private static InvalidOperationException InvalidDeathWindWalker() => new("ordinaryPhysicalDeathWindWalkerChanged");
    }
}

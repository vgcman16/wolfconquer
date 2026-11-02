using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOrdinarySpellBinding
{
    private LegacySavedVigorSeed? savedVigor;
    private VigorReceipt? vigorReceipt;

    // Read-only provenance, never a writer. Only a real completed query and
    // later Store-authorized publication can install a receipt.
    internal sealed record VigorReceipt(WorldSession Session, LegacyNativeAttributeMapProof Map,
        uint Value, LegacyNativeMagicQueryClient.CompletedMaximums? Completion,
        LegacyVigorCatalogWitness? Metadata);

    internal sealed class PreparedVigorRetention
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly VigorReceipt before, after;
        private PreparedVigorRetention(LegacyOrdinarySpellBinding binding, VigorReceipt before, VigorReceipt after)
        { this.binding = binding; this.before = before; this.after = after; }
        internal bool IsCurrentPlain() => ReferenceEquals(binding.vigorReceipt, before) &&
            binding.VigorRetentionIsCurrent(before, after);
        internal static PreparedVigorRetention Create(LegacyOrdinarySpellBinding binding, VigorReceipt before, VigorReceipt after) =>
            new(binding, before, after);
        internal void PublishActual(WorldSessionStore.OwnedVigorPublication permit)
        {
            if (!WorldSessionStore.ConsumeVigorPublication(permit, binding, this)) throw InvalidVigor();
            binding.vigorReceipt = after;
        }
    }

    internal void InitializePreparedVigorSeed(LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner,
        byte[] image, WorldSession proposed)
    {
        if (savedVigor is not null || IsRetired || !WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
            !owner.BeforeOwnerIsCurrentPlain(Client, Player) || owner.PreparedUid != Uid ||
            !ReferenceEquals(proposed.NativeGameClient, Client) || proposed.AccountId != AccountId ||
            proposed.Character.Uid != Uid || Client.OrdinarySpellBinding is not null) throw InvalidVigor();
        savedVigor = new LegacySavedVigorSeed(image, proposed.Character);
        var map = LegacyNativeAttributeMapProof.CaptureRaw(proposed.Character.Attributes);
        uint value = LegacyEquipmentQueryBounds.ReadVigor(proposed.Character);
        if (!map.IsCurrentPlain()) throw InvalidVigor();
        // Detached data comes from the actual admission image and real prefix.
        // It is usable only once this exact object is the published bound holder.
        vigorReceipt = new(proposed, map, value, null, null);
    }

    internal bool MatchesSavedVigorSeed(byte[] image)
    {
        try { return savedVigor is not null && savedVigor.Matches(image); }
        catch (Exception error) when (error is InvalidDataException or ArgumentException or FormatException) { return false; }
    }

    internal void RecordBoundQueryVigor(WorldSession current, ushort value,
        LegacyNativeMagicQueryClient.CompletedMaximums completion, LegacyVigorCatalogWitness metadata)
    {
        if (savedVigor is null || !WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
            !RuntimeInspectionPlayerOwnerIsCurrentPlain() || !ReferenceEquals(current.NativeGameClient, Client) ||
            current.AccountId != AccountId || current.Character.Uid != Uid ||
            LegacyEquipmentQueryBounds.ReadVigor(current.Character) != value || !completion.IsCurrentPlain(this, current)) throw InvalidVigor();
        var proof = LegacyNativeAttributeMapProof.CaptureRaw(current.Character.Attributes);
        if (!proof.IsCurrentPlain() || !metadata.IsCurrentPlain()) throw InvalidVigor();
        vigorReceipt = new(current, proof, value, completion, metadata);
    }

    internal PreparedVigorRetention? PrepareVigorRetention(WorldSession expected, WorldSession final)
    {
        try
        {
            if (vigorReceipt is not { } before || !ReferenceEquals(before.Session, expected) ||
                LegacyEquipmentQueryBounds.ReadVigor(expected.Character) != before.Value ||
                LegacyEquipmentQueryBounds.ReadVigor(final.Character) != before.Value ||
                before.Completion is null && !LegacySavedVigorSeed.SameValue(expected.Character, final.Character)) return null;
            var map = LegacyNativeAttributeMapProof.CaptureRaw(final.Character.Attributes);
            var after = new VigorReceipt(final, map, before.Value, before.Completion, before.Metadata);
            if (!VigorRetentionIsCurrent(before, after)) return null;
            return PreparedVigorRetention.Create(this, before, after);
        }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException or ArgumentException or OverflowException) { return null; }
    }

    private bool VigorRetentionIsCurrent(VigorReceipt before, VigorReceipt after)
    {
        if (!RuntimeInspectionPlayerOwnerIsCurrentPlain() || !Store.IsBoundSpellOwnerCurrent(this) ||
            !before.Map.IsCurrentPlain() || !after.Map.IsCurrentPlain() || before.Value != after.Value ||
            before.Session.AccountId != AccountId || before.Session.Character.Uid != Uid ||
            after.Session.AccountId != AccountId || after.Session.Character.Uid != Uid ||
            !ReferenceEquals(before.Session.NativeGameClient, Client) || !ReferenceEquals(after.Session.NativeGameClient, Client) ||
            !ReferenceEquals(before.Session.PlayerItems, after.Session.PlayerItems) ||
            before.Session.AlternateEquipmentActive != after.Session.AlternateEquipmentActive) return false;
        if (before.Completion is null) return before.Metadata is null && savedVigor is not null &&
            LegacyNativeMagicQueryClient.IsVigorSeedEpochCurrentPlain(this, before.Session);
        return before.Metadata is not null && before.Metadata.IsCurrentPlain() &&
            before.Completion.CoversVigorSuccessor(this, before.Session, after.Session);
    }

    // Called only from Store's consumed actual-holder publication permit.
    internal void PublishVigorRetention(PreparedVigorRetention? retained, WorldSessionStore.OwnedVigorPublication permit)
    {
        if (retained is null)
        {
            if (!WorldSessionStore.ConsumeVigorPublication(permit, this, null)) throw InvalidVigor();
            vigorReceipt = null;
        }
        else retained.PublishActual(permit);
    }

    internal bool MatchesAttributeAllocationVigor(CharacterSnapshot disk, WorldSession expected)
    {
        try
        {
            if (vigorReceipt is not { } receipt || !ReferenceEquals(receipt.Session, expected) ||
                !RuntimeInspectionPlayerOwnerIsCurrentPlain() || !Store.IsBoundSpellOwnerCurrent(this) ||
                !ReferenceEquals(expected.NativeGameClient, Client) || expected.AccountId != AccountId ||
                expected.Character.Uid != Uid || !receipt.Map.IsCurrentPlain()) return false;
            if (receipt.Completion is null)
                return receipt.Metadata is null && LegacyNativeMagicQueryClient.IsVigorSeedEpochCurrentPlain(this, expected) &&
                    LegacySavedVigorSeed.SameValue(disk, expected.Character);
            return receipt.Metadata is not null && receipt.Metadata.IsCurrentPlain() &&
                receipt.Completion.IsVigorEpochCurrentPlain(this, expected);
        }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException or ArgumentException) { return false; }
    }

    private static InvalidOperationException InvalidVigor() => new("boundQueryVigorOriginUnavailable");
}

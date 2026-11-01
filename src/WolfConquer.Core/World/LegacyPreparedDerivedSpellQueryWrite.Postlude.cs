using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedDerivedSpellQueryWrite
{
    private WorldSession? preparedFinal, publishedFinal;
    private LegacyNativeAttributeMapProof? finalAttributes, finalPaid;
    internal void PrepareFinal(WorldSession actual)
    {
        if (preparedFinal is not null || !BeforeOwnerIsCurrent() || !ProposalMatches(actual)) throw Invalid();
        preparedFinal = actual;
        finalAttributes = new(actual.Character.Attributes); finalPaid = new(actual.Character.PaidNobility);
    }
    // The Store calls this only after its actual native+holder plain stores.
    internal void RegisterPublished(WorldSession actual)
    {
        if (!ReferenceEquals(preparedFinal, actual) || !ReferenceEquals(committed, preparation)) throw Invalid();
        publishedFinal = actual;
    }
    internal bool IsPublishedCurrentPlain(WorldSession actual) => ReferenceEquals(publishedFinal, actual) &&
        ReferenceEquals(committed, preparation) && ReferenceEquals(binding.SavedSpells, savedAfter) &&
        store.TryGet(binding.Connection, out var current) && ReferenceEquals(current, actual) &&
        store.IsBoundSpellOwnerCurrent(binding) && frame.IsActionLifecycleCurrent(store, context) &&
        query.IsEpochCurrentPlain(binding, actual) && metadata.IsCurrentPlain() &&
        finalAttributes is not null && finalAttributes.IsCurrentPlain() && finalPaid is not null && finalPaid.IsCurrentPlain();
    internal bool CoversCommittedPublication(WorldServerFrameHandler actualFrame, WorldSessionStore actualStore,
        ConquerTcpConnection connection, WorldSession? previous, WorldSession current) =>
        ReferenceEquals(frame, actualFrame) && ReferenceEquals(store, actualStore) &&
        ReferenceEquals(binding.Connection, connection) && ReferenceEquals(previous, preparation.Expected) &&
        IsPublishedCurrentPlain(current) && ExactAfterFilesAreCurrent() && IsPublishedCurrentPlain(current);
}

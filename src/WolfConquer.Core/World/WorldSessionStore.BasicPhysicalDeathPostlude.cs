using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed partial class BasicPhysicalPublication
    {
        // Existing concrete Frame postlude only. This is historical commit
        // evidence plus current final owners, never the old before-plan guard.
        internal bool CoversCommittedDeathSpells(WorldServerFrameHandler frame, WorldSessionStore actualStore,
            ConquerTcpConnection connection, WorldSession? previous, WorldSession current)
        {
            if (!ReferenceEquals(store, actualStore) || !store.basicPhysicalPublications.TryGetValue(this, out var actual) ||
                !ReferenceEquals(actual.Frame, frame) || !ReferenceEquals(actual.TargetBinding.Connection, connection) ||
                !ReferenceEquals(actual.Plan.ExpectedTarget, previous) || !ReferenceEquals(actual.Target, current) ||
                actual.Plan.Death is null || actual.DeathFileWriter is not { } writer ||
                actual.CommittedWriter?.IsCommitted != true || !store.IsBasicPhysicalPublicationCurrentHeldPlain(actual) ||
                !writer.MatchesActualCommittedAfterImages()) return false;
            return store.IsBasicPhysicalPublicationCurrentHeldPlain(actual);
        }
    }
}

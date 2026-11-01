using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedBasicPhysicalProficiencyWrite
{
    private WorldSession? publishedBefore, publishedAfter;

    internal void RegisterPublished(WorldSessionStore.BasicPhysicalPublication actual, WorldSession previous, WorldSession current)
    {
        if (!IsCommittedFor(actual) || !store.IsActualBasicPhysicalProficiencyPublished(actual, this, previous, current)) throw Invalid();
        publishedBefore = previous; publishedAfter = current;
    }

    internal bool CoversCommittedPublication(WorldServerFrameHandler owner, WorldSessionStore actualStore,
        ConquerTcpConnection connection, WorldSession? previous, WorldSession current) =>
        ReferenceEquals(owner, frame) && ReferenceEquals(actualStore, store) && ReferenceEquals(connection, hit.ActorBinding.Connection) &&
        ReferenceEquals(previous, publishedBefore) && ReferenceEquals(current, publishedAfter) && committed is { } actual &&
        ReferenceEquals(current.PlayerProficiencies, Plan.After) &&
        store.IsActualBasicPhysicalProficiencyPublished(actual, this, previous!, current) && ExactCommittedImageIsCurrent(actual);
}

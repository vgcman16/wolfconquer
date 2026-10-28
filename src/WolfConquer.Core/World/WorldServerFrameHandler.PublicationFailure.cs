namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal void BlockPublishedSessionFailure(WorldSessionStore store, WorldSessionPublicationException failure)
    {
        if (!ReferenceEquals(_sessions, store) || !failure.BelongsTo(store, failure.Connection))
            throw new InvalidOperationException("Publication failure requires its actual Frame Store owner.");
        _tradeRecovery.Block("sessionPostPublicationFailure:" + failure.Stage + ":recoveryRequired");
    }
}

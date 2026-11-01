namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal WorldPacketHandlerContext CreateOwnedPhysicalDeathContext(WorldSessionStore store,
        WorldPacketHandlerContext context, WorldPacketHandlers.BasicPhysicalReceiveDecision decision,
        BasicPhysicalAttempt attempt, WorldSession expectedActor)
    {
        if (!ReferenceEquals(store, _sessions) || !TryGetBasicPhysicalAttemptRoles(context, attempt, out var actor, out _) ||
            !ReferenceEquals(context.Connection, actor.Connection) ||
            !_sessions.TryGet(actor.Connection, out var current) || !ReferenceEquals(current, expectedActor))
            throw new InvalidOperationException("ordinaryPhysicalDeathFrameChanged");
        // The private real Frame factory supplies its configured clocks, team,
        // catalogs and services. The caller cannot choose a death clock/provider.
        return CreateMonsterRuntimeContext(new(actor.Connection, expectedActor)) with
        {
            BasicPhysicalAttempt = attempt, BasicPhysicalReceiveDecision = decision,
            TradeOperations = _worldOperations,
        };
    }
}

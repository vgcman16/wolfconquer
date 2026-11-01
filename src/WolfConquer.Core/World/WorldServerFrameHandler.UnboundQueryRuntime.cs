namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal void RecordCompletedUnboundQueryRuntime(WorldSessionStore store, WorldPacketHandlerContext context,
        WorldSession expected, WorldSession published, WorldPacketHandlers.ManagedQueryHitpointsPurpose purpose,
        int value, bool vigor, uint preparedVigor)
    {
        if (!ReferenceEquals(_sessions, store) || !ReferenceEquals(context.Sessions, store) ||
            !store.IsFramePersistenceCurrent(this, context.Connection, published) ||
            !WorldPacketHandlers.IsConsumedManagedQueryHitpoints(purpose, context, expected, value, vigor) ||
            vigor && preparedVigor != checked((uint)value)) throw new InvalidOperationException("managedQueryRuntimeOwnerChanged");
        // No origin is invented for detached/declared component sessions. The
        // real query can update its runtime cache without granting file rights.
        if (!unboundCharacterOrigins.TryGetValue(context.Connection, out var origin)) return;
        if (origin.AccountId != published.AccountId || origin.Uid != published.Character.Uid ||
            !LegacyNativeClientPublication.TryCurrent(store, context.Connection, origin.Uid, origin.Identity, out var current) ||
            !ReferenceEquals(current, published)) throw new InvalidOperationException("managedQueryRuntimeEpochChanged");
        origin.RuntimeVigor = preparedVigor;
    }
}

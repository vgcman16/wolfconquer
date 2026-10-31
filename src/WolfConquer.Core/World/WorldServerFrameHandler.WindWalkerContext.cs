using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal bool TryPrepareWindWalkerContext(WorldSessionStore store, WorldPacketHandlerContext context,
        WorldSession expected, out WorldPacketHandlerContext prepared)
    {
        prepared = null!;
        // The actual initial/runtime context already belongs to this Frame but
        // predates its operation-barrier field. Join this same real owner;
        // a foreign nonnull barrier is never replaced by a permissive default.
        if (!ReferenceEquals(_sessions, store) || !ReferenceEquals(context.Sessions, store) ||
            !ReferenceEquals(context.Trades, _tradeStates) || !ReferenceEquals(context.TradeRecovery, _tradeRecovery) ||
            !ReferenceEquals(context.WorldData, _worldData) || context.ProtocolProfile != _protocolProfile ||
            context.Connection.NativeGameClient?.OrdinarySpellBinding is not { } binding || binding.IsRetired ||
            !ReferenceEquals(binding.Store, store) || !ReferenceEquals(binding.Recovery, _tradeRecovery) ||
            !ReferenceEquals(binding.ItemCatalog, _worldData) || !ReferenceEquals(expected.NativeGameClient, binding.Client) ||
            expected.Character.Uid != binding.Uid || expected.AccountId != binding.AccountId ||
            !store.TryGet(context.Connection, out var current) || !ReferenceEquals(current, expected) ||
            context.TradeOperations is not null && !ReferenceEquals(context.TradeOperations, _worldOperations))
            return false;
        prepared = context.TradeOperations is null ? context with { TradeOperations = _worldOperations } : context;
        return true;
    }

    internal object RequireWindWalkerLifecycleGate(WorldSessionStore store)
    {
        if (!ReferenceEquals(store, _sessions)) throw new InvalidOperationException("WindWalker requires its actual Frame/Store.");
        return _tradeStates.SyncRoot;
    }

    internal bool IsWindWalkerContext(WorldSessionStore store, WorldPacketHandlerContext context, WorldSession expected) =>
        ReferenceEquals(_sessions, store) && ReferenceEquals(context.Sessions, store) &&
        ReferenceEquals(context.Trades, _tradeStates) && ReferenceEquals(context.TradeOperations, _worldOperations) &&
        ReferenceEquals(context.TradeRecovery, _tradeRecovery) && ReferenceEquals(context.WorldData, _worldData) &&
        context.ProtocolProfile == _protocolProfile && _protocolProfile == ConquerProtocolProfile.VisibleDx8D13 &&
        Monitor.IsEntered(_tradeStates.SyncRoot) && !_tradeStates.HasConnectionEnded(context.Connection) &&
        !_tradeRecovery.IsBlocked && store.IsFramePersistenceCurrent(this, context.Connection, expected);
}

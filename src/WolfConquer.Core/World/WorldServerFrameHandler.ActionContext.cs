using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal bool TryPrepareActionContext(WorldSessionStore store, WorldPacketHandlerContext context,
        WorldSession expected, out WorldPacketHandlerContext prepared)
    {
        prepared = null!;
        if (!ReferenceEquals(store, _sessions) || !ReferenceEquals(context.Sessions, store) ||
            !ReferenceEquals(context.Trades, _tradeStates) || !ReferenceEquals(context.TradeRecovery, _tradeRecovery) ||
            !ReferenceEquals(context.WorldData, _worldData) || context.ProtocolProfile != _protocolProfile ||
            _protocolProfile != ConquerProtocolProfile.VisibleDx8D13 ||
            context.TradeOperations is not null && !ReferenceEquals(context.TradeOperations, _worldOperations) ||
            _ownedAdmissionRuntime is not { IsStopping: false } ||
            context.Connection.NativeGameClient?.OrdinarySpellBinding is not { IsRetired: false } binding ||
            !ReferenceEquals(binding.Store, store) || !ReferenceEquals(binding.Recovery, _tradeRecovery) ||
            !ReferenceEquals(binding.ItemCatalog, _worldData) || !ReferenceEquals(expected.NativeGameClient, binding.Client) ||
            expected.Character.Uid != binding.Uid || expected.AccountId != binding.AccountId ||
            !_creationLifetimes.TryGetValue(context.Connection, out var lifetime) || !lifetime.IsCurrent(context.Connection) ||
            !store.TryGet(context.Connection, out var current) || !ReferenceEquals(current, expected)) return false;
        prepared = context.TradeOperations is null ? context with { TradeOperations = _worldOperations } : context;
        return true;
    }

    internal object RequireActionLifecycleGate(WorldSessionStore store)
    {
        if (!ReferenceEquals(store, _sessions)) throw new InvalidOperationException("Action requires its actual Frame/Store.");
        return _tradeStates.SyncRoot;
    }

    internal bool IsActionLifecycleCurrent(WorldSessionStore store, WorldPacketHandlerContext context) =>
        ReferenceEquals(store, _sessions) && ReferenceEquals(context.Sessions, store) &&
        ReferenceEquals(context.Trades, _tradeStates) && ReferenceEquals(context.TradeOperations, _worldOperations) &&
        ReferenceEquals(context.TradeRecovery, _tradeRecovery) && ReferenceEquals(context.WorldData, _worldData) &&
        context.ProtocolProfile == _protocolProfile && _protocolProfile == ConquerProtocolProfile.VisibleDx8D13 &&
        _ownedAdmissionRuntime is { IsStopping: false } && Monitor.IsEntered(_tradeStates.SyncRoot) &&
        !_tradeStates.HasConnectionEnded(context.Connection) && !_tradeRecovery.IsBlocked &&
        _creationLifetimes.TryGetValue(context.Connection, out var lifetime) && lifetime.IsCurrent(context.Connection);
}

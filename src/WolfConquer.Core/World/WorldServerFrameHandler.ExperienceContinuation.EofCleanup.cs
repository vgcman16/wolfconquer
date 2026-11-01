using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Denial-only identity proof for exact checkpoint retirement. This is not
    // a live action context and must never authorize gameplay or admission.
    internal bool IsEndedContinuationCleanupCurrent(WorldSessionStore store, WorldPacketHandlerContext context,
        LegacyOrdinarySpellBinding binding) =>
        Monitor.IsEntered(_tradeStates.SyncRoot) && ReferenceEquals(store, _sessions) &&
        ReferenceEquals(context.Sessions, store) && ReferenceEquals(context.Trades, _tradeStates) &&
        ReferenceEquals(context.TradeOperations, _worldOperations) &&
        _worldOperations.OwnsActiveExclusiveLease(context.FrameOperationLease) &&
        ReferenceEquals(context.TradeRecovery, _tradeRecovery) && !_tradeRecovery.IsBlocked &&
        ReferenceEquals(context.WorldData, _worldData) && context.ProtocolProfile == _protocolProfile &&
        _protocolProfile == ConquerProtocolProfile.VisibleDx8D13 && _ownedAdmissionRuntime is { IsStopping: false } &&
        _tradeStates.HasConnectionEnded(context.Connection) &&
        _creationLifetimes.TryGetValue(context.Connection, out var lifetime) && lifetime.IsClosed(context.Connection) &&
        ReferenceEquals(binding.Store, store) && ReferenceEquals(binding.Connection, context.Connection) &&
        ReferenceEquals(binding.Recovery, _tradeRecovery) && ReferenceEquals(binding.ItemCatalog, _worldData) &&
        ReferenceEquals(context.Connection.NativeGameClient, binding.Client) &&
        ReferenceEquals(binding.Client.Player, binding.Player) && ReferenceEquals(binding.Player.Owner, binding.Client) &&
        ReferenceEquals(binding.Client.OrdinarySpellBinding, binding);
}

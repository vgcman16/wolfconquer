using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal bool IsOwnedPhysicalMetadataCatalogCurrent(WorldSessionStore store, LegacyOrdinarySpellBinding binding,
        WorldDataCatalog? catalog) => ReferenceEquals(store, _sessions) && ReferenceEquals(binding.Store, store) &&
        ReferenceEquals(_worldData, catalog) && ReferenceEquals(binding.ItemCatalog, catalog) &&
        _protocolProfile == ConquerProtocolProfile.VisibleDx8D13 && binding.ItemProfile == _protocolProfile &&
        _ownedAdmissionRuntime is { IsStopping: false } && ReferenceEquals(binding.Recovery, _tradeRecovery) && !_tradeRecovery.IsBlocked &&
        !_tradeStates.HasConnectionEnded(binding.Connection) &&
        _creationLifetimes.TryGetValue(binding.Connection, out var lifetime) && lifetime.IsCurrent(binding.Connection) &&
        !binding.IsRetired && ReferenceEquals(binding.Connection.NativeGameClient, binding.Client) &&
        ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) && store.IsBoundSpellOwnerCurrent(binding);
}

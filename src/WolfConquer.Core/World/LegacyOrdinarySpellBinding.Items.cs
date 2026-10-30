using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// This is part of the same genuine Store-issued binding, not a UID authority.
internal sealed partial class LegacyOrdinarySpellBinding
{
    private LegacyNativeOwnedItemGraph? items;
    private WorldDataCatalog? itemCatalog;
    private ConquerProtocolProfile itemProfile;
    private PlayerItemSnapshot? itemSnapshot;

    internal LegacyNativeOwnedItemGraph Items => items ?? throw InvalidItems();
    internal WorldDataCatalog? ItemCatalog => items is not null && itemSnapshot is not null ? itemCatalog : throw InvalidItems();
    internal ConquerProtocolProfile ItemProfile => itemProfile;
    internal PlayerItemSnapshot ItemSnapshot => Volatile.Read(ref itemSnapshot) ?? throw InvalidItems();

    // The binding and graph are still detached here. Actual prefix/Store
    // publication, with its consumed proofs, is the only attachment authority.
    internal void InitializePreparedItems(LegacyNativeOwnedItemGraph graph, WorldDataCatalog? catalog,
        ConquerProtocolProfile profile, PlayerItemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (items is not null || itemSnapshot is not null || IsRetired || Client.OrdinarySpellBinding is not null ||
            profile != ConquerProtocolProfile.VisibleDx8D13 ||
            !ReferenceEquals(graph.Client, Client) || !ReferenceEquals(graph.Player, Player) || graph.Uid != Uid ||
            !ReferenceEquals(graph.Image.Catalog, catalog) || graph.Image.Profile != profile ||
            !ReferenceEquals(graph.Image.FinalSnapshot, snapshot))
            throw InvalidItems();
        items = graph; itemCatalog = catalog; itemProfile = profile; itemSnapshot = snapshot;
    }

    internal void PublishItemSnapshot(PlayerItemSnapshot snapshot) => Volatile.Write(ref itemSnapshot, snapshot);

    private bool ItemOwnerIsCurrentPlain() => items is not null && itemSnapshot is not null &&
        itemProfile == ConquerProtocolProfile.VisibleDx8D13 &&
        ReferenceEquals(items.Client, Client) && ReferenceEquals(items.Player, Player) && items.Uid == Uid &&
        ReferenceEquals(items.Image.Catalog, itemCatalog) && items.Image.Profile == itemProfile &&
        ReferenceEquals(items.Image.FinalSnapshot, itemSnapshot) && items.PhysicalOwnerIsCurrentPlain();

    private static InvalidOperationException InvalidItems() =>
        new("ordinaryNativeItemsRequireCurrentCompleteOwner");
}

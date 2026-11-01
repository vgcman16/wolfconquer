using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // A read-only identity denial proof, not catalog adoption or an owner
    // minted from equivalent decoded rows. The actual plan owns its inputs.
    internal bool IsOwnedPhysicalMetadataCatalogCurrent(LegacyOrdinarySpellBinding binding, WorldDataCatalog? catalog) =>
        framePersistenceOwner?.IsOwnedPhysicalMetadataCatalogCurrent(this, binding, catalog) == true;
}

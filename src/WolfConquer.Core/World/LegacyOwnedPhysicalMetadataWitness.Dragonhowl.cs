using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOwnedPhysicalMetadataWitness
{
    // DATA/currentness only. No query or attack completion is issued here.
    internal static LegacyOwnedPhysicalMetadataWitness CaptureDragonhowl(LegacyOrdinarySpellBinding binding,
        WorldDataCatalog catalog, WorldSession source)
    {
        var proof = new LegacyOwnedPhysicalMetadataWitness(binding, catalog, preserveRawAttributes: true);
        proof.Extend(source);
        return proof;
    }
}

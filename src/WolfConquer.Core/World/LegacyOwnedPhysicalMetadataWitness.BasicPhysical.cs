using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOwnedPhysicalMetadataWitness
{
    // Only the concrete basic-physical receiver selects this raw currentness
    // witness; Extend still captures the same actual metadata and native inputs.
    internal static LegacyOwnedPhysicalMetadataWitness CaptureBasicPhysical(
        LegacyOrdinarySpellBinding binding, WorldDataCatalog? catalog, WorldSession source)
    {
        var witness = new LegacyOwnedPhysicalMetadataWitness(binding, catalog, preserveRawAttributes: true);
        witness.Extend(source);
        return witness;
    }
}

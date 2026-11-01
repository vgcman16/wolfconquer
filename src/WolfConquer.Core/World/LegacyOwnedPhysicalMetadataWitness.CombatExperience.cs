using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOwnedPhysicalMetadataWitness
{
    internal static LegacyOwnedPhysicalMetadataWitness CaptureCombatExperience(
        LegacyOrdinarySpellBinding binding, WorldDataCatalog catalog, WorldSession source)
    {
        var witness = new LegacyOwnedPhysicalMetadataWitness(binding, catalog, preserveRawAttributes: true);
        witness.Extend(source);
        return witness;
    }
}

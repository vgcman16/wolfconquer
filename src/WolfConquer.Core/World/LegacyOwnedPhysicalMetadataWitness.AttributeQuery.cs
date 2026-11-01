using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOwnedPhysicalMetadataWitness
{
    internal static LegacyOwnedPhysicalMetadataWitness CaptureAttributeQuery(LegacyOrdinarySpellBinding binding,
        WorldDataCatalog? catalog, WorldSession source)
    {
        var proof = new LegacyOwnedPhysicalMetadataWitness(binding, catalog, preserveRawAttributes: true);
        proof.Extend(source);
        proof.RequireNativeQueryInputs(source);
        return proof;
    }
}

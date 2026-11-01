using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOwnedPhysicalMetadataWitness
{
    // DATA footprint for this actual source phase only. Full ordinary HP or
    // native-query collectors are not dependencies of an isolated grant.
    internal static LegacyOwnedPhysicalMetadataWitness CaptureDerivedQuery(LegacyOrdinarySpellBinding binding,
        WorldDataCatalog? catalog, WorldSession source, LegacyDerivedSpellQuerySource purpose)
    {
        var result = new LegacyOwnedPhysicalMetadataWitness(binding, catalog);
        if (!result.IsCurrentPlain() || !ReferenceEquals(source.NativeGameClient, binding.Client) ||
            source.Character.Uid != binding.Uid || source.AccountId != binding.AccountId ||
            purpose is not (LegacyDerivedSpellQuerySource.Rank or LegacyDerivedSpellQuerySource.MythSoul)) throw Invalid();
        result.inputs.Add(new(source));
        if (purpose == LegacyDerivedSpellQuerySource.Rank && source.Character.Class / 1000 is 2 or 4 or 7 or 13 or 14)
        {
            if (catalog is null) throw Invalid();
            result.UseRows(catalog.CombatGearRanks);
            result.RequireCombatGearRankNumbers();
        }
        if (!result.IsCurrentPlain()) throw Invalid();
        return result;
    }
}

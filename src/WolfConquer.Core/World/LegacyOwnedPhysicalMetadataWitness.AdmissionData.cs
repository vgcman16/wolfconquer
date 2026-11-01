using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOwnedPhysicalMetadataWitness
{
    // A detached DATA collector is never exposed as a live metadata witness.
    // Its IsCurrentPlain deliberately remains false without a real binding.
    private LegacyOwnedPhysicalMetadataWitness(WorldDataCatalog? catalog)
    { this.catalog = catalog; preserveRawAttributes = true; }

    internal sealed class AdmissionQueryData
    {
        private readonly LegacyOwnedPhysicalMetadataWitness data;
        private readonly WorldSession candidate;
        private readonly WorldDataCatalog? catalog;

        private AdmissionQueryData(WorldSession candidate, WorldDataCatalog? catalog)
        {
            this.candidate = candidate; this.catalog = catalog;
            data = new LegacyOwnedPhysicalMetadataWitness(catalog);
            data.inputs.Add(new Input(candidate, preserveRawAttributes: true));
            var selected = candidate with { PlayerItems = candidate.PlayerItems with
                { EquipmentItems = LegacyEquipmentQuerySelector.Select(candidate) } };
            data.inputs.Add(new Input(selected, preserveRawAttributes: true));
            LegacyPlayerStatusCalculator.CaptureOwnedPhysicalMetadata(data, selected, catalog);
            LegacyMaxHitpointsCalculator.CaptureOwnedPhysicalMetadata(data, selected, catalog);
            LegacyPlayerStatusCalculator.CaptureOwnedPhysicalNativeQueryMetadata(data, selected, catalog);
            if (catalog is not null && candidate.Character.Class / 1000 is 2 or 4 or 7 or 13 or 14)
            { data.UseRows(catalog.CombatGearRanks); data.RequireCombatGearRankNumbers(); }
            if (!IsCurrentFor(candidate, catalog)) throw Invalid();
        }

        internal static AdmissionQueryData Capture(WorldSession candidate, WorldDataCatalog? catalog) => new(candidate, catalog);

        internal bool IsCurrentFor(WorldSession actual, WorldDataCatalog? actualCatalog) =>
            ReferenceEquals(candidate, actual) && ReferenceEquals(catalog, actualCatalog) && data.CapturedDataIsCurrent();

        internal void RequireRuntimeSpell(LegacyMagicRuntimeSpell resolved)
        {
            if (!IsCurrentFor(candidate, catalog)) throw Invalid();
            _ = data.RequireRawRuntimeSpell(resolved.Id, resolved.Level, out var retained);
            if (retained != resolved || !IsCurrentFor(candidate, catalog)) throw Invalid();
        }

        internal void RequireDerivedSpell(ushort id, ushort level)
        {
            if (id is not (20_000 or 21_930) || !IsCurrentFor(candidate, catalog)) throw Invalid();
            _ = data.RequireRawRuntimeSpell(id, level);
            if (!IsCurrentFor(candidate, catalog)) throw Invalid();
        }
    }
}

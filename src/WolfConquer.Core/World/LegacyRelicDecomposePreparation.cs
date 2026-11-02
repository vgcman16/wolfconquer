using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed class LegacyRelicDecomposePreparation
{
    private readonly WorldDataCatalog catalog;
    private readonly LegacyRelicDecomposeMetadata metadata;
    private readonly LegacyRelicDecomposeInputs inputs;
    private LegacyRelicDecomposePreparation(WorldDataCatalog catalog, LegacyRelicDecomposeMetadata metadata,
        LegacyRelicDecomposeInputs inputs, LegacyRelicDecomposeRecipe recipe)
    { this.catalog = catalog; this.metadata = metadata; this.inputs = inputs; Recipe = recipe; }

    internal LegacyRelicDecomposeRecipe Recipe { get; }
    internal uint MaximumUid => inputs.MaximumUid;
    internal byte[] CopyBeforeImage() => inputs.CopyBeforeImage();
    internal bool IsCurrent(PlayerItemSnapshot actual) => metadata.IsCurrent(catalog) && inputs.IsCurrent(actual);

    internal static bool TryPrepare(WorldDataCatalog catalog, PlayerItemSnapshot items, LegacyRelicDecomposeRequest request,
        out LegacyRelicDecomposePreparation? preparation, out string reason)
    {
        preparation = null; reason = "relicDecomposeMetadataOwnerRequired";
        if (catalog is null || catalog.RelicDecomposeMetadata is not { } metadata || !metadata.IsCurrent(catalog)) return false;
        if (!LegacyRelicDecomposeInputs.TryCapture(items, request, out var inputs, out reason) || inputs is null) return false;
        foreach (var row in inputs.Selections)
            if (!metadata.ContainsRelic(row.ItemId)) { reason = "relicDecomposeActualRelicMetadataRequired"; return false; }
        if (!LegacyRelicDecomposeRecipe.TryFromRecorded(request, inputs.Selections, metadata.RewardDurability,
                out var recipe, out reason) || recipe is null) return false;
        var candidate = new LegacyRelicDecomposePreparation(catalog, metadata, inputs, recipe);
        if (!candidate.IsCurrent(items)) { reason = "relicDecomposeInputsChanged"; return false; }
        preparation = candidate; reason = string.Empty; return true;
    }
}

namespace WolfConquer.Core.World;

// Only the actual admitted Rune owner can issue this live compose dependency
// proof. The exposed operation is detached journal input, never an authority.
internal sealed partial class LegacyRuneComposeEvidence
{
    private readonly LegacyRuneOperationEvidence dependencies;
    private readonly LegacyRuneOperationEvidence.CatalogWitness catalog;
    private readonly MaterialCatalogWitness materialsCatalog;
    private readonly LegacyRuneComposeProgressWitness progress;
    private readonly uint selectedUid;
    private readonly uint[] materials;
    private readonly LegacyRuneMetadata[] metadata;
    private readonly LegacyRuneComposeProgressRow[] progressRows;
    private readonly LegacyRuneComposeMaterialMetadata[] materialMetadata;
    internal LegacyRuneComposeOperation Operation { get; }

    private LegacyRuneComposeEvidence(WorldPacketHandlerContext context, WorldSession stable, uint uid, uint[] selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        dependencies = LegacyRuneOperationEvidence.Capture(context, stable, false, uid, 212);
        var adopted = context.WorldData ?? throw Invalid();
        selectedUid = uid; materials = selected.ToArray();
        progress = new(adopted);
        var materialTypes = materials.Select(materialUid => stable.PlayerItems.InventoryItems.Concat(stable.PlayerItems.RuneItems)
            .Single(row => row.Uid == materialUid).ItemId).Distinct().Order().ToArray();
        materialsCatalog = new(adopted, materialTypes);
        var plan = WorldPacketHandlers.PlanRecordedRuneCompose(stable.PlayerItems, uid, materials, progress.Lookup, materialsCatalog.Lookup);
        progressRows = progress.CaptureUsed(plan.UsedProgress);
        var required = LegacyRuneOperation.RequiredMetadata(stable.PlayerItems, uid)
            .Append(plan.Target.ItemId).Distinct().Order().ToArray();
        catalog = new(adopted, required); metadata = catalog.Rows.ToArray();
        materialMetadata = plan.Consumed.Select(row => row.ItemId).Distinct().Order()
            .Select(id => materialsCatalog.Lookup.TryGetValue(id, out var row) ? row : throw Invalid()).ToArray();
        Operation = new(uid, materials.ToArray(), metadata.ToArray(), progressRows.ToArray(), materialMetadata.ToArray());
        Operation.Validate();
        if (!IsCurrent()) throw Invalid();
    }

    internal static LegacyRuneComposeEvidence Capture(WorldPacketHandlerContext context, WorldSession stable,
        uint uid, uint[] selected) => new(context, stable, uid, selected);

    internal bool IsCurrent()
    {
        if (!dependencies.IsCurrent() || !catalog.IsCurrent() || !materialsCatalog.IsCurrent() || !progress.IsCurrent() ||
            Operation.SelectedUid != selectedUid ||
            Operation.Materials.Length != materials.Length || Operation.Metadata.Length != metadata.Length ||
            Operation.Progress.Length != progressRows.Length || Operation.MaterialMetadata.Length != materialMetadata.Length) return false;
        for (var index = 0; index < materials.Length; index++)
            if (Operation.Materials[index] != materials[index]) return false;
        for (var index = 0; index < metadata.Length; index++)
            if (!ReferenceEquals(Operation.Metadata[index], metadata[index])) return false;
        for (var index = 0; index < progressRows.Length; index++)
            if (!ReferenceEquals(Operation.Progress[index], progressRows[index])) return false;
        for (var index = 0; index < materialMetadata.Length; index++)
            if (!ReferenceEquals(Operation.MaterialMetadata[index], materialMetadata[index])) return false;
        return true;
    }

    private static InvalidOperationException Invalid() => new("runeComposeRequiresCurrentAdmittedDependencies");
}

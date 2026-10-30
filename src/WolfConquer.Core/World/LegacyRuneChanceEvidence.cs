using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

// This admitted owner issues one decision. The detached journal operation is
// replay input, never a caller-supplied roll or permission to gamble again.
internal sealed class LegacyRuneChanceEvidence
{
    private readonly LegacyRuneOperationEvidence dependencies;
    private readonly LegacyRuneOperationEvidence.CatalogWitness catalog;
    private readonly LegacyRuneComposeProgressWitness progress;
    private readonly uint selectedUid;
    private readonly LegacyRuneMetadata[] metadata;
    private readonly LegacyRuneChanceProgressRow[] progressRows;
    private readonly byte? roll;
    internal LegacyRuneChanceOperation Operation { get; }

    private LegacyRuneChanceEvidence(WorldPacketHandlerContext context, WorldSession stable, uint uid,
        WorldPacketHandlers.ItemSpellBeforeImages before)
    {
        before.RequireChanceOwner(context, stable);
        dependencies = LegacyRuneOperationEvidence.Capture(context, stable, false, uid, 212);
        var adopted = context.WorldData ?? throw Invalid();
        selectedUid = uid; progress = new(adopted);
        var lookup = progress.Lookup.ToDictionary(pair => pair.Key,
            pair => new LegacyRuneChanceProgressRow(pair.Value.Family, pair.Value.Level, pair.Value.Progress));
        var analysis = WorldPacketHandlers.AnalyzeRecordedRuneChance(stable.PlayerItems, uid, lookup);
        progressRows = progress.CaptureUsed(analysis.UsedProgress)
            .Select(row => new LegacyRuneChanceProgressRow(row.Family, row.Level, row.Progress)).ToArray();
        var required = LegacyRuneOperation.RequiredMetadata(stable.PlayerItems, uid);
        var possible = analysis.Gambled && analysis.RatePercent != 0
            ? required.Append(checked(analysis.Target.ItemId + 1)).Distinct().Order().ToArray() : required;
        catalog = new(adopted, possible);

        // Prove every possible output before the private draw: mapping,
        // ownership retirement, full item graph and native item wire fields.
        if (analysis.RatePercent is > 0 and < 100)
        { Preflight(0); Preflight(99); }
        else Preflight(null);
        before.RequireChanceOwner(context, stable);
        if (!dependencies.IsCurrent() || !catalog.IsCurrent() || !progress.IsCurrent()) throw Invalid();
        roll = analysis.RatePercent is > 0 and < 100 ? checked((byte)Random.Shared.Next(0, 100)) : null;
        Operation = MakeOperation(roll);
        metadata = Operation.Metadata.ToArray();
        Operation.Validate();
        if (!IsCurrent()) throw Invalid();

        LegacyRuneChanceOperation MakeOperation(byte? decision)
        {
            var plan = WorldPacketHandlers.ResolveRecordedRuneChance(analysis, decision);
            var needed = required.Append(plan.Target.ItemId).Distinct().Order().ToArray();
            return new(uid, catalog.Rows.Where(row => needed.Contains(row.ItemId)).ToArray(), progressRows.ToArray(), decision);
        }
        void Preflight(byte? decision)
        {
            var operation = MakeOperation(decision);
            var projected = WorldPacketHandlers.ProjectRecordedRuneChance(stable.Character, stable.PlayerItems, stable.PlayerSpells, operation);
            if (projected.Character.MinHitPoints != stable.Character.MinHitPoints ||
                !LegacyItemSaveGraph.TryCapture(projected.Items, out var graph) ||
                !graph.HasBytes(PlayerItemSnapshotLoader.ToLegacyBytes(projected.Items))) throw Invalid();
            LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(PlayerItemSnapshotLoader.ToLegacyBytes(projected.Items));
            _ = LegacyRuneSpellOrigins.ProjectCharacterImage(before.CopyCharacter(), projected.Character);
            _ = LegacyRuneSpellOrigins.ProjectSpellImage(before.CopySpells(), projected.Spells);
            _ = LegacyGameItemPacket.FromSnapshotItem(projected.Target,
                LegacyGameItemPacket.CreateRuneSpellOverride(projected.Target, adopted.ItemsById),
                LegacyGameItemPacket.UpdateItemMode).ToFrameBytes();
        }
    }

    internal static LegacyRuneChanceEvidence Capture(WorldPacketHandlerContext context, WorldSession stable,
        uint uid, WorldPacketHandlers.ItemSpellBeforeImages before) => new(context, stable, uid, before);

    internal bool IsCurrent()
    {
        if (!dependencies.IsCurrent() || !catalog.IsCurrent() || !progress.IsCurrent() ||
            Operation.SelectedUid != selectedUid || Operation.Roll != roll ||
            Operation.Metadata.Length != metadata.Length || Operation.Progress.Length != progressRows.Length) return false;
        for (var index = 0; index < metadata.Length; index++)
            if (!ReferenceEquals(Operation.Metadata[index], metadata[index])) return false;
        for (var index = 0; index < progressRows.Length; index++)
            if (!ReferenceEquals(Operation.Progress[index], progressRows[index])) return false;
        return true;
    }

    private static InvalidOperationException Invalid() => new("runeChanceRequiresCurrentAdmittedDependencies");
}

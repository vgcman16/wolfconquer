using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRelicFuseInputs
{
    internal static bool TryCaptureReturn(PlayerItemSnapshot owner, uint selectedUid,
        out ReturnWitness? witness, out string reason) => ReturnWitness.TryCapture(owner, selectedUid, out witness, out reason);

    internal sealed class ReturnWitness
    {
        private readonly PlayerItemSnapshot owner;
        private readonly Graph graph;
        internal PlayerItemSnapshotItem Selected { get; }

        private ReturnWitness(PlayerItemSnapshot owner, Graph graph, PlayerItemSnapshotItem selected)
        { this.owner = owner; this.graph = graph; Selected = selected; }

        internal static bool TryCapture(PlayerItemSnapshot owner, uint selectedUid, out ReturnWitness? witness, out string reason)
        {
            witness = null; reason = "relicFuseReturnOwnerAndUidRequired";
            if (owner is null || selectedUid == 0) return false;
            if (!TryGraph(owner, selectedUid, selectedUid, out var captured))
            { reason = "relicFuseReturnItemGraphInvalid"; return false; }
            if (captured!.MaximumDepth > 1)
            { reason = "relicFuseReturnNestedSaveUnsupported"; return false; }
            if (captured.RootCounts[0] >= 40)
            { reason = "relicFuseReturnInventoryFull"; return false; }
            var held = captured.Rows.Where(row => row.Group == 1 && row.Parent == -1).ToArray();
            if (held.Length != 3 || captured.RootCounts[1] != 3 ||
                held.Any(row => row.Value.WarehouseId != 0 || row.Value.Position is < 213 or > 215) ||
                held.Select(row => row.Value.Position).Distinct().Count() != 3)
            { reason = "relicFuseReturnHeldTripleRequired"; return false; }
            var selected = held.SingleOrDefault(row => row.Value.Uid == selectedUid);
            if (selected is null)
            { reason = "relicFuseReturnSelectedHeldItemRequired"; return false; }
            var prepared = new ReturnWitness(owner, captured, selected.Source);
            if (!prepared.IsCurrent(owner))
            { reason = "relicFuseReturnOwnerChanged"; return false; }
            witness = prepared;
            reason = string.Empty;
            return true;
        }

        internal bool IsCurrent(PlayerItemSnapshot current) => ReferenceEquals(owner, current) &&
            TryGraph(current, Selected.Uid, Selected.Uid, out var captured) && graph.Matches(captured!);
    }
}

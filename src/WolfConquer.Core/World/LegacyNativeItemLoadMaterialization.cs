using Destination = WolfConquer.Core.World.LegacyNativeItemLoadOwners.Destination;
using Placement = WolfConquer.Core.World.LegacyNativeItemLoadOwners.Placement;

namespace WolfConquer.Core.World;

// Genuine detached conversion only. Root's owner lease/publisher owns admission
// and publication; this component never changes actual runtime containers.
internal static partial class LegacyNativeItemLoadMaterialization
{
    internal sealed class Plan
    {
        private readonly MaterializationWitness witness;
        internal Placement[] Placements { get; }
        internal uint? QuestUid { get; }

        internal Plan(MaterializationWitness witness, Placement[] placements, uint? questUid)
        { this.witness = witness; Placements = placements; QuestUid = questUid; }

        internal void RequireCurrent() => witness.RequireCurrent();
    }

    internal static Plan Prepare(LegacyNativeItemLoadImage image, LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player)
    {
        Destination required = RequiredDestinations(image);
        var witness = new MaterializationWitness(image, client, player, required);
        var placements = new List<Placement>();
        var items = new Dictionary<uint, LegacyNativeGameItemRuntime>();
        uint? questUid = null;
        foreach (var root in image.Roots)
        {
            var original = root.Record;
            if (original.ITEM_ID == 205_901) continue; // Already checked, no children/perfection loss.
            var record = original;
            // Existing explicit legacy normalization, not a new client rule.
            LegacyNativeOuterItemRecordSanitation.SanitizeBeforeConversion(ref record);
            var parent = Convert(record, witness);
            items.Add(parent.Uid, parent);
            if (record.ITEM_ID == 750_000) questUid = parent.Uid;
            foreach (var deposited in root.Deposits)
            {
                var child = Convert(deposited, witness); // Never apply outer sanitation to children.
                if (witness.GuildId == 0) child.Inscribed = 0;
                if (!parent.Deposite.TryAdd(child.Uid, child))
                    throw Invalid("duplicate prepared child identity");
                items.Add(child.Uid, child);
                if (deposited.ITEM_ID == 750_000) questUid = child.Uid;
            }
            PreparePlacements(parent, original, witness, placements);
        }
        foreach (var perfection in image.Perfections)
        {
            if (!items.TryGetValue(perfection.ItemUID, out var item))
                throw Invalid("perfection target was not genuinely materialized");
            // Genuine fixed-record text/date behavior, on this new reference only.
            perfection.ApplyTo(item);
        }
        witness.RequireCurrent();
        return new(witness, placements.ToArray(), questUid);
    }

    private static LegacyNativeGameItemRuntime Convert(LegacyNativeClientItemRecord record,
        MaterializationWitness witness)
    {
        witness.RequireCurrent();
        var item = record.GetDataItem();
        witness.ObserveConstructor(item);
        return item;
    }

    private static InvalidDataException Invalid(string reason) =>
        new("Invalid native item materialization: " + reason);
}

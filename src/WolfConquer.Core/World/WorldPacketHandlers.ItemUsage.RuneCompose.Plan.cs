using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyRuneComposePlan(PlayerItemSnapshot Items, PlayerItemSnapshotItem Target,
    IReadOnlyList<PlayerItemSnapshotItem> Consumed, IReadOnlySet<(uint Family, byte Level)> UsedProgress);

public static partial class WorldPacketHandlers
{
    // Pure arithmetic and physical-root projection. A captured table is input,
    // never authority to call this as an admitted action or write any images.
    internal static LegacyRuneComposePlan PlanRecordedRuneCompose(PlayerItemSnapshot before, uint uid,
        uint[] selected, IReadOnlyDictionary<(uint Family, byte Level), LegacyRuneComposeProgressRow> progress,
        IReadOnlyDictionary<uint, LegacyRuneComposeMaterialMetadata> materialCaps)
    {
        if (uid == 0 || selected is null || selected.Length > 49 ||
            selected.Any(value => value == 0 || value == uid) || selected.Distinct().Count() != selected.Length ||
            !TryGetRuneItem(before, uid, out var target)) throw InvalidSpellMutation();
        uint family = target.ItemId / 10_000, level = target.ItemId % 100;
        if (family is < 401 or > 403 || level == 0 || level > GetLegacyMaxRuneLevel(target.ItemId) || target.StackSize > 1)
            throw InvalidSpellMutation();
        var materialRows = new List<PlayerItemSnapshotItem>();
        foreach (var selectedUid in selected)
        {
            var candidates = before.InventoryItems.Concat(before.RuneItems).Where(row => row.Uid == selectedUid).ToArray();
            if (candidates.Length != 1) throw InvalidSpellMutation();
            materialRows.Add(candidates[0]);
        }
        var used = new HashSet<(uint Family, byte Level)>();
        var consumed = new List<PlayerItemSnapshotItem>();
        var items = before; var updated = target;
        foreach (var material in materialRows)
        {
            if (updated.ItemId % 100 >= GetLegacyMaxRuneLevel(updated.ItemId)) break;
            if (!IsLegacyRuneComposeMaterialItemId(material.ItemId) || material.Locked != 0 || material.DepositedItems.Count != 0)
                throw InvalidSpellMutation();
            if (!materialCaps.TryGetValue(material.ItemId, out var cap) || cap is null || cap.ItemId != material.ItemId ||
                Math.Max((uint)material.StackSize, 1U) > Math.Max((uint)cap.MaximumStackSize, 1U)) throw InvalidSpellMutation();
            uint itemId = updated.ItemId;
            uint delta = Difference(itemId);
            uint materialProgress = Require(material.ItemId / 10_000, material.ItemId % 100);
            uint stack = material.StackSize == 0 ? 1U : material.StackSize;
            uint points = checked(materialProgress * stack + material.RuneExperience);
            uint experience = checked(updated.RuneExperience + points);
            while (experience >= delta)
            {
                experience = checked(experience - delta); itemId = checked(itemId + 1);
                if (itemId % 100 >= GetLegacyMaxRuneLevel(itemId)) { experience = 0; break; }
                delta = Difference(itemId);
            }
            updated = updated.WithItemId(itemId).WithRuneExperience(experience);
            if (items.InventoryItems.Any(row => row.Uid == material.Uid))
                items = items with { InventoryItems = items.InventoryItems.Where(row => row.Uid != material.Uid).ToArray() };
            else items = RemoveLegacyRuneItem(items, material);
            consumed.Add(material);
        }
        items = ReplaceRuneItem(items, target, updated);
        return new(items, updated, consumed.ToArray(), used);

        uint Require(uint requiredFamily, uint requiredLevel)
        {
            if (requiredLevel is 0 or > 99 || !progress.TryGetValue((requiredFamily, checked((byte)requiredLevel)), out var row) ||
                row is null || row.Family != requiredFamily || row.Level != requiredLevel) throw InvalidSpellMutation();
            used.Add((requiredFamily, checked((byte)requiredLevel))); return row.Progress;
        }
        uint Difference(uint id)
        {
            var current = Require(id / 10_000, id % 100); var next = Require(id / 10_000, id % 100 + 1);
            if (next <= current) throw InvalidSpellMutation();
            return checked(next - current);
        }
    }
}

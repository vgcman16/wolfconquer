using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal enum LegacyRuneComposeEventKind { RemoveSpell, RemoveItem }
internal sealed record LegacyRuneComposeEvent(LegacyRuneComposeEventKind Kind, uint Value);
internal sealed record LegacyRuneComposeProjection(CharacterSnapshot Character, PlayerItemSnapshot Items,
    PlayerSpellSnapshot Spells, PlayerItemSnapshotItem Target, IReadOnlyList<LegacyRuneComposeEvent> Events,
    IReadOnlyList<PlayerItemSnapshotItem> Consumed);

public static partial class WorldPacketHandlers
{
    internal static LegacyRuneComposeProjection ProjectRecordedRuneCompose(CharacterSnapshot character,
        PlayerItemSnapshot before, PlayerSpellSnapshot spells, LegacyRuneComposeOperation operation)
    {
        operation.Validate();
        var progress = operation.Progress.ToDictionary(row => (row.Family, row.Level));
        var caps = operation.MaterialMetadata.ToDictionary(row => row.ItemId);
        var plan = PlanRecordedRuneCompose(before, operation.SelectedUid, operation.Materials, progress, caps);
        if (!plan.UsedProgress.SetEquals(progress.Keys)) throw InvalidSpellMutation();
        if (!plan.Consumed.Select(row => row.ItemId).Distinct().Order().SequenceEqual(operation.MaterialMetadata.Select(row => row.ItemId)))
            throw InvalidSpellMutation();
        var selected = before.RuneItems.Single(row => row.Uid == operation.SelectedUid);
        var required = before.RuneItems.Where(row => IsLegacyRuneEquipSlot(row.ItemId, row.Position))
            .Select(row => row.ItemId).Append(selected.ItemId).Append(plan.Target.ItemId).Distinct().Order().ToArray();
        if (!operation.Metadata.Select(row => row.ItemId).SequenceEqual(required)) throw InvalidSpellMutation();
        foreach (var row in before.RuneItems)
            if (row.Position is not (LegacyRuneBagPosition or LegacyRuneStoragePosition) &&
                !IsLegacyRuneEquipSlot(row.ItemId, row.Position)) throw InvalidSpellMutation();
        var metadata = operation.Metadata.ToDictionary(row => row.ItemId);
        foreach (var row in operation.Metadata) _ = ResolveRecordedRuneSpell(row);
        LegacyRuneSpellOrigins.RequireRows(character, spells);
        var origins = LegacyRuneSpellOrigins.Read(character);
        var providers = RuneProviders(before, metadata);
        var rows = LegacyNativeSpellStorage.FreezeSessionSnapshot(spells).Spells.ToList();
        var legacy = new List<LegacyRuneOperationEvent>();
        RetireLegacyPassiveRuneOrigins(before, metadata, providers, origins, rows, legacy, LegacyRuneMappingSemantics.RawSelf);
        if (origins.Keys.Any(id => !providers.Contains(id))) throw InvalidSpellMutation();
        var output = legacy.Select(row => new LegacyRuneComposeEvent(LegacyRuneComposeEventKind.RemoveSpell, row.Value)).ToList();
        var current = before;
        foreach (var material in plan.Consumed)
        {
            current = current.InventoryItems.Any(row => row.Uid == material.Uid)
                ? current with { InventoryItems = current.InventoryItems.Where(row => row.Uid != material.Uid).ToArray() }
                : RemoveLegacyRuneItem(current, material);
            RetireUnavailable(current);
            output.Add(new(LegacyRuneComposeEventKind.RemoveItem, material.Uid));
        }
        // Source changes only ItemID/RuneEXP. Existing learned level and origin
        // stay exact while any real provider still supplies that ID; no grant.
        RetireUnavailable(plan.Items);
        var result = LegacyRuneSpellOrigins.WithOrigins(character, origins);
        return new(result, plan.Items, new(rows.ToArray()), plan.Target, output.ToArray(), plan.Consumed);

        void RetireUnavailable(PlayerItemSnapshot items)
        {
            var remaining = RuneProviders(items, metadata);
            var removed = origins.Keys.Where(id => !remaining.Contains(id)).ToHashSet();
            foreach (var id in removed.Order())
            {
                origins.Remove(id); output.Add(new(LegacyRuneComposeEventKind.RemoveSpell, id));
            }
            if (removed.Count != 0) rows.RemoveAll(row => removed.Contains(row.Id));
        }
    }
}

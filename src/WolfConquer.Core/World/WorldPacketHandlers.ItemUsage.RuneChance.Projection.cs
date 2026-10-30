using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyRuneChanceProjection(CharacterSnapshot Character, PlayerItemSnapshot Items,
    PlayerSpellSnapshot Spells, PlayerItemSnapshotItem Target, IReadOnlyList<ushort> RemovedSpells);

public static partial class WorldPacketHandlers
{
    internal static LegacyRuneChanceProjection ProjectRecordedRuneChance(CharacterSnapshot character,
        PlayerItemSnapshot before, PlayerSpellSnapshot spells, LegacyRuneChanceOperation operation)
    {
        operation.Validate();
        var progress = operation.Progress.ToDictionary(row => (row.Family, row.Level));
        var plan = PlanRecordedRuneChance(before, operation.SelectedUid, progress, operation.Roll);
        if (!plan.UsedProgress.SetEquals(progress.Keys)) throw InvalidSpellMutation();
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
        var removed = legacy.Select(row => row.Value).ToList();
        var items = ReplaceRuneItem(before, selected, plan.Target);
        var remaining = RuneProviders(items, metadata);
        var retired = origins.Keys.Where(id => !remaining.Contains(id)).ToHashSet();
        foreach (var id in retired.Order()) { origins.Remove(id); removed.Add(id); }
        if (retired.Count != 0) rows.RemoveAll(row => retired.Contains(row.Id));
        // The source action sends no HP refresh or SpellAdd. Preserve current
        // HP and existing learned levels; only proven lost origins retire.
        return new(LegacyRuneSpellOrigins.WithOrigins(character, origins), items, new(rows.ToArray()), plan.Target, removed.ToArray());
    }
}

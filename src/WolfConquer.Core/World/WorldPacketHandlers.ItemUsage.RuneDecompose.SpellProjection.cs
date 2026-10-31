using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static bool RuneDecomposeCharactersEqual(CharacterSnapshot first, CharacterSnapshot second) =>
        FullAccountCharacterScalarsEqual(first, second) &&
        FullAccountStoredFieldsMatch(first.Attributes, second.Attributes) && FullAccountStoredFieldsMatch(second.Attributes, first.Attributes) &&
        FullAccountStoredFieldsMatch(first.PaidNobility, second.PaidNobility) && FullAccountStoredFieldsMatch(second.PaidNobility, first.PaidNobility);

    internal static (CharacterSnapshot Character, PlayerSpellSnapshot Spells, IReadOnlyList<ushort> Removed)
        PrepareRuneDecomposeSpellDependencies(CharacterSnapshot character, PlayerItemSnapshot before,
        PlayerSpellSnapshot spells, PlayerItemSnapshotItem target, LegacyRuneMetadata[] metadata)
    {
        // Fresh essence/fragment outputs have only211/0 positions and cannot be
        // providers. Prove removal before reservation without fabricating IDs.
        var removedOnly = before with { RuneItems = before.RuneItems.Where(row => row.Uid != target.Uid).ToArray() };
        return ProjectRuneDecomposeSpells(character, before, removedOnly, spells, target, metadata);
    }

    private static (CharacterSnapshot Character, PlayerSpellSnapshot Spells, IReadOnlyList<ushort> Removed)
        ProjectRuneDecomposeSpells(CharacterSnapshot character, PlayerItemSnapshot before, PlayerItemSnapshot after,
            PlayerSpellSnapshot spells, PlayerItemSnapshotItem target, LegacyRuneMetadata[] captured)
    {
        if (target.Position is 211 or 212)
        {
            if (captured.Length != 0) throw InvalidSpellMutation();
            _ = LegacyNativeSpellStorage.FreezeSessionSnapshot(spells);
            return (character, spells, []);
        }
        if (!captured.Select(row => row.ItemId).SequenceEqual(LegacyRuneOperation.RequiredMetadata(before, target.Uid)))
            throw InvalidSpellMutation();
        LegacyRuneOperation.ValidateMetadata(captured);
        var metadata = captured.ToDictionary(row => row.ItemId);
        foreach (var row in captured) _ = ResolveRecordedRuneSpell(row);
        LegacyRuneSpellOrigins.RequireRows(character, spells);
        var origins = LegacyRuneSpellOrigins.Read(character);
        var providers = RuneProviders(before, metadata);
        var rows = LegacyNativeSpellStorage.FreezeSessionSnapshot(spells).Spells.ToList();
        var legacy = new List<LegacyRuneOperationEvent>();
        RetireLegacyPassiveRuneOrigins(before, metadata, providers, origins, rows, legacy, LegacyRuneMappingSemantics.RawSelf);
        if (origins.Keys.Any(id => !providers.Contains(id))) throw InvalidSpellMutation();
        var removed = legacy.Select(row => row.Value).ToList();
        var remaining = RuneProviders(after, metadata);
        var retired = origins.Keys.Where(id => !remaining.Contains(id)).ToHashSet();
        foreach (var id in retired.Order()) { origins.Remove(id); removed.Add(id); }
        if (retired.Count != 0) rows.RemoveAll(row => retired.Contains(row.Id));
        return (LegacyRuneSpellOrigins.WithOrigins(character, origins), new(rows.ToArray()), removed.ToArray());
    }
}

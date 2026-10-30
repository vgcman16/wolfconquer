using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void ValidateRecordedRuneContainerImages(JournalRecord record, uint highWater)
    {
        var operation = record.RuneContainer ?? throw new InvalidDataException("Recorded Rune container operation is missing.");
        if (record.Version is not (13 or 15)) throw new InvalidDataException("Unexpected Rune container record version.");
        var semantics = record.Version == 15 ? LegacyRuneMappingSemantics.RawSelf : LegacyRuneMappingSemantics.LegacyDisplay;
        operation.Validate(semantics); RequireCharacterItemsSpellsOrder(record.Changes);
        var character = record.Changes[0]; var items = record.Changes[1]; var spells = record.Changes[2];
        if (character.FileId is <= 0 or > uint.MaxValue || character.Before is null || items.Before is null ||
            ReadCharacterUid(character.Before) != items.FileId || ReadCharacterUid(character.After) != items.FileId || items.FileId != spells.FileId)
            throw new InvalidDataException("Recorded Rune container participant identities differ.");
        var beforeCharacter = ReadRuneCharacter(character.Before); _ = ReadRuneCharacter(character.After);
        var beforeUids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.Before).ToHashSet();
        var afterUids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.After).ToHashSet();
        var expectedUids = beforeUids.ToHashSet();
        if (operation.Kind == LegacyRuneContainerKind.Drop && !expectedUids.Remove(operation.SelectedUid))
            throw new InvalidDataException("Dropped Rune UID is absent from its captured physical set.");
        if (!expectedUids.SetEquals(afterUids) || beforeUids.Any(uid => uid > highWater))
            throw new InvalidDataException("Rune container operation has an unregistered or unrelated physical UID change.");
        var beforeItems = PlayerItemSnapshotLoader.Parse(items.Before); var afterItems = PlayerItemSnapshotLoader.Parse(items.After);
        if (!LegacyItemSaveGraph.TryCapture(beforeItems, out _) || !LegacyItemSaveGraph.TryCapture(afterItems, out var graph) ||
            !WorldPacketHandlers.ItemSpellRawRecordsMatch(items.Before, PlayerItemSnapshotLoader.ToLegacyBytes(beforeItems)) || !graph.HasBytes(items.After))
            throw new InvalidDataException("Recorded Rune container item graphs are not lossless.");
        var beforeSpells = ReadOwnedItemSpellRows(spells.Before); _ = ReadOwnedItemSpellRows(spells.After);
        LegacyRuneSpellOrigins.RequireSavedRows(beforeCharacter, spells.Before);
        if (!LegacyRuneOperation.RequiredMetadata(beforeItems, operation.SelectedUid).SequenceEqual(operation.Metadata.Select(row => row.ItemId)))
            throw new InvalidDataException("Recorded Rune container metadata has missing or unrelated dependencies.");
        LegacyRuneOperationProjection projected;
        try { projected = WorldPacketHandlers.ProjectRecordedRuneContainer(beforeCharacter, beforeItems, beforeSpells, operation, semantics); }
        catch (InvalidOperationException error)
        { throw new InvalidDataException("Recorded Rune container action cannot produce the captured outcome.", error); }
        if (!LegacyRuneSpellOrigins.ProjectCharacterImage(character.Before, projected.Character).AsSpan().SequenceEqual(character.After) ||
            !PlayerItemSnapshotLoader.ToLegacyBytes(projected.Items).AsSpan().SequenceEqual(items.After) ||
            !LegacyRuneSpellOrigins.ProjectSpellImage(spells.Before, projected.Spells).AsSpan().SequenceEqual(spells.After))
            throw new InvalidDataException("Recorded Rune container afterimages differ from the exact captured action.");
    }
}

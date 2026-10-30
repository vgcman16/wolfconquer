using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void ValidateRecordedRuneComposeImages(JournalRecord record, uint highWater)
    {
        if (record.Version != 16 || record.RuneCompose is not { } operation)
            throw new InvalidDataException("Recorded Rune compose operation is missing or misversioned.");
        operation.Validate(); RequireCharacterItemsSpellsOrder(record.Changes);
        var character = record.Changes[0]; var items = record.Changes[1]; var spells = record.Changes[2];
        if (character.FileId is <= 0 or > uint.MaxValue || character.Before is null || items.Before is null ||
            ReadCharacterUid(character.Before) != items.FileId || ReadCharacterUid(character.After) != items.FileId || items.FileId != spells.FileId)
            throw new InvalidDataException("Recorded Rune compose participant identities differ.");
        var beforeCharacter = ReadRuneCharacter(character.Before); _ = ReadRuneCharacter(character.After);
        var beforeUids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.Before).ToHashSet();
        var afterUids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.After).ToHashSet();
        if (beforeUids.Any(uid => uid > highWater))
            throw new InvalidDataException("Rune compose before-image contains an unregistered physical UID.");
        var beforeItems = PlayerItemSnapshotLoader.Parse(items.Before); var afterItems = PlayerItemSnapshotLoader.Parse(items.After);
        if (!LegacyItemSaveGraph.TryCapture(beforeItems, out _) || !LegacyItemSaveGraph.TryCapture(afterItems, out var graph) ||
            !WorldPacketHandlers.ItemSpellRawRecordsMatch(items.Before, PlayerItemSnapshotLoader.ToLegacyBytes(beforeItems)) || !graph.HasBytes(items.After))
            throw new InvalidDataException("Recorded Rune compose item graphs are not lossless.");
        var beforeSpells = ReadOwnedItemSpellRows(spells.Before); _ = ReadOwnedItemSpellRows(spells.After);
        LegacyRuneSpellOrigins.RequireSavedRows(beforeCharacter, spells.Before);
        LegacyRuneComposeProjection projected;
        try { projected = WorldPacketHandlers.ProjectRecordedRuneCompose(beforeCharacter, beforeItems, beforeSpells, operation); }
        catch (InvalidOperationException error)
        { throw new InvalidDataException("Recorded Rune composition cannot produce the captured outcome.", error); }
        var expectedUids = beforeUids.ToHashSet();
        foreach (var material in projected.Consumed)
            if (material.DepositedItems.Count != 0 || !expectedUids.Remove(material.Uid))
                throw new InvalidDataException("Rune composition has an unproven or repeated physical retirement.");
        if (!expectedUids.SetEquals(afterUids))
            throw new InvalidDataException("Rune composition must retire only its exact consumed existing roots.");
        if (!LegacyRuneSpellOrigins.ProjectCharacterImage(character.Before, projected.Character).AsSpan().SequenceEqual(character.After) ||
            !PlayerItemSnapshotLoader.ToLegacyBytes(projected.Items).AsSpan().SequenceEqual(items.After) ||
            !LegacyRuneSpellOrigins.ProjectSpellImage(spells.Before, projected.Spells).AsSpan().SequenceEqual(spells.After))
            throw new InvalidDataException("Recorded Rune compose afterimages differ from the exact captured operation.");
    }
}

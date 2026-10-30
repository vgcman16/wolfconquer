using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void ValidateRecordedRuneChanceImages(JournalRecord record, uint highWater)
    {
        if (record.Version != 17 || record.RuneChance is not { } operation)
            throw new InvalidDataException("Recorded Rune chance operation is missing or misversioned.");
        operation.Validate(); RequireCharacterItemsSpellsOrder(record.Changes);
        var character = record.Changes[0]; var items = record.Changes[1]; var spells = record.Changes[2];
        if (character.FileId is <= 0 or > uint.MaxValue || character.Before is null || items.Before is null ||
            ReadCharacterUid(character.Before) != items.FileId || ReadCharacterUid(character.After) != items.FileId || items.FileId != spells.FileId)
            throw new InvalidDataException("Recorded Rune chance participant identities differ.");
        var beforeCharacter = ReadRuneCharacter(character.Before); var afterCharacter = ReadRuneCharacter(character.After);
        if (beforeCharacter.MinHitPoints != afterCharacter.MinHitPoints)
            throw new InvalidDataException("Recorded Rune chance cannot change saved hitpoints.");
        var beforeUids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.Before).ToHashSet();
        var afterUids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.After).ToHashSet();
        if (!beforeUids.SetEquals(afterUids) || beforeUids.Any(uid => uid > highWater))
            throw new InvalidDataException("Rune chance must conserve its complete registered physical UID set.");
        var beforeItems = PlayerItemSnapshotLoader.Parse(items.Before); var afterItems = PlayerItemSnapshotLoader.Parse(items.After);
        if (!LegacyItemSaveGraph.TryCapture(beforeItems, out _) || !LegacyItemSaveGraph.TryCapture(afterItems, out var graph) ||
            !WorldPacketHandlers.ItemSpellRawRecordsMatch(items.Before, PlayerItemSnapshotLoader.ToLegacyBytes(beforeItems)) || !graph.HasBytes(items.After))
            throw new InvalidDataException("Recorded Rune chance item graphs are not lossless.");
        var beforeSpells = ReadOwnedItemSpellRows(spells.Before); _ = ReadOwnedItemSpellRows(spells.After);
        LegacyRuneSpellOrigins.RequireSavedRows(beforeCharacter, spells.Before);
        try
        {
            var projected = WorldPacketHandlers.ProjectRecordedRuneChance(beforeCharacter, beforeItems, beforeSpells, operation);
            if (projected.Character.MinHitPoints != beforeCharacter.MinHitPoints ||
                !LegacyRuneSpellOrigins.ProjectCharacterImage(character.Before, projected.Character).AsSpan().SequenceEqual(character.After) ||
                !PlayerItemSnapshotLoader.ToLegacyBytes(projected.Items).AsSpan().SequenceEqual(items.After) ||
                !LegacyRuneSpellOrigins.ProjectSpellImage(spells.Before, projected.Spells).AsSpan().SequenceEqual(spells.After))
                throw new InvalidDataException("Recorded Rune chance afterimages differ from the exact captured decision.");
        }
        catch (InvalidOperationException error)
        { throw new InvalidDataException("Recorded Rune chance cannot produce the captured outcome.", error); }
    }
}

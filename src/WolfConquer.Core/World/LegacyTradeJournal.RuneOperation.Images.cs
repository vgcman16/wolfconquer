using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void ValidateRecordedRuneImages(JournalRecord record, uint highWater)
    {
        var operation = record.RuneOperation ?? throw new InvalidDataException("Recorded Rune operation is missing.");
        if (record.Version is not (12 or 14)) throw new InvalidDataException("Unexpected Rune record version.");
        var semantics = record.Version == 14 ? LegacyRuneMappingSemantics.RawSelf : LegacyRuneMappingSemantics.LegacyDisplay;
        operation.Validate(semantics); RequireCharacterItemsSpellsOrder(record.Changes);
        var character = record.Changes[0]; var items = record.Changes[1]; var spells = record.Changes[2];
        if (character.FileId is <= 0 or > uint.MaxValue || character.Before is null || items.Before is null ||
            ReadCharacterUid(character.Before) != items.FileId || ReadCharacterUid(character.After) != items.FileId || items.FileId != spells.FileId)
            throw new InvalidDataException("Recorded Rune participant identities differ.");
        var beforeCharacter = ReadRuneCharacter(character.Before);
        _ = ReadRuneCharacter(character.After);
        var beforeUids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.Before).ToHashSet();
        var afterUids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.After).ToHashSet();
        if (!beforeUids.SetEquals(afterUids) || beforeUids.Any(uid => uid > highWater))
            throw new InvalidDataException("Recorded Rune operation must conserve its registered physical UID set.");
        var beforeItems = PlayerItemSnapshotLoader.Parse(items.Before); var afterItems = PlayerItemSnapshotLoader.Parse(items.After);
        if (!LegacyItemSaveGraph.TryCapture(beforeItems, out _) || !LegacyItemSaveGraph.TryCapture(afterItems, out var graph) ||
            !WorldPacketHandlers.ItemSpellRawRecordsMatch(items.Before, PlayerItemSnapshotLoader.ToLegacyBytes(beforeItems)) || !graph.HasBytes(items.After))
            throw new InvalidDataException("Recorded Rune item graphs are not lossless.");
        var beforeSpells = ReadOwnedItemSpellRows(spells.Before); _ = ReadOwnedItemSpellRows(spells.After);
        LegacyRuneSpellOrigins.RequireSavedRows(beforeCharacter, spells.Before);
        if (!LegacyRuneOperation.RequiredMetadata(beforeItems, operation.SelectedUid).SequenceEqual(operation.Metadata.Select(row => row.ItemId)))
            throw new InvalidDataException("Recorded Rune metadata has missing or unrelated dependencies.");
        LegacyRuneOperationProjection projected;
        try { projected = WorldPacketHandlers.ProjectRecordedRuneOperation(beforeCharacter, beforeItems, beforeSpells, operation, semantics); }
        catch (InvalidOperationException error)
        { throw new InvalidDataException("Recorded Rune operation cannot produce the captured outcome.", error); }
        if (!LegacyRuneSpellOrigins.ProjectCharacterImage(character.Before, projected.Character).AsSpan().SequenceEqual(character.After) ||
            !PlayerItemSnapshotLoader.ToLegacyBytes(projected.Items).AsSpan().SequenceEqual(items.After) ||
            !LegacyRuneSpellOrigins.ProjectSpellImage(spells.Before, projected.Spells).AsSpan().SequenceEqual(spells.After))
            throw new InvalidDataException("Recorded Rune afterimages differ from the exact captured operation.");
    }

    private static CharacterSnapshot ReadRuneCharacter(byte[] image)
    {
        var parsed = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(image, out _));
        LegacyOrdinaryAdmissionValidation.ValidateCharacter(parsed);
        if (!LegacyWarehouseMoneyState.TryRead(parsed.Attributes, out _) || parsed.BoundConquerPoints > int.MaxValue ||
            LegacyTradeEscrowClaims.ReadJournalWallet(image).Claim is not null)
            throw new InvalidDataException("Recorded Rune currency or unresolved escrow is invalid.");
        return parsed;
    }
}

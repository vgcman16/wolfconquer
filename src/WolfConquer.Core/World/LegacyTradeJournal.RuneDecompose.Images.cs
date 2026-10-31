using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void ValidateRecordedRuneDecomposeImages(JournalRecord record, uint highWater)
    {
        if (record.Version != 18 || record.RuneDecompose is not { } operation)
            throw new InvalidDataException("Recorded Rune decomposition is missing or misversioned.");
        operation.Validate(); RequireCharacterItemsSpellsOrder(record.Changes);
        var character = record.Changes[0]; var items = record.Changes[1]; var spells = record.Changes[2];
        if (character.FileId is <= 0 or > uint.MaxValue || character.Before is null || items.Before is null ||
            operation.Request.AccountId != character.FileId ||
            ReadCharacterUid(character.Before) != operation.Request.CharacterUid ||
            ReadCharacterUid(character.After) != operation.Request.CharacterUid ||
            items.FileId != operation.Request.CharacterUid || spells.FileId != operation.Request.CharacterUid)
            throw new InvalidDataException("Rune decomposition account, Character and item/spell owners differ.");
        var beforeCharacter = ReadRuneCharacter(character.Before); var afterCharacter = ReadRuneCharacter(character.After);
        if (beforeCharacter.MinHitPoints != afterCharacter.MinHitPoints)
            throw new InvalidDataException("Rune decomposition cannot change saved hitpoints.");
        var digest = LegacyRuneDecomposeRecipeDigest.Compute(operation.Allocation.DomainId,
            operation.Request.AccountId, operation.Request.CharacterUid, operation.SelectedUid, operation.Progress,
            operation.RuneMetadata, operation.RewardMetadata, operation.FragmentColor,
            character.Before, items.Before, spells.Before);
        if (!StringComparer.Ordinal.Equals(digest, operation.Request.PayloadSha256))
            throw new InvalidDataException("Rune decomposition allocation recipe differs from its complete captured inputs.");

        var beforeUids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.Before).ToHashSet();
        var afterUids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.After).ToHashSet();
        var expectedUids = new HashSet<uint>(beforeUids);
        if (!expectedUids.Remove(operation.SelectedUid) || beforeUids.Any(uid => uid > highWater) || afterUids.Any(uid => uid > highWater))
            throw new InvalidDataException("Rune decomposition lacks its registered selected UID or exceeds the pinned namespace.");
        for (var index = 0; index < operation.Allocation.Count; index++)
        {
            var uid = checked(operation.Allocation.First + (uint)index);
            if (beforeUids.Contains(uid) || !expectedUids.Add(uid))
                throw new InvalidDataException("Rune decomposition allocation overlaps captured physical ownership.");
        }
        if (!expectedUids.SetEquals(afterUids))
            throw new InvalidDataException("Rune decomposition must retire only its selected root and create exactly its registered range.");
        var beforeItems = PlayerItemSnapshotLoader.Parse(items.Before); var afterItems = PlayerItemSnapshotLoader.Parse(items.After);
        if (!LegacyItemSaveGraph.TryCapture(beforeItems, out _) || !LegacyItemSaveGraph.TryCapture(afterItems, out var graph) ||
            !WorldPacketHandlers.ItemSpellRawRecordsMatch(items.Before, PlayerItemSnapshotLoader.ToLegacyBytes(beforeItems)) || !graph.HasBytes(items.After))
            throw new InvalidDataException("Rune decomposition item images are not lossless.");
        var target = beforeItems.RuneItems.SingleOrDefault(row => row.Uid == operation.SelectedUid)
            ?? throw new InvalidDataException("Rune decomposition selected UID is not a captured Rune root.");
        var beforeSpells = ReadOwnedItemSpellRows(spells.Before); _ = ReadOwnedItemSpellRows(spells.After);
        bool stored = target.Position is 211 or 212;
        if (!stored) LegacyRuneSpellOrigins.RequireSavedRows(beforeCharacter, spells.Before);
        try
        {
            var projected = WorldPacketHandlers.ProjectRecordedRuneDecompose(beforeCharacter, beforeItems, beforeSpells, operation);
            bool characterMatches = stored ? Matches(character.Before, character.After) :
                LegacyRuneSpellOrigins.ProjectCharacterImage(character.Before, projected.Character).AsSpan().SequenceEqual(character.After);
            bool spellsMatch = stored ? spells.Before is null
                ? spells.After.AsSpan().SequenceEqual(new byte[sizeof(int)]) : Matches(spells.Before, spells.After)
                : LegacyRuneSpellOrigins.ProjectSpellImage(spells.Before, projected.Spells).AsSpan().SequenceEqual(spells.After);
            if (projected.Character.MinHitPoints != beforeCharacter.MinHitPoints || !characterMatches || !spellsMatch ||
                !PlayerItemSnapshotLoader.ToLegacyBytes(projected.Items).AsSpan().SequenceEqual(items.After))
                throw new InvalidDataException("Rune decomposition afterimages differ from its exact recorded recipe.");
        }
        catch (InvalidOperationException error)
        { throw new InvalidDataException("Recorded Rune decomposition cannot produce the captured outcome.", error); }
    }
}

using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void ValidateRecordedRuneBagImages(JournalRecord record, uint highWater)
    {
        if (record.Version != 20 || record.RuneBag is not { } operation) throw new InvalidDataException("Missing RuneBag20 recipe.");
        operation.Validate(); RequireCharacterItemsSpellsOrder(record.Changes);
        var character = record.Changes[0]; var items = record.Changes[1]; var spells = record.Changes[2];
        if (character.FileId is <= 0 or > uint.MaxValue || character.Before is null || items.Before is null ||
            !Matches(character.Before, character.After) || ReadCharacterUid(character.Before) != items.FileId || items.FileId != spells.FileId)
            throw new InvalidDataException("RuneBag owners or unchanged Character image differ.");
        var parsedCharacter = ReadRuneCharacter(character.Before);
        var beforeSpells = ReadOwnedItemSpellRows(spells.Before); _ = ReadOwnedItemSpellRows(spells.After);
        if (spells.Before is null ? !spells.After.AsSpan().SequenceEqual(new byte[sizeof(int)]) : !Matches(spells.Before, spells.After))
            throw new InvalidDataException("RuneBag cannot change complete spell rows.");
        var beforeUids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.Before).ToHashSet();
        var afterUids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.After).ToHashSet();
        if (beforeUids.Any(uid => uid > highWater) || afterUids.Any(uid => uid > highWater))
            throw new InvalidDataException("RuneBag physical IDs exceed the pinned namespace.");
        var expected = new HashSet<uint>(beforeUids);
        if (!operation.IntoBag && !expected.Remove(operation.SelectedUid)) throw new InvalidDataException("Missing RuneBag selected physical UID.");
        if (operation.Allocation is { } allocation)
        {
            if (operation.Request!.AccountId != character.FileId || operation.Request.CharacterUid != items.FileId ||
                beforeUids.Contains(allocation.First) || !expected.Add(allocation.First) ||
                operation.Request.PayloadSha256 != LegacyRuneBagRecipeDigest.Compute(allocation.DomainId,
                    operation.Request.AccountId, operation.Request.CharacterUid, operation.SelectedUid, operation.IntoBag,
                    operation.Metadata, character.Before, items.Before, spells.Before))
                throw new InvalidDataException("RuneBag allocation differs from complete captured recipe.");
        }
        if (!expected.SetEquals(afterUids)) throw new InvalidDataException("RuneBag physical UID transition differs from its exact recipe.");
        var beforeItems = PlayerItemSnapshotLoader.Parse(items.Before); var afterItems = PlayerItemSnapshotLoader.Parse(items.After);
        if (!LegacyItemSaveGraph.TryCapture(beforeItems, out _) || !LegacyItemSaveGraph.TryCapture(afterItems, out var graph) ||
            !WorldPacketHandlers.ItemSpellRawRecordsMatch(items.Before, PlayerItemSnapshotLoader.ToLegacyBytes(beforeItems)) || !graph.HasBytes(items.After))
            throw new InvalidDataException("RuneBag item images are not lossless.");
        try
        {
            var projected = WorldPacketHandlers.ProjectRecordedRuneBag(parsedCharacter, beforeItems, beforeSpells, operation);
            if (!PlayerItemSnapshotLoader.ToLegacyBytes(projected.Items).AsSpan().SequenceEqual(items.After))
                throw new InvalidDataException("RuneBag afterimage differs from its exact full-row recipe.");
        }
        catch (InvalidOperationException error) { throw new InvalidDataException("RuneBag recipe cannot produce this outcome.", error); }
    }
}

using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void ValidateCharacterItemsSpellsChanges(LegacyTradeJournalChange[] changes)
    {
        if (changes.Length != 3 || changes.Count(change => change.Kind == LegacyTradeJournalFileKind.Character) != 1 ||
            changes.Count(change => change.Kind == LegacyTradeJournalFileKind.Items) != 1 ||
            changes.Count(change => change.Kind == LegacyTradeJournalFileKind.Spells) != 1)
            throw new ArgumentException("A Character, Items and Spells transaction requires exactly three typed targets.");
        var character = changes.Single(change => change.Kind == LegacyTradeJournalFileKind.Character);
        var items = changes.Single(change => change.Kind == LegacyTradeJournalFileKind.Items);
        var spells = changes.Single(change => change.Kind == LegacyTradeJournalFileKind.Spells);
        if (character.FileId <= 0 || items.FileId is <= 0 or > uint.MaxValue || items.FileId != spells.FileId ||
            character.Before is null || items.Before is null ||
            changes.Any(change => change.After is null || change.After.Length > MaxTargetBytes || change.Before?.Length > MaxTargetBytes))
            throw new ArgumentException("Invalid Character, Items or Spells image or identity.");
        if (ReadCharacterUid(character.Before) != items.FileId || ReadCharacterUid(character.After) != items.FileId)
            throw new InvalidDataException("Character image UID differs from Items or Spells file identity.");
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(items.Before);
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(items.After);
        if (spells.Before is not null) ValidateSpellsImage(spells.Before);
        ValidateSpellsImage(spells.After);
    }
}

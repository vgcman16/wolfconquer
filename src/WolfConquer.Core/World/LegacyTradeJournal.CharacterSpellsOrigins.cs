using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    // Version8 DATA validation only. Real live retirement still requires its
    // concrete class/clear producer. New grants/levels belong to typed22/23.
    private static void ValidateOrdinaryCharacterSpellsDerivedOrigins(LegacyTradeJournalChange[] changes)
    {
        var character = changes.Single(change => change.Kind == LegacyTradeJournalFileKind.Character);
        var spells = changes.Single(change => change.Kind == LegacyTradeJournalFileKind.Spells);
        var before = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(character.Before!, out _));
        var after = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(character.After, out _));
        LegacyDerivedSpellOrigins.RequirePreservedOrRemoved(before, spells.Before, after, spells.After);
    }
}

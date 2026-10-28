using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void ValidateCharacterSpellsChanges(LegacyTradeJournalChange[] changes)
    {
        if (changes.Length != 2 || changes.Count(change => change.Kind == LegacyTradeJournalFileKind.Character) != 1 ||
            changes.Count(change => change.Kind == LegacyTradeJournalFileKind.Spells) != 1)
            throw new ArgumentException("A Character and Spells transaction requires exactly two typed targets.");
        var character = changes.Single(change => change.Kind == LegacyTradeJournalFileKind.Character);
        var spells = changes.Single(change => change.Kind == LegacyTradeJournalFileKind.Spells);
        if (character.FileId <= 0 || spells.FileId is <= 0 or > uint.MaxValue || character.Before is null ||
            character.After is null || spells.After is null || character.Before.Length > MaxTargetBytes ||
            character.After.Length > MaxTargetBytes || spells.Before?.Length > MaxTargetBytes ||
            spells.After.Length > MaxTargetBytes)
            throw new ArgumentException("Invalid Character or Spells image or identity.");
        if (ReadCharacterUid(character.Before) != spells.FileId || ReadCharacterUid(character.After) != spells.FileId)
            throw new InvalidDataException("Character image UID differs from the Spells file identity.");
        if (spells.Before is not null) ValidateSpellsImage(spells.Before);
        ValidateSpellsImage(spells.After);
    }

    private static long ReadCharacterUid(byte[] image)
    {
        var text = CharacterSnapshotFileEncoding.Decode(image, out _);
        RequireSingleCharacterSection(text);
        var character = CharacterSnapshotParser.ParseText(text);
        if (character.Uid is <= 0 or > uint.MaxValue)
            throw new InvalidDataException("Character image has an invalid UID.");
        return character.Uid;
    }

    private static void RequireSingleCharacterSection(string text)
    {
        var sections = 0;
        var active = false;
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } raw)
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                active = line[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                if (active && ++sections != 1)
                    throw new InvalidDataException("Character image contains duplicate Character sections.");
                continue;
            }
            if (!active || line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            var equals = raw.IndexOf('=');
            if (equals <= 0)
                throw new InvalidDataException("Character section contains a malformed field.");
            var key = raw[..equals].Trim();
            if (key.Length == 0 || !keys.Add(key))
                throw new InvalidDataException("Character section contains an empty or duplicate key.");
        }
        if (sections != 1) throw new InvalidDataException("Character image requires exactly one Character section.");
    }

    private static void ValidateSpellsImage(byte[] image)
    {
        var spells = PlayerSpellSnapshotLoader.Parse(image).Spells;
        if (spells.Any(spell => spell.Id == 0 || spell.Experience < 0) ||
            spells.Select(spell => spell.Id).Distinct().Count() != spells.Count)
            throw new InvalidDataException("Invalid Spells image membership.");
    }
}

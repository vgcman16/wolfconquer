using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static void ValidateOrdinaryAdmissionCharacterImage(byte[] image, long account)
    {
        var text = CharacterSnapshotFileEncoding.Decode(image, out _);
        var sections = 0; var active = false;
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } raw)
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                active = line[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                if (active && ++sections != 1) throw new InvalidDataException("Ambiguous admission Character section.");
                continue;
            }
            if (!active || line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            var equals = raw.IndexOf('=');
            if (equals <= 0 || !keys.Add(raw[..equals].Trim())) throw new InvalidDataException("Ambiguous admission Character field.");
        }
        if (sections != 1) throw new InvalidDataException("Missing admission Character section.");
        var character = CharacterSnapshotParser.ParseText(text);
        if (character.Uid != account) throw new InvalidDataException("Admission Character UID differs from the actual file identity.");
        LegacyOrdinaryAdmissionValidation.ValidateCharacter(character);
    }

    private static void ValidateAdmissionSavedImage(LegacyTradeJournalChange row, byte[]? image)
    {
        if (image is null) return;
        switch (row.Kind)
        {
            case LegacyTradeJournalFileKind.Character: ValidateOrdinaryAdmissionCharacterImage(image, row.FileId); break;
            case LegacyTradeJournalFileKind.Items: LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(image); break;
            case LegacyTradeJournalFileKind.Spells:
                var spells = PlayerSpellSnapshotLoader.Parse(image);
                if (spells.Spells.Any(spell => spell.Id == 0 || spell.Experience < 0) ||
                    spells.Spells.Select(spell => spell.Id).Distinct().Count() != spells.Spells.Count)
                    throw new InvalidDataException("Invalid admission spell membership.");
                break;
            case LegacyTradeJournalFileKind.Proficiencies:
                var profs = PlayerProficiencySnapshotLoader.Parse(image);
                if (profs.Proficiencies.Select(prof => prof.Id).Distinct().Count() != profs.Proficiencies.Count)
                    throw new InvalidDataException("Invalid admission proficiency membership.");
                break;
        }
    }
}

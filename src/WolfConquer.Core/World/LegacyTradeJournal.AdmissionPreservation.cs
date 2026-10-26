using System.Text.Json;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static bool IsOrdinaryAdmissionVersion(int version) => version is 6 or 7;

    private static long ReadAdmissionTime(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out var ticks) ||
            ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            throw new JsonException("Invalid preservation admission time.");
        return ticks;
    }

    private static void ValidatePreservedAdmission(JournalRecord record)
    {
        if (record.AdmissionPreparedAt is not { } ticks || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            throw new InvalidDataException("Missing preservation admission time.");
        var at = new DateTime(ticks, DateTimeKind.Unspecified);
        var character = record.Changes.Single(row => row.Kind == LegacyTradeJournalFileKind.Character);
        if (character.Before is null || !LegacyOrdinaryAdmissionImages.ProjectCharacter(character.Before, at).AsSpan()
                .SequenceEqual(character.After))
            throw new InvalidDataException("Ordinary admission changes unrelated Character state.");
        var beforeCharacter = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(character.Before, out _));
        foreach (var row in record.Changes.Where(row => row.Kind != LegacyTradeJournalFileKind.Character))
        {
            var expected = row.Kind switch
            {
                LegacyTradeJournalFileKind.Items => row.Before,
                LegacyTradeJournalFileKind.Spells => ExpectedSpells(beforeCharacter, row.Before),
                LegacyTradeJournalFileKind.Proficiencies => row.Before ?? new byte[sizeof(int)],
                _ => throw new InvalidDataException("Unsupported preservation admission target."),
            };
            if (!Matches(row.After, expected)) throw new InvalidDataException("Ordinary admission replaces saved business state.");
        }
        foreach (var input in record.AdmissionInputs!)
            if (input.Kind == LegacyTradeJournalFileKind.Spells &&
                !Matches(input.Image, ExpectedSpells(beforeCharacter, input.Image)))
                throw new InvalidDataException("Required additive ordinary spell grant is missing.");
    }

    private static byte[]? ExpectedSpells(CharacterSnapshot character, byte[]? before)
    {
        if (before is null && LegacyClassOwnedSpellGrantProfile.GetSpellIds(character).Count == 0) return null;
        return LegacyOrdinaryAdmissionSpellImages.Project(character, before);
    }
}

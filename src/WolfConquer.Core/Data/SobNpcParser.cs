using System.Globalization;

namespace WolfConquer.Core.Data;

public static class SobNpcParser
{
    public static SobNpc ParseLine(string line, int sourceLine = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);

        var fields = line.Split(',', StringSplitOptions.None | StringSplitOptions.TrimEntries);
        if (fields.Length is not (11 or 12))
        {
            throw new FormatException($"SobNpc row has {fields.Length} fields; expected 11 or 12.");
        }

        return new SobNpc(
            ParseInt(fields[0], "sob npc id"),
            fields[1],
            ParseInt(fields[2], "sob npc kind"),
            ParseInt(fields[3], "sob npc lookface"),
            ParseInt(fields[4], "sob npc map id"),
            ParseInt(fields[5], "sob npc x"),
            ParseInt(fields[6], "sob npc y"),
            ParseInt(fields[7], "sob npc life"),
            ParseInt(fields[8], "sob npc max life"),
            ParseInt(fields[9], "sob npc unknown9"),
            ParseOptionalInt(fields[10], "sob npc unknown10"),
            fields.Length == 12 ? fields[11] : null,
            sourceLine,
            line,
            fields);
    }

    private static int ParseInt(string value, string fieldName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid {fieldName}: '{value}'.");
        }

        return parsed;
    }

    private static int? ParseOptionalInt(string value, string fieldName)
    {
        return value.Length == 0 ? null : ParseInt(value, fieldName);
    }
}

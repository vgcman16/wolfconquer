using System.Globalization;

namespace WolfConquer.Core.Data;

public static class NpcParser
{
    public static Npc ParseLine(string line, int sourceLine = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);

        var fields = line.Split(',', StringSplitOptions.None | StringSplitOptions.TrimEntries);
        if (fields.Length is not (6 or 7))
        {
            throw new FormatException($"NPC row has {fields.Length} fields; expected 6 or 7.");
        }

        return new Npc(
            ParseInt(fields[0], "npc id"),
            ParseInt(fields[1], "npc kind"),
            ParseInt(fields[2], "npc lookface"),
            ParseInt(fields[3], "npc map id"),
            ParseInt(fields[4], "npc x"),
            ParseInt(fields[5], "npc y"),
            fields.Length == 7 ? ParseInt(fields[6], "npc optional flag") : null,
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
}

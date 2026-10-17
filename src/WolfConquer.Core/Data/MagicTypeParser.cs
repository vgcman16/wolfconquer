using System.Globalization;

namespace WolfConquer.Core.Data;

public static class MagicTypeParser
{
    public static MagicType ParseLine(string line, int? expectedFieldCount = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);

        var fields = AtAtTableParser.SplitRow(line);

        if (expectedFieldCount is not null && fields.Count != expectedFieldCount.Value)
        {
            throw new FormatException($"magictype row has {fields.Count} fields; expected {expectedFieldCount.Value}.");
        }

        if (fields.Count < 4)
        {
            throw new FormatException("magictype row must include id, type id, sort, and name fields.");
        }

        return new MagicType(ParseInt(fields[0], "magic id"), ParseInt(fields[1], "magic type id"), fields[3], fields);
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

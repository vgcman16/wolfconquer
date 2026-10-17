using System.Globalization;

namespace WolfConquer.Core.Data;

public static class ItemTypeParser
{
    public static ItemType ParseLine(string line, int? expectedFieldCount = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);

        var fields = AtAtTableParser.SplitRow(line);
        ValidateFieldCount(fields, expectedFieldCount, "itemtype");

        if (fields.Count < 2)
        {
            throw new FormatException("itemtype row must include at least id and name fields.");
        }

        return new ItemType(ParseInt(fields[0], "item id"), fields[1], fields);
    }

    private static void ValidateFieldCount(IReadOnlyCollection<string> fields, int? expectedFieldCount, string tableName)
    {
        if (expectedFieldCount is not null && fields.Count != expectedFieldCount.Value)
        {
            throw new FormatException($"{tableName} row has {fields.Count} fields; expected {expectedFieldCount.Value}.");
        }
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

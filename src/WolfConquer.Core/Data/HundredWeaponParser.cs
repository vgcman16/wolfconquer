using System.Globalization;

namespace WolfConquer.Core.Data;

public static class HundredWeaponParser
{
    private const int ExpectedFieldCount = 12;
    private const int NamedFieldCount = 3;

    public static HundredWeapon ParseRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count != ExpectedFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected {ExpectedFieldCount}.");
        }

        if (fields[^1].Length != 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing @@ field.");
        }

        var values = new int[ExpectedFieldCount - NamedFieldCount - 1];
        for (var index = NamedFieldCount; index < fields.Count - 1; index++)
        {
            values[index - NamedFieldCount] = ParseInt(fields[index], $"hundred weapon value {index - NamedFieldCount}");
        }

        return new HundredWeapon(
            ParseInt(fields[0], "hundred weapon id"),
            ParseInt(fields[1], "hundred weapon type"),
            ParseInt(fields[2], "hundred weapon level"),
            values,
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static int ParseInt(string value, string name)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }
}

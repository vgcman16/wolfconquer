using System.Globalization;

namespace WolfConquer.Core.Data;

public static class SpecialEffectAttributeParser
{
    private const int ExpectedFieldCount = 9;
    private const int NamedFieldCount = 2;
    private const int ValueCount = ExpectedFieldCount - NamedFieldCount - 1;

    public static SpecialEffectAttribute ParseRow(string sourceFile, int lineNumber, string row)
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

        var values = new int[ValueCount];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = ParseInt(fields[NamedFieldCount + index], $"special effect attribute value {index}");
        }

        return new SpecialEffectAttribute(
            ParseInt(fields[0], "special effect attribute id"),
            ParseInt(fields[1], "special effect attribute type"),
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

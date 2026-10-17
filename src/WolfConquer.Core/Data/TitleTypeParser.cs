namespace WolfConquer.Core.Data;

public static class TitleTypeParser
{
    public static TitleType ParseRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count != 9)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected 9.");
        }

        return new TitleType(
            ParseInt(fields[0], "title id"),
            ParseInt(fields[1], "title type id"),
            fields[2],
            ParseInt(fields[3], "title unknown0"),
            ParseInt(fields[4], "title unknown1"),
            ParseInt(fields[5], "title unknown2"),
            ParseInt(fields[6], "title unknown3"),
            ParseInt(fields[7], "title score"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static int ParseInt(string value, string name)
    {
        return int.TryParse(value, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }
}

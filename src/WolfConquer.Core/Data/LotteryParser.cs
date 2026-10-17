namespace WolfConquer.Core.Data;

public static class LotteryParser
{
    public static LotteryDictionaryEntry ParseDictionaryRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count != 7)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected 7.");
        }

        return new LotteryDictionaryEntry(
            ParseInt(fields[0], "lottery dictionary id"),
            ParseInt(fields[1], "lottery dictionary type"),
            ParseInt(fields[2], "lottery dictionary subtype"),
            ParseInt(fields[3], "lottery dictionary value"),
            ParseInt(fields[4], "lottery dictionary unknown0"),
            ParseInt(fields[5], "lottery dictionary weight"),
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

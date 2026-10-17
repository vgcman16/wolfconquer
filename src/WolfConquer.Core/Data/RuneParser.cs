namespace WolfConquer.Core.Data;

public static class RuneParser
{
    public static RuneLevelExperience ParseLevelExperienceRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count != 5)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected 5.");
        }

        if (fields[^1].Length != 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing @@ field.");
        }

        return new RuneLevelExperience(
            ParseInt(fields[0], "rune level experience id"),
            ParseInt(fields[1], "rune type"),
            ParseInt(fields[2], "rune level"),
            ParseLong(fields[3], "rune experience"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static RuneStorageAttribute ParseStorageAttributeRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count != 9)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected 9.");
        }

        if (fields[^1].Length != 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing @@ field.");
        }

        var bonuses = new[]
        {
            new RuneStorageAttributeBonus(
                ParseInt(fields[2], "rune storage attribute type 1"),
                ParseInt(fields[3], "rune storage attribute value 1")),
            new RuneStorageAttributeBonus(
                ParseInt(fields[4], "rune storage attribute type 2"),
                ParseInt(fields[5], "rune storage attribute value 2")),
            new RuneStorageAttributeBonus(
                ParseInt(fields[6], "rune storage attribute type 3"),
                ParseInt(fields[7], "rune storage attribute value 3")),
        };

        return new RuneStorageAttribute(
            ParseInt(fields[0], "rune storage attribute id"),
            ParseInt(fields[1], "rune storage score"),
            bonuses,
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

    private static long ParseLong(string value, string name)
    {
        return long.TryParse(value, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }
}

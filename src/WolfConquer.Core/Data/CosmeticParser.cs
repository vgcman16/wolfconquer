namespace WolfConquer.Core.Data;

public static class CosmeticParser
{
    public static HairColorType ParseHairColorTypeRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count != 6)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected 6.");
        }

        if (fields[^1].Length != 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing @@ field.");
        }

        return new HairColorType(
            ParseInt(fields[0], "hair color id"),
            ParseInt(fields[1], "hair color index"),
            ParseInt(fields[2], "hair color unlock type"),
            ParseInt(fields[3], "hair color item type id"),
            ParseInt(fields[4], "hair color cost"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static HairFaceStorageType ParseHairFaceStorageTypeRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count != 12)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected 12.");
        }

        if (fields[^1].Length != 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing @@ field.");
        }

        return new HairFaceStorageType(
            ParseInt(fields[0], "hair face storage category type"),
            ParseInt(fields[1], "hair face storage id"),
            ParseInt(fields[2], "hair face storage unknown0"),
            fields[3],
            ParseInt(fields[4], "hair face storage unlock type"),
            ParseInt(fields[5], "hair face storage required value"),
            ParseInt(fields[6], "hair face storage unknown1"),
            ParseInt(fields[7], "hair face storage cost"),
            ParseInt(fields[8], "hair face storage unknown2"),
            fields[9],
            ParseInt(fields[10], "hair face storage sort"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static CoatStorageType ParseCoatStorageTypeRow(string sourceFile, int lineNumber, string row)
    {
        var fields = ParseAtAtRow(sourceFile, lineNumber, row, expectedFieldCount: 9);

        return new CoatStorageType(
            ParseInt(fields[0], "coat storage item type id"),
            ParseLong(fields[1], "coat storage type"),
            ParseIntRange(fields, start: 2, count: 6, name: "coat storage parameter"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static CoatStorageAttribute ParseCoatStorageAttributeRow(string sourceFile, int lineNumber, string row)
    {
        var fields = ParseAtAtRow(sourceFile, lineNumber, row, expectedFieldCount: 13);

        return new CoatStorageAttribute(
            ParseInt(fields[0], "coat storage attribute id"),
            ParseLong(fields[1], "coat storage type"),
            ParseInt(fields[2], "coat storage required storage score"),
            [
                new CosmeticAttributeBonus(ParseInt(fields[3], "coat storage bonus type 0"), ParseInt(fields[4], "coat storage bonus value 0")),
                new CosmeticAttributeBonus(ParseInt(fields[5], "coat storage bonus type 1"), ParseInt(fields[6], "coat storage bonus value 1")),
                new CosmeticAttributeBonus(ParseInt(fields[7], "coat storage bonus type 2"), ParseInt(fields[8], "coat storage bonus value 2")),
                new CosmeticAttributeBonus(ParseInt(fields[9], "coat storage bonus type 3"), ParseInt(fields[10], "coat storage bonus value 3")),
            ],
            ParseInt(fields[11], "coat storage reserved value"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static CoatColorRule ParseCoatColorRuleRow(string sourceFile, int lineNumber, string row)
    {
        var fields = ParseAtAtRow(sourceFile, lineNumber, row, expectedFieldCount: 20);

        return new CoatColorRule(
            ParseInt(fields[0], "coat color rule item type id"),
            ParseIntRange(fields, start: 1, count: 18, name: "coat color rule parameter"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static IReadOnlyList<string> ParseAtAtRow(string sourceFile, int lineNumber, string row, int expectedFieldCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count != expectedFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected {expectedFieldCount}.");
        }

        if (fields[^1].Length != 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing @@ field.");
        }

        return fields;
    }

    private static IReadOnlyList<int> ParseIntRange(IReadOnlyList<string> fields, int start, int count, string name)
    {
        var values = new int[count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ParseInt(fields[start + i], $"{name}[{i}]");
        }

        return values;
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

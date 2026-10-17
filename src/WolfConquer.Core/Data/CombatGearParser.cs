namespace WolfConquer.Core.Data;

public static class CombatGearParser
{
    public static CombatGear ParseRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count != 23)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected 23.");
        }

        if (fields[^1].Length != 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing @@ field.");
        }

        return new CombatGear(
            ParseInt(fields[0], "combat gear id"),
            ParseInt(fields[1], "combat gear type id"),
            ParseInt(fields[2], "combat gear level"),
            ParseInt(fields[3], "combat gear up experience"),
            ParseInt(fields[4], "combat gear life"),
            ParseInt(fields[5], "combat gear attack"),
            ParseInt(fields[6], "combat gear defense"),
            ParseInt(fields[7], "combat gear unknown stat 0"),
            ParseInt(fields[8], "combat gear magic defense"),
            ParseIntRange(fields, start: 9, count: 9, name: "combat gear magic type ids"),
            ParseInt(fields[18], "combat gear score"),
            ParseInt(fields[19], "combat gear unknown0"),
            ParseInt(fields[20], "combat gear unknown1"),
            ParseInt(fields[21], "combat gear unknown2"),
            sourceFile,
            lineNumber,
            row,
            fields);
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
}

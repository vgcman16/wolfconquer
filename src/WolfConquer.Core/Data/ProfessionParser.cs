namespace WolfConquer.Core.Data;

public static class ProfessionParser
{
    public static ProfessionLevelUp ParseLevelUpRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = SplitAndValidate(sourceFile, lineNumber, row, expectedFieldCount: 7);
        return new ProfessionLevelUp(
            ParseInt(fields[0], "profession level-up id"),
            ParseInt(fields[1], "profession code"),
            ParseInt(fields[2], "required level"),
            ParseInt(fields[3], "required metempsychosis"),
            ParseInt(fields[4], "required money"),
            ParseInt(fields[5], "required proficiency"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static ProfessionLevelBenefit ParseLevelBenefitRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = SplitAndValidate(sourceFile, lineNumber, row, expectedFieldCount: 9);
        return new ProfessionLevelBenefit(
            ParseInt(fields[0], "profession benefit id"),
            ParseInt(fields[1], "profession code"),
            ParseInt(fields[2], "profession benefit rank"),
            ParseIntRange(fields, start: 3, count: 5, name: "profession benefit values"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static ProfessionTitleBenefit ParseTitleBenefitRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = SplitAndValidate(sourceFile, lineNumber, row, expectedFieldCount: 9);
        return new ProfessionTitleBenefit(
            ParseInt(fields[0], "profession title benefit id"),
            ParseInt(fields[1], "profession group"),
            ParseInt(fields[2], "profession title rank"),
            ParseIntRange(fields, start: 3, count: 5, name: "profession title benefit values"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static IReadOnlyList<string> SplitAndValidate(
        string sourceFile,
        int lineNumber,
        string row,
        int expectedFieldCount)
    {
        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count != expectedFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected {expectedFieldCount}.");
        }

        if (fields[^1].Length != 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing empty field.");
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
}

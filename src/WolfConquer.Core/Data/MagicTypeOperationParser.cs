using System.Globalization;

namespace WolfConquer.Core.Data;

public static class MagicTypeOperationParser
{
    private const int ExpectedFieldCount = 65;
    private const int ReferencedMagicTypeStartField = 5;

    public static MagicTypeOperationEntry ParseLine(string sourceFile, int lineNumber, string line)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentNullException.ThrowIfNull(line);
        ValidateLineNumber(lineNumber);

        var fields = line.Split(',');
        if (fields.Length != ExpectedFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected {ExpectedFieldCount}.");
        }

        var values = new int[ExpectedFieldCount];
        for (var index = 0; index < fields.Length; index++)
        {
            values[index] = ParseInt(sourceFile, lineNumber, fields[index], index + 1);
        }

        var referencedMagicTypeIds = values
            .Skip(ReferencedMagicTypeStartField)
            .Where(value => value != 0)
            .ToArray();

        return new MagicTypeOperationEntry(
            values[0],
            values[1],
            values[2],
            values[3],
            values[4],
            referencedMagicTypeIds,
            values,
            sourceFile,
            lineNumber,
            line,
            fields);
    }

    public static IReadOnlyList<MagicTypeOperationEntry> ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var sourceFile = Path.GetFileName(path);
        var entries = new List<MagicTypeOperationEntry>();
        using var reader = File.OpenText(path);
        var lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.Trim().Length == 0)
            {
                continue;
            }

            entries.Add(ParseLine(sourceFile, lineNumber, line));
        }

        return entries;
    }

    private static int ParseInt(string sourceFile, int lineNumber, string value, int fieldNumber)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"{sourceFile} line {lineNumber} field {fieldNumber} has invalid integer: '{value}'.");
    }

    private static void ValidateLineNumber(int lineNumber)
    {
        if (lineNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number must be positive.");
        }
    }
}

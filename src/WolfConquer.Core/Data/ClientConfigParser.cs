using System.Globalization;

namespace WolfConquer.Core.Data;

public static class ClientConfigParser
{
    private const int FieldCount = 11;

    public static IReadOnlyList<ClientConfigRecord> ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return Parse(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<ClientConfigRecord> Parse(
        string text,
        string sourceFile = "client_config.ini")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var records = new List<ClientConfigRecord>();
        using var reader = new StringReader(text);
        var lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.Trim().Length == 0)
            {
                continue;
            }

            records.Add(ParseRow(sourceFile, lineNumber, line));
        }

        return records;
    }

    public static ClientConfigRecord ParseRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentNullException.ThrowIfNull(row);
        ValidateLineNumber(lineNumber);

        var fields = row.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != FieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected {FieldCount}.");
        }

        var loginXRaw = fields[3];
        var loginYRaw = fields[4];
        var mapXRaw = fields[5];
        var mapYRaw = fields[6];

        return new ClientConfigRecord(
            ParseInt(sourceFile, lineNumber, fields[0], "client config server id"),
            fields[1],
            ParseInt(sourceFile, lineNumber, fields[2], "client config build"),
            loginXRaw,
            ParseInt(sourceFile, lineNumber, loginXRaw, "client config login X"),
            loginYRaw,
            ParseInt(sourceFile, lineNumber, loginYRaw, "client config login Y"),
            mapXRaw,
            ParseInt(sourceFile, lineNumber, mapXRaw, "client config map X"),
            mapYRaw,
            ParseInt(sourceFile, lineNumber, mapYRaw, "client config map Y"),
            ParseInt(sourceFile, lineNumber, fields[7], "client config region"),
            ParseInt(sourceFile, lineNumber, fields[8], "client config status"),
            fields[9],
            ParseInt(sourceFile, lineNumber, fields[10], "client config port"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static int ParseInt(string sourceFile, int lineNumber, string value, string fieldName)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"{sourceFile} line {lineNumber} has invalid {fieldName}: '{value}'.");
    }

    private static void ValidateLineNumber(int lineNumber)
    {
        if (lineNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number must be positive.");
        }
    }
}

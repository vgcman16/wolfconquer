using System.Globalization;

namespace WolfConquer.Core.Data;

public static class PvpEventParser
{
    private const int ArenaFieldCount = 17;
    private const int ArenaNumericFieldCount = 14;
    private const int PkTournamentFieldCount = 8;
    private const int ClassPkWarFieldCount = 5;

    public static IReadOnlyList<ArenaRecord> ParseArena(string text, string sourceFile = "Arena.ini")
    {
        return ParseCountedRows(text, sourceFile, ParseArenaRow);
    }

    public static IReadOnlyList<ArenaRecord> ParseArenaFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ParseArena(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<TeamArenaRecord> ParseTeamArena(string text, string sourceFile = "TeamArena.ini")
    {
        return ParseCountedRows(text, sourceFile, ParseTeamArenaRow);
    }

    public static IReadOnlyList<TeamArenaRecord> ParseTeamArenaFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ParseTeamArena(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static ArenaRecord ParseArenaRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);
        ValidateLineNumber(lineNumber);

        var fields = SplitSlashRow(sourceFile, lineNumber, row, ArenaFieldCount);
        var values = new int[ArenaNumericFieldCount];
        values[0] = ParseInt(sourceFile, lineNumber, fields[2], "arena level");
        values[1] = ParseInt(sourceFile, lineNumber, fields[3], "arena mesh");
        values[2] = ParseInt(sourceFile, lineNumber, fields[4], "arena lookface");
        values[3] = ParseInt(sourceFile, lineNumber, fields[5], "arena points");

        for (var index = 6; index <= 15; index++)
        {
            values[index - 2] = ParseInt(sourceFile, lineNumber, fields[index], $"arena value {index}");
        }

        return new ArenaRecord(
            ParseInt(sourceFile, lineNumber, fields[0], "arena player id"),
            fields[1],
            values,
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static TeamArenaRecord ParseTeamArenaRow(string sourceFile, int lineNumber, string row)
    {
        var arena = ParseArenaRow(sourceFile, lineNumber, row);
        return new TeamArenaRecord(
            arena.PlayerId,
            arena.PlayerName,
            arena.Values,
            arena.SourceFile,
            arena.LineNumber,
            arena.RawLine,
            arena.Fields);
    }

    public static IReadOnlyList<PkTournamentRecord> ParsePkTournament(
        string text,
        string sourceFile = "ElitePk.ini")
    {
        return ParseCountedRows(text, sourceFile, ParsePkTournamentRow);
    }

    public static IReadOnlyList<PkTournamentRecord> ParsePkTournamentFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ParsePkTournament(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static PkTournamentRecord ParsePkTournamentRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);
        ValidateLineNumber(lineNumber);

        var fields = SplitSlashRow(sourceFile, lineNumber, row, PkTournamentFieldCount);
        return new PkTournamentRecord(
            ParseInt(sourceFile, lineNumber, fields[0], "PK tournament group"),
            ParseInt(sourceFile, lineNumber, fields[1], "PK tournament slot"),
            ParseInt(sourceFile, lineNumber, fields[2], "PK tournament level"),
            fields[3],
            ParseInt(sourceFile, lineNumber, fields[4], "PK tournament lookface"),
            ParseInt(sourceFile, lineNumber, fields[5], "PK tournament status"),
            ParseInt(sourceFile, lineNumber, fields[6], "PK tournament player id"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static IReadOnlyList<ClassPkWarRecord> ParseClassPkWar(
        string text,
        string sourceFile = "ClassPkWar.ini")
    {
        return ParseCountedRows(text, sourceFile, ParseClassPkWarRow);
    }

    public static IReadOnlyList<ClassPkWarRecord> ParseClassPkWarFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ParseClassPkWar(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static ClassPkWarRecord ParseClassPkWarRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);
        ValidateLineNumber(lineNumber);

        var fields = SplitSlashRow(sourceFile, lineNumber, row, ClassPkWarFieldCount);
        return new ClassPkWarRecord(
            ParseInt(sourceFile, lineNumber, fields[0], "class PK war class id"),
            ParseInt(sourceFile, lineNumber, fields[1], "class PK war slot"),
            ParseInt(sourceFile, lineNumber, fields[2], "class PK war player id"),
            ParseInt(sourceFile, lineNumber, fields[3], "class PK war points"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static IReadOnlyList<T> ParseCountedRows<T>(
        string text,
        string sourceFile,
        Func<string, int, string, T> parseRow)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        int? expectedCount = null;
        var rows = new List<T>();
        using var reader = new StringReader(text);
        var lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (expectedCount is null)
            {
                expectedCount = ParseCountHeader(sourceFile, lineNumber, trimmed);
                continue;
            }

            rows.Add(parseRow(sourceFile, lineNumber, line));
        }

        if (expectedCount is null)
        {
            throw new FormatException($"{sourceFile} is missing Count header.");
        }

        if (rows.Count != expectedCount.Value)
        {
            throw new FormatException($"{sourceFile} declares Count={expectedCount.Value} but contains {rows.Count} rows.");
        }

        return rows;
    }

    private static int ParseCountHeader(string sourceFile, int lineNumber, string line)
    {
        const string prefix = "Count=";
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must start with a Count header.");
        }

        var countText = line[prefix.Length..].Trim();
        if (!int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has invalid Count value '{countText}'.");
        }

        return count;
    }

    private static IReadOnlyList<string> SplitSlashRow(
        string sourceFile,
        int lineNumber,
        string row,
        int expectedFieldCount)
    {
        var fields = row.Split('/', StringSplitOptions.None);
        if (fields.Length != expectedFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected {expectedFieldCount}.");
        }

        if (fields[^1].Length != 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing slash.");
        }

        return fields;
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

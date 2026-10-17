using System.Globalization;

namespace WolfConquer.Core.Data;

public static class RankingSnapshotParser
{
    private const int CombatGearRankFieldCount = 23;
    private const int PrestigeRankFieldCount = 66;
    private const int InnerPowerRankMinimumFieldCount = 5;
    private const int NewPlayerRewardFieldCount = 3;
    private const int VoteFieldCount = 4;
    private const int CouplesPkFieldCount = 3;

    public static IReadOnlyList<CombatGearRankEntry> ParseCombatGearRanksFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ParseCombatGearRanks(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<CombatGearRankEntry> ParseCombatGearRanks(
        string text,
        string sourceFile = "CombatGearRank.txt")
    {
        return ParseCountedRows(text, sourceFile, ParseCombatGearRankRow);
    }

    public static CombatGearRankEntry ParseCombatGearRankRow(string sourceFile, int lineNumber, string row)
    {
        var fields = ParseSlashFields(sourceFile, lineNumber, row, CombatGearRankFieldCount);

        return new CombatGearRankEntry(
            ParseInt(fields[0], "combat gear rank character id"),
            fields[1],
            CopyRange(fields, start: 2, count: CombatGearRankFieldCount - 3),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static IReadOnlyList<PrestigeRankEntry> ParsePrestigeRanksFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ParsePrestigeRanks(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<PrestigeRankEntry> ParsePrestigeRanks(
        string text,
        string sourceFile = "PrestigeRanking.txt")
    {
        return ParseCountedRows(text, sourceFile, ParsePrestigeRankRow);
    }

    public static PrestigeRankEntry ParsePrestigeRankRow(string sourceFile, int lineNumber, string row)
    {
        var fields = ParseSlashFields(sourceFile, lineNumber, row, PrestigeRankFieldCount);

        return new PrestigeRankEntry(
            ParseInt(fields[0], "prestige rank character id"),
            fields[1],
            CopyRange(fields, start: 2, count: PrestigeRankFieldCount - 3),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static IReadOnlyList<InnerPowerRankEntry> ParseInnerPowerRanksFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ParseInnerPowerRanks(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<InnerPowerRankEntry> ParseInnerPowerRanks(
        string text,
        string sourceFile = "InnerPower.txt")
    {
        return ParseCountedRows(text, sourceFile, ParseInnerPowerRankRow);
    }

    public static InnerPowerRankEntry ParseInnerPowerRankRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentNullException.ThrowIfNull(row);
        ValidateLineNumber(lineNumber);

        var fields = SplitSlashRow(row);
        if (fields.Count < InnerPowerRankMinimumFieldCount)
        {
            throw new FormatException(
                $"{sourceFile} line {lineNumber} has {fields.Count} fields; expected at least {InnerPowerRankMinimumFieldCount}.");
        }

        if (fields[^1].Length != 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing / field.");
        }

        return new InnerPowerRankEntry(
            fields[0],
            ParseInt(fields[1], "inner power rank character id"),
            CopyRange(fields, start: 2, count: fields.Count - 3),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static IReadOnlyList<NewPlayerRewardRecord> ParseNewPlayerRewardsFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ParseNewPlayerRewards(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<NewPlayerRewardRecord> ParseNewPlayerRewards(
        string text,
        string sourceFile = "NewPlayerRewords.txt")
    {
        return ParseCountedRows(text, sourceFile, ParseNewPlayerRewardRow);
    }

    public static NewPlayerRewardRecord ParseNewPlayerRewardRow(string sourceFile, int lineNumber, string row)
    {
        var fields = ParseSlashFields(sourceFile, lineNumber, row, NewPlayerRewardFieldCount);

        return new NewPlayerRewardRecord(
            fields[0],
            fields[1],
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static IReadOnlyList<VoteRecord> ParseVotesFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ParseVotes(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<VoteRecord> ParseVotes(
        string text,
        string sourceFile = "Votes.txt")
    {
        return ParseCountedRows(text, sourceFile, ParseVoteRow);
    }

    public static VoteRecord ParseVoteRow(string sourceFile, int lineNumber, string row)
    {
        var fields = ParseSlashFields(sourceFile, lineNumber, row, VoteFieldCount);

        return new VoteRecord(
            ParseInt(fields[0], "vote character id"),
            fields[1],
            fields[2],
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static IReadOnlyList<CouplesPkRecord> ParseCouplesPkFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ParseCouplesPk(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<CouplesPkRecord> ParseCouplesPk(
        string text,
        string sourceFile = "CouplesPK.ini")
    {
        return ParseCountedRows(text, sourceFile, ParseCouplesPkRow);
    }

    public static CouplesPkRecord ParseCouplesPkRow(string sourceFile, int lineNumber, string row)
    {
        var fields = ParseSlashFields(sourceFile, lineNumber, row, CouplesPkFieldCount);

        return new CouplesPkRecord(
            fields[0],
            fields[1],
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static IReadOnlyList<string> SplitSlashRow(string row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.Split('/', StringSplitOptions.None);
    }

    private static IReadOnlyList<T> ParseCountedRows<T>(
        string text,
        string sourceFile,
        Func<string, int, string, T> parseRow)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var rows = new List<T>();
        int? expectedCount = null;
        using var reader = new StringReader(text);
        var lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.Trim().Length == 0)
            {
                continue;
            }

            if (expectedCount is null)
            {
                expectedCount = ParseCountHeader(sourceFile, lineNumber, line);
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
        var trimmed = line.Trim();
        if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must start with a Count header.");
        }

        var countText = trimmed[prefix.Length..].Trim();
        if (!int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has invalid Count value '{countText}'.");
        }

        if (count < 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has negative Count value.");
        }

        return count;
    }

    private static IReadOnlyList<string> ParseSlashFields(
        string sourceFile,
        int lineNumber,
        string row,
        int expectedFieldCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentNullException.ThrowIfNull(row);
        ValidateLineNumber(lineNumber);

        var fields = SplitSlashRow(row);
        if (fields.Count != expectedFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected {expectedFieldCount}.");
        }

        if (fields[^1].Length != 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing / field.");
        }

        return fields;
    }

    private static IReadOnlyList<string> CopyRange(IReadOnlyList<string> fields, int start, int count)
    {
        var values = new string[count];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = fields[start + index];
        }

        return values;
    }

    private static int ParseInt(string value, string name)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }

    private static void ValidateLineNumber(int lineNumber)
    {
        if (lineNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number must be positive.");
        }
    }
}

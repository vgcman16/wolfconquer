using System.Globalization;

namespace WolfConquer.Core.Data;

public static class LotteryPrizeParser
{
    private const int ShortFieldCount = 4;
    private const int SocketTailFieldCount = 4;

    public static IReadOnlyList<LotteryPrize> ParseFile(string path, bool requireCountMatch = false)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return Parse(File.ReadAllText(path), Path.GetFileName(path), requireCountMatch);
    }

    public static IReadOnlyList<LotteryPrize> Parse(
        string text,
        string sourceFile = "lottery.ini",
        bool requireCountMatch = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        int? expectedCount = null;
        var prizes = new List<LotteryPrize>();
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

            prizes.Add(ParseRow(sourceFile, lineNumber, line));
        }

        if (expectedCount is null)
        {
            throw new FormatException($"{sourceFile} is missing Count header.");
        }

        if (requireCountMatch && prizes.Count != expectedCount.Value)
        {
            throw new FormatException($"{sourceFile} declares Count={expectedCount.Value} but contains {prizes.Count} rows.");
        }

        return prizes;
    }

    public static LotteryPrize ParseRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentNullException.ThrowIfNull(row);
        ValidateLineNumber(lineNumber);

        var fields = SplitWhitespace(row).ToArray();
        if (fields.Length != ShortFieldCount && fields.Length < 7)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected {ShortFieldCount} or at least 7.");
        }

        var hasSocketTail = fields.Length >= 7;
        var itemTypeIndex = hasSocketTail ? fields.Length - SocketTailFieldCount : fields.Length - 1;
        var itemName = string.Join(' ', fields.Skip(2).Take(itemTypeIndex - 2));
        if (itemName.Length == 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has an empty lottery prize item name.");
        }

        var itemTypeRaw = fields[itemTypeIndex];
        return new LotteryPrize(
            ParseRequiredInt(fields[0], "lottery prize type"),
            ParseRequiredInt(fields[1], "lottery prize level"),
            itemName,
            itemTypeRaw,
            ParseOptionalInt(itemTypeRaw, "lottery prize item type id"),
            hasSocketTail ? ParseRequiredInt(fields[^3], "lottery prize socket one") : null,
            hasSocketTail ? ParseRequiredInt(fields[^2], "lottery prize socket two") : null,
            hasSocketTail ? ParseRequiredInt(fields[^1], "lottery prize plus level") : null,
            sourceFile,
            lineNumber,
            row,
            fields);
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
        if (!int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has invalid Count value '{countText}'.");
        }

        return count;
    }

    private static IReadOnlyList<string> SplitWhitespace(string row)
    {
        var fields = new List<string>();
        var index = 0;
        while (index < row.Length)
        {
            while (index < row.Length && char.IsWhiteSpace(row[index]))
            {
                index++;
            }

            var start = index;
            while (index < row.Length && !char.IsWhiteSpace(row[index]))
            {
                index++;
            }

            if (start < index)
            {
                fields.Add(row[start..index]);
            }
        }

        return fields;
    }

    private static int ParseRequiredInt(string value, string name)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }

    private static int? ParseOptionalInt(string value, string name)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static void ValidateLineNumber(int lineNumber)
    {
        if (lineNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number must be positive.");
        }
    }
}

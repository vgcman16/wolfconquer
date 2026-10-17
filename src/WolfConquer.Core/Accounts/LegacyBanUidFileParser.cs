using System.Globalization;

namespace WolfConquer.Core.Accounts;

public sealed record LegacyBanUidRecord(
    long AccountId,
    uint Hours,
    long StartBan,
    string Name,
    string Reason)
{
    public bool IsActive(DateTime now)
    {
        try
        {
            return DateTime.FromBinary(StartBan).AddHours(Hours) >= now;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

public static class LegacyBanUidFileParser
{
    public static IReadOnlyList<LegacyBanUidRecord> ParseFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ParseLines(File.ReadLines(path));
    }

    public static IReadOnlyList<LegacyBanUidRecord> ParseLines(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var materialized = lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        if (materialized.Length == 0)
        {
            return [];
        }

        var declaredCount = TryReadCount(materialized[0]);
        var records = new List<LegacyBanUidRecord>();
        var maxLineCount = declaredCount.HasValue
            ? Math.Min(declaredCount.Value, materialized.Length - 1)
            : materialized.Length;
        var startIndex = declaredCount.HasValue ? 1 : 0;

        for (var index = 0; index < maxLineCount; index++)
        {
            if (TryParseRecordLine(materialized[startIndex + index], out var record))
            {
                records.Add(record);
            }
        }

        return records;
    }

    public static IReadOnlySet<long> ParseActiveAccountIds(IEnumerable<string> lines, DateTime now)
    {
        return ParseLines(lines)
            .Where(record => record.IsActive(now))
            .Select(record => record.AccountId)
            .ToHashSet();
    }

    private static int? TryReadCount(string line)
    {
        var separator = line.IndexOf('=');
        if (separator <= 0 ||
            !string.Equals(line[..separator].Trim(), "Count", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return int.TryParse(line[(separator + 1)..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            ? Math.Max(0, count)
            : 0;
    }

    private static bool TryParseRecordLine(string line, out LegacyBanUidRecord record)
    {
        var parts = line.Split('/');
        if (parts.Length < 5 ||
            !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var accountId) ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
            !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var startBan))
        {
            record = new LegacyBanUidRecord(0, 0, 0, string.Empty, string.Empty);
            return false;
        }

        record = new LegacyBanUidRecord(accountId, hours, startBan, parts[3], parts[4]);
        return true;
    }
}

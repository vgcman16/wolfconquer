using System.Globalization;
using System.Text.RegularExpressions;

namespace WolfConquer.Core.Accounts;

public static class LegacyAccServerLoginLogParser
{
    private static readonly Regex LogLinePattern = new(
        @"^\s*UserName\[Acc\]\s*\|(?<username>[^|]*)\|\s*UIDAccount\s*\|(?<accountId>\d+)\s*\|\s*IPAccount\s*\|\s*(?<ip>[^|]*?)\s*\|\s*PasswordAccount\s*\[(?<password>[^\]]*)\]\s*\[The Clock\]\s*(?<clock>.*)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

    public static IReadOnlyList<LegacyAccServerLoginLogEntry> ParseFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ParseLines(File.ReadLines(path), path);
    }

    public static IReadOnlyList<LegacyAccServerLoginLogEntry> ParseLines(
        IEnumerable<string> lines,
        string sourcePath = "")
    {
        ArgumentNullException.ThrowIfNull(lines);

        var entries = new List<LegacyAccServerLoginLogEntry>();
        var lineNumber = 0;
        foreach (var line in lines)
        {
            lineNumber++;
            if (TryParseLine(line, out var entry))
            {
                entries.Add(entry with
                {
                    SourcePath = sourcePath,
                    LineNumber = lineNumber,
                });
            }
        }

        return entries;
    }

    public static bool TryParseLine(string line, out LegacyAccServerLoginLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(line);

        var match = LogLinePattern.Match(line);
        if (!match.Success ||
            !long.TryParse(match.Groups["accountId"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var accountId))
        {
            entry = new LegacyAccServerLoginLogEntry(string.Empty, 0, string.Empty, string.Empty, string.Empty);
            return false;
        }

        entry = new LegacyAccServerLoginLogEntry(
            match.Groups["username"].Value.Trim(),
            accountId,
            match.Groups["ip"].Value.Trim(),
            match.Groups["password"].Value,
            match.Groups["clock"].Value.Trim());
        return true;
    }
}

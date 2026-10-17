using System.Globalization;
using System.Text.RegularExpressions;

internal static partial class Program
{
    static int ParityLogSummary(string[] args)
    {
        ParityLogSummaryOptions options;
        try
        {
            options = ResolveParityLogSummaryOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        if (!File.Exists(options.Path) && !Directory.Exists(options.Path))
        {
            Console.Error.WriteLine($"Parity log path not found: {options.Path}");
            return 1;
        }

        try
        {
            var report = BuildParityLogSummary(options);
            PrintParityLogSummary(report);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Parity log summary failed: {ex.Message}");
            return 1;
        }
    }

    static ParityLogSummaryOptions ResolveParityLogSummaryOptions(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("Usage: WolfConquer.DataTool parity-log-summary <logPathOrDirectory> [--top N]");
        }

        var path = Path.GetFullPath(args[1]);
        var topCount = 10;
        for (var index = 2; index < args.Length; index++)
        {
            var option = args[index];
            if (string.Equals(option, "--top", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length)
                {
                    throw new ArgumentException("Missing value for --top.");
                }

                topCount = ParseParityLogSummaryTopCount(args[index]);
                continue;
            }

            const string topPrefix = "--top=";
            if (option.StartsWith(topPrefix, StringComparison.OrdinalIgnoreCase))
            {
                topCount = ParseParityLogSummaryTopCount(option[topPrefix.Length..]);
                continue;
            }

            throw new ArgumentException($"Unknown parity-log-summary option: {option}");
        }

        return new ParityLogSummaryOptions(path, topCount);
    }

    static int ParseParityLogSummaryTopCount(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var topCount) || topCount < 0)
        {
            throw new FormatException("--top requires a zero or positive integer.");
        }

        return topCount;
    }

    static ParityLogSummaryReport BuildParityLogSummary(ParityLogSummaryOptions options)
    {
        var categoryCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var prefixCounts = new Dictionary<string, long>(StringComparer.Ordinal);
        var scannedFiles = 0;
        var matchingLines = 0L;

        foreach (var file in EnumerateParityLogFiles(options.Path))
        {
            scannedFiles++;
            foreach (var line in EnumerateParityLogLines(file))
            {
                var categories = GetParityLogCategories(line);
                if (categories.Count == 0)
                {
                    continue;
                }

                matchingLines++;
                foreach (var category in categories)
                {
                    IncrementCount(categoryCounts, category);
                }

                IncrementCount(prefixCounts, CreateParityLogMessagePrefix(line));
            }
        }

        return new ParityLogSummaryReport(
            options.Path,
            options.TopCount,
            scannedFiles,
            matchingLines,
            categoryCounts,
            prefixCounts);
    }

    static IEnumerable<string> EnumerateParityLogLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    static IEnumerable<string> EnumerateParityLogFiles(string path)
    {
        if (File.Exists(path))
        {
            yield return path;
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(path, "*.log", SearchOption.AllDirectories)
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            yield return file;
        }
    }

    static IReadOnlyList<string> GetParityLogCategories(string line)
    {
        var categories = new List<string>();
        foreach (var marker in ParityLogMarkerDefinitions.All)
        {
            foreach (var alias in marker.Aliases)
            {
                if (line.Contains(alias, StringComparison.OrdinalIgnoreCase))
                {
                    categories.Add(marker.Category);
                    break;
                }
            }
        }

        AddStructuredParityLogCategory(line, "category", "category", categories);
        AddStructuredParityLogCategory(line, "gap", "gap", categories);
        AddStructuredParityLogCategory(line, "severity", "severity", categories);

        return categories;
    }

    static void AddStructuredParityLogCategory(
        string line,
        string key,
        string prefix,
        List<string> categories)
    {
        var match = Regex.Match(
            line,
            $@"\b{Regex.Escape(key)}=(?<value>[A-Za-z0-9_.:-]+)",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return;
        }

        var category = $"{prefix}:{match.Groups["value"].Value}";
        if (!categories.Contains(category, StringComparer.OrdinalIgnoreCase))
        {
            categories.Add(category);
        }
    }

    static string CreateParityLogMessagePrefix(string line)
    {
        var message = StripParityLogPreamble(line.Trim());
        message = Regex.Replace(message, @"\s+", " ");
        message = Regex.Replace(
            message,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            "#",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        message = Regex.Replace(
            message,
            @"\b0x[0-9a-f]+\b",
            "0x#",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        message = Regex.Replace(
            message,
            @"\b(player|account|session|connection|request|trace|span|pid|tid|elapsedMs|elapsed|tick|seq|sequence)=\S+",
            "$1=#",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        message = message.Trim();
        const int maxPrefixLength = 160;
        if (message.Length > maxPrefixLength)
        {
            message = message[..maxPrefixLength].TrimEnd() + "...";
        }

        return message.Length == 0 ? "<empty>" : message;
    }

    static string StripParityLogPreamble(string message)
    {
        while (true)
        {
            var stripped = Regex.Replace(message, @"^\s*\[[^\]]+\]\s*", "");
            if (!string.Equals(stripped, message, StringComparison.Ordinal))
            {
                message = stripped;
                continue;
            }

            stripped = Regex.Replace(
                message,
                @"^\s*\d{4}-\d{2}-\d{2}[T\s]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?\s*",
                "");
            if (!string.Equals(stripped, message, StringComparison.Ordinal))
            {
                message = stripped;
                continue;
            }

            stripped = Regex.Replace(
                message,
                @"^\s*(trace|debug|info|warn|warning|error|fatal|trc|dbg|inf|wrn|err)\s*[:|-]?\s*",
                "",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (string.Equals(stripped, message, StringComparison.Ordinal))
            {
                return message;
            }

            message = stripped;
        }
    }

    static void PrintParityLogSummary(ParityLogSummaryReport report)
    {
        Console.WriteLine("Parity log summary:");
        Console.WriteLine($"Path: {report.Path}");
        Console.WriteLine($"Scanned files: {report.ScannedFileCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Matching lines: {report.MatchingLineCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine("Categories:");

        var wroteCategory = false;
        foreach (var marker in ParityLogMarkerDefinitions.All)
        {
            if (!report.CategoryCounts.TryGetValue(marker.Category, out var count) || count <= 0)
            {
                continue;
            }

            Console.WriteLine($"  {marker.Category}: {count.ToString(CultureInfo.InvariantCulture)}");
            wroteCategory = true;
        }

        var standardCategories = ParityLogMarkerDefinitions.All
            .Select(static marker => marker.Category)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var category in report.CategoryCounts
                     .Where(pair => !standardCategories.Contains(pair.Key) && pair.Value > 0)
                     .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  {category.Key}: {category.Value.ToString(CultureInfo.InvariantCulture)}");
            wroteCategory = true;
        }

        if (!wroteCategory)
        {
            Console.WriteLine("  <none>");
        }

        Console.WriteLine("Top message prefixes:");
        var wrotePrefix = false;
        foreach (var prefix in report.PrefixCounts
                     .OrderByDescending(pair => pair.Value)
                     .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                     .Take(report.TopCount))
        {
            Console.WriteLine($"  {prefix.Value.ToString(CultureInfo.InvariantCulture)}: {prefix.Key}");
            wrotePrefix = true;
        }

        if (!wrotePrefix)
        {
            Console.WriteLine("  <none>");
        }
    }

    static void IncrementCount<TKey>(IDictionary<TKey, long> counts, TKey key)
        where TKey : notnull
    {
        counts.TryGetValue(key, out var count);
        counts[key] = count + 1;
    }
}

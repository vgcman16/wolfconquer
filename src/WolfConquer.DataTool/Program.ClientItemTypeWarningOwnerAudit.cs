using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

internal static partial class Program
{
    private const string ClientItemTypeWarningOwnerAuditUsage =
        "Usage: WolfConquer.DataTool client-itemtype-warning-owner-audit <clientDebugLogPath> <clientRoot> [--since-line N] [--since timestamp] [--top N]";

    static int ClientItemTypeWarningOwnerAudit(string[] args)
    {
        if (!TryParseClientItemTypeWarningOwnerAuditOptions(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(ClientItemTypeWarningOwnerAuditUsage);
            return 1;
        }

        ClientItemTypeWarningOwnerAuditReport report;
        try
        {
            report = ClientItemTypeWarningOwnerAuditor.Audit(options);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not audit ItemType warning owners: {ex.Message}");
            Console.Error.WriteLine(ClientItemTypeWarningOwnerAuditUsage);
            return 1;
        }

        Console.WriteLine("Client ItemType warning owner audit:");
        Console.WriteLine($"Client log: {report.ClientLogPath}");
        Console.WriteLine($"Client root: {report.ClientRoot}");
        Console.WriteLine($"Since: {FormatOptionalTimestamp(report.Since)}");
        Console.WriteLine($"Since line: {report.SinceLine?.ToString(CultureInfo.InvariantCulture) ?? "<none>"}");
        Console.WriteLine($"Warnings: total={report.TotalWarningCount} uniqueIds={report.UniqueWarningIdCount} reportedIds={report.Items.Count}");
        Console.WriteLine($"Text files: scanned={report.ScannedTextFiles} skipped={report.SkippedFiles}");

        if (report.Items.Count == 0)
        {
            Console.WriteLine("Warning ids: <none>");
            return 0;
        }

        Console.WriteLine("Warning ids:");
        foreach (var item in report.Items)
        {
            Console.WriteLine($"  id {item.Id} logCount={item.LogCount}");
            if (item.Owners.Count == 0)
            {
                Console.WriteLine("    <no text owner found>");
                continue;
            }

            foreach (var owner in item.Owners)
            {
                Console.WriteLine(
                    $"    {owner.RelativePath} hits={owner.HitCount} firstLines={string.Join(", ", owner.FirstLines)}");
            }
        }

        return 0;
    }

    private static bool TryParseClientItemTypeWarningOwnerAuditOptions(
        string[] args,
        out ClientItemTypeWarningOwnerAuditOptions options,
        out string error)
    {
        options = new ClientItemTypeWarningOwnerAuditOptions("", "", null, null, 50);
        error = "";

        if (args.Length < 3)
        {
            error = "Missing client debug log path or client root.";
            return false;
        }

        var clientLogPath = Path.GetFullPath(args[1]);
        var clientRoot = Path.GetFullPath(args[2]);
        DateTime? since = null;
        int? sinceLine = null;
        var top = 50;

        for (var index = 3; index < args.Length; index++)
        {
            var argument = args[index];
            if (TryReadOptionValue(args, ref index, "--since", argument, out var sinceValue))
            {
                if (!ClientPlaytestLogAuditor.TryParseTimestamp(sinceValue, null, out var parsedSince))
                {
                    error = $"Could not parse --since timestamp: {sinceValue}";
                    return false;
                }

                since = parsedSince;
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--since-line", argument, out var sinceLineValue))
            {
                if (!int.TryParse(sinceLineValue, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSinceLine) ||
                    parsedSinceLine < 1)
                {
                    error = $"Invalid --since-line value: {sinceLineValue}";
                    return false;
                }

                sinceLine = parsedSinceLine;
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--top", argument, out var topValue))
            {
                if (!int.TryParse(topValue, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedTop) ||
                    parsedTop < 1)
                {
                    error = $"Invalid --top value: {topValue}";
                    return false;
                }

                top = parsedTop;
                continue;
            }

            error = $"Unknown option: {argument}";
            return false;
        }

        options = new ClientItemTypeWarningOwnerAuditOptions(clientLogPath, clientRoot, since, sinceLine, top);
        return true;
    }
}

internal sealed record ClientItemTypeWarningOwnerAuditOptions(
    string ClientLogPath,
    string ClientRoot,
    DateTime? Since,
    int? SinceLine,
    int Top);

internal sealed record ClientItemTypeWarningOwnerAuditReport(
    string ClientLogPath,
    string ClientRoot,
    DateTime? Since,
    int? SinceLine,
    int TotalWarningCount,
    int UniqueWarningIdCount,
    int ScannedTextFiles,
    int SkippedFiles,
    IReadOnlyList<ClientItemTypeWarningOwnerItem> Items);

internal sealed record ClientItemTypeWarningOwnerItem(
    string Id,
    int LogCount,
    IReadOnlyList<ClientItemTypeWarningTextOwner> Owners);

internal sealed record ClientItemTypeWarningTextOwner(
    string RelativePath,
    int HitCount,
    IReadOnlyList<int> FirstLines);

internal static class ClientItemTypeWarningOwnerAuditor
{
    private const long MaxTextFileBytes = 5 * 1024 * 1024;
    private const int FirstLineLimit = 5;

    private static readonly Regex ItemTypeReadErrorPattern = new(
        @"\bWARNING:\s*Read Error in ItemType\.dat,\s*itemtypeID=(?<id>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ClientDateStampPattern = new(
        @"--\s*(?<stamp>[A-Za-z]{3}\s+[A-Za-z]{3}\s+\d{1,2}\s+\d{2}:\d{2}:\d{2}\s+\d{4})\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex IsoDateStampPattern = new(
        @"(?<stamp>\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TimeOnlyStampPattern = new(
        @"^\s*\[?(?<stamp>\d{2}:\d{2}:\d{2})(?:[.\s\]])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> IncludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ini",
        ".lua",
        ".txt",
        ".dat",
    };

    public static ClientItemTypeWarningOwnerAuditReport Audit(ClientItemTypeWarningOwnerAuditOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!File.Exists(options.ClientLogPath))
        {
            throw new FileNotFoundException($"Client debug log not found: {options.ClientLogPath}", options.ClientLogPath);
        }

        if (!Directory.Exists(options.ClientRoot))
        {
            throw new DirectoryNotFoundException($"Client root not found: {options.ClientRoot}");
        }

        var fullLogPath = Path.GetFullPath(options.ClientLogPath);
        var fullClientRoot = Path.GetFullPath(options.ClientRoot);
        var counters = ReadWarningCounters(fullLogPath, options.Since, options.SinceLine);
        var topIds = counters
            .OrderByDescending(static pair => pair.Value)
            .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
            .Take(options.Top)
            .Select(static pair => pair.Key)
            .ToArray();

        var ownerMap = topIds.ToDictionary(static id => id, static _ => new List<ClientItemTypeWarningTextOwner>(), StringComparer.Ordinal);
        var scannedTextFiles = 0;
        var skippedFiles = 0;

        if (topIds.Length > 0)
        {
            foreach (var path in EnumerateCandidateFiles(fullClientRoot))
            {
                if (!TryReadCandidateText(path, out var text))
                {
                    skippedFiles++;
                    continue;
                }

                scannedTextFiles++;
                var relativePath = Path.GetRelativePath(fullClientRoot, path)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/');
                foreach (var owner in FindOwners(relativePath, text, topIds))
                {
                    ownerMap[owner.Id].Add(owner.Owner);
                }
            }
        }

        var items = topIds
            .Select(id => new ClientItemTypeWarningOwnerItem(
                id,
                counters[id],
                ownerMap[id]
                    .OrderByDescending(static owner => owner.HitCount)
                    .ThenBy(static owner => owner.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();

        return new ClientItemTypeWarningOwnerAuditReport(
            fullLogPath,
            fullClientRoot,
            options.Since,
            options.SinceLine,
            counters.Values.Sum(),
            counters.Count,
            scannedTextFiles,
            skippedFiles,
            items);
    }

    private static SortedDictionary<string, int> ReadWarningCounters(string path, DateTime? since, int? sinceLine)
    {
        var counters = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var lineNumber = 0;
        DateTime? lastTimestamp = null;

        foreach (var line in ReadSharedLogLines(path))
        {
            lineNumber++;
            if (!ShouldIncludeLine(line, lineNumber, since, sinceLine, ref lastTimestamp))
            {
                continue;
            }

            var match = ItemTypeReadErrorPattern.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var id = match.Groups["id"].Value;
            counters[id] = counters.TryGetValue(id, out var count) ? count + 1 : 1;
        }

        return counters;
    }

    private static IEnumerable<string> EnumerateCandidateFiles(string clientRoot)
    {
        foreach (var subdirectory in new[] { "ini", "Script" })
        {
            var root = Path.Combine(clientRoot, subdirectory);
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (!IncludedExtensions.Contains(Path.GetExtension(path)) ||
                    string.Equals(Path.GetFileName(path), "ItemType.dat", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return path;
            }
        }
    }

    private static bool TryReadCandidateText(string path, out string text)
    {
        text = "";
        var info = new FileInfo(path);
        if (info.Length > MaxTextFileBytes)
        {
            return false;
        }

        var bytes = File.ReadAllBytes(path);
        if (bytes.Any(static value => value == 0))
        {
            return false;
        }

        foreach (var encoding in GetTextEncodings())
        {
            try
            {
                text = encoding.GetString(bytes);
                return true;
            }
            catch (DecoderFallbackException)
            {
            }
        }

        return false;
    }

    private static IEnumerable<(string Id, ClientItemTypeWarningTextOwner Owner)> FindOwners(
        string relativePath,
        string text,
        IReadOnlyList<string> ids)
    {
        var lineCounters = ids.ToDictionary(static id => id, static _ => new TextOwnerLineCounter(), StringComparer.Ordinal);
        var lineNumber = 0;

        foreach (var line in SplitLines(text))
        {
            lineNumber++;
            foreach (var id in ids)
            {
                if (ContainsWholeNumericToken(line, id))
                {
                    lineCounters[id].Record(lineNumber);
                }
            }
        }

        foreach (var pair in lineCounters.Where(static pair => pair.Value.HitCount > 0))
        {
            yield return (pair.Key, new ClientItemTypeWarningTextOwner(
                relativePath,
                pair.Value.HitCount,
                pair.Value.FirstLines));
        }
    }

    private static bool ContainsWholeNumericToken(string line, string id)
    {
        var index = line.IndexOf(id, StringComparison.Ordinal);
        while (index >= 0)
        {
            var beforeOk = index == 0 || !char.IsLetterOrDigit(line[index - 1]);
            var afterIndex = index + id.Length;
            var afterOk = afterIndex >= line.Length || !char.IsLetterOrDigit(line[afterIndex]);
            if (beforeOk && afterOk)
            {
                return true;
            }

            index = line.IndexOf(id, index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool ShouldIncludeLine(
        string line,
        int lineNumber,
        DateTime? since,
        int? sinceLine,
        ref DateTime? lastTimestamp)
    {
        if (TryExtractLineTimestamp(line, since, out var lineTimestamp))
        {
            lastTimestamp = lineTimestamp;
        }

        if (sinceLine is not null && lineNumber < sinceLine.Value)
        {
            return false;
        }

        if (since is null)
        {
            return true;
        }

        return lastTimestamp is not null && lastTimestamp.Value >= since.Value;
    }

    private static bool TryExtractLineTimestamp(string line, DateTime? since, out DateTime timestamp)
    {
        var match = ClientDateStampPattern.Match(line);
        if (match.Success && ClientPlaytestLogAuditor.TryParseTimestamp(match.Groups["stamp"].Value, since, out timestamp))
        {
            return true;
        }

        match = IsoDateStampPattern.Match(line);
        if (match.Success && ClientPlaytestLogAuditor.TryParseTimestamp(match.Groups["stamp"].Value, since, out timestamp))
        {
            return true;
        }

        match = TimeOnlyStampPattern.Match(line);
        if (match.Success && ClientPlaytestLogAuditor.TryParseTimestamp(match.Groups["stamp"].Value, since, out timestamp))
        {
            return true;
        }

        timestamp = default;
        return false;
    }

    private static IEnumerable<string> ReadSharedLogLines(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
    }

    private static IReadOnlyList<Encoding> GetTextEncodings()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        return
        [
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            Encoding.GetEncoding(
                936,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback),
        ];
    }

    private sealed class TextOwnerLineCounter
    {
        private readonly List<int> firstLines = [];

        public int HitCount { get; private set; }

        public IReadOnlyList<int> FirstLines => firstLines;

        public void Record(int lineNumber)
        {
            HitCount++;
            if (firstLines.Count == 0 || firstLines[^1] != lineNumber)
            {
                if (firstLines.Count < FirstLineLimit)
                {
                    firstLines.Add(lineNumber);
                }
            }
        }
    }
}

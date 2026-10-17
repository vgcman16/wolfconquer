using System.Globalization;
using System.Text.RegularExpressions;

internal static partial class Program
{
    private const int ClientPlaytestAuditPreviewLimit = 20;

    private const string ClientPlaytestAuditUsage =
        "Usage: WolfConquer.DataTool client-playtest-audit <clientDebugLogPath> [--since timestamp] [--since-line N] [--server-log path] [...] [--list-all] [--no-fail-on-catch]";

    static int ClientPlaytestAudit(string[] args)
    {
        if (!TryParseClientPlaytestAuditOptions(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(ClientPlaytestAuditUsage);
            return 1;
        }

        ClientPlaytestAuditReport report;
        try
        {
            report = ClientPlaytestLogAuditor.Audit(options);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not audit playtest logs: {ex.Message}");
            return 1;
        }

        Console.WriteLine("Client playtest audit:");
        Console.WriteLine($"Client log: {report.ClientLogPath}");
        Console.WriteLine($"Since: {FormatOptionalTimestamp(report.Since)}");
        Console.WriteLine($"Since line: {report.SinceLine?.ToString(CultureInfo.InvariantCulture) ?? "<none>"}");
        Console.WriteLine($"Fail on critical catch: {!report.NoFailOnCatch}");
        Console.WriteLine($"Client lines: scanned={report.Client.ScannedLines} included={report.Client.IncludedLines}");
        Console.WriteLine($"Client catch errors: {report.TotalCatchCount}");
        foreach (var catchSummary in report.Catches)
        {
            Console.WriteLine(
                $"  opcode {catchSummary.Opcode} count={catchSummary.Count} " +
                $"firstLine={catchSummary.FirstLine} lastLine={catchSummary.LastLine} " +
                $"critical={catchSummary.IsCritical}");
        }

        Console.WriteLine($"Critical client catches: {report.CriticalCatchCount} opcodes=2200,2176");
        Console.WriteLine($"Lua errors: {report.Client.LuaErrors}");
        Console.WriteLine($"Missing textures: {report.Client.MissingTextures}");
        if (options.ListAll)
        {
            PrintClientPlaytestAuditFullList("Missing texture ids", report.Client.MissingTextureIds);
        }

        Console.WriteLine($"ANI warnings: {report.Client.AniWarnings}");
        Console.WriteLine($"ItemType read errors: {report.Client.ItemTypeReadErrors}");
        Console.WriteLine(
            $"ItemType read error ids: unique={report.Client.ItemTypeReadErrorIds.Count} " +
            $"first{ClientPlaytestAuditPreviewLimit}={FormatPreviewList(report.Client.ItemTypeReadErrorIds)}");
        if (options.ListAll)
        {
            PrintClientPlaytestAuditFullList("ItemType read error ids", report.Client.ItemTypeReadErrorIds);
        }

        Console.WriteLine(
            $"Missing string keys: unique={report.Client.MissingStringKeys.Count} " +
            $"first{ClientPlaytestAuditPreviewLimit}={FormatPreviewList(report.Client.MissingStringKeys)}");
        if (options.ListAll)
        {
            PrintClientPlaytestAuditFullList("Missing string keys", report.Client.MissingStringKeys);
        }

        Console.WriteLine($"Client check/assert lines: {report.Client.ClientCheckLines}");
        Console.WriteLine(
            $"Client check/assert previews: unique={report.Client.ClientCheckLinePreviews.Count} " +
            $"first{ClientPlaytestAuditPreviewLimit}={FormatPreviewList(report.Client.ClientCheckLinePreviews)}");
        if (options.ListAll)
        {
            PrintClientPlaytestAuditFullList("Client check/assert previews", report.Client.ClientCheckLinePreviews);
        }

        Console.WriteLine($"Client symptom hint lines: {report.Client.ClientSymptomLines}");
        Console.WriteLine(
            $"Client symptom hint previews: unique={report.Client.ClientSymptomPreviews.Count} " +
            $"first{ClientPlaytestAuditPreviewLimit}={FormatPreviewList(report.Client.ClientSymptomPreviews)}");
        if (options.ListAll)
        {
            PrintClientPlaytestAuditFullList("Client symptom hint previews", report.Client.ClientSymptomPreviews);
        }

        Console.WriteLine($"Server logs: {report.ServerLogs.Count}");
        Console.WriteLine(
            $"Server unhandled/unsupported/error lines: " +
            $"unhandled={report.ServerUnhandledLines} " +
            $"unsupported={report.ServerUnsupportedLines} " +
            $"errors={report.ServerErrorLines}");

        foreach (var serverLog in report.ServerLogs)
        {
            Console.WriteLine(
                $"  {serverLog.Path}: scanned={serverLog.ScannedLines} included={serverLog.IncludedLines} " +
                $"unhandled={serverLog.UnhandledLines} unsupported={serverLog.UnsupportedLines} " +
                $"errors={serverLog.ErrorLines}");
        }

        if (report.HasFailingCriticalCatch)
        {
            Console.WriteLine("Result: critical client catches found.");
            return 2;
        }

        Console.WriteLine(report.CriticalCatchCount > 0
            ? "Result: critical client catches found, but --no-fail-on-catch is set."
            : "Result: no critical client catches found.");
        return 0;
    }

    private static bool TryParseClientPlaytestAuditOptions(
        string[] args,
        out ClientPlaytestAuditOptions options,
        out string error)
    {
        options = new ClientPlaytestAuditOptions("", null, null, [], false, false);
        error = "";

        if (args.Length < 2)
        {
            error = "Missing client debug log path.";
            return false;
        }

        var clientLogPath = Path.GetFullPath(args[1]);
        DateTime? since = null;
        int? sinceLine = null;
        var serverLogs = new List<string>();
        var listAll = false;
        var noFailOnCatch = false;

        for (var index = 2; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--list-all", StringComparison.OrdinalIgnoreCase))
            {
                listAll = true;
                continue;
            }

            if (string.Equals(argument, "--no-fail-on-catch", StringComparison.OrdinalIgnoreCase))
            {
                noFailOnCatch = true;
                continue;
            }

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

            if (TryReadOptionValue(args, ref index, "--server-log", argument, out var serverLogPath))
            {
                serverLogs.Add(Path.GetFullPath(serverLogPath));
                continue;
            }

            error = $"Unknown option: {argument}";
            return false;
        }

        options = new ClientPlaytestAuditOptions(clientLogPath, since, sinceLine, serverLogs, listAll, noFailOnCatch);
        return true;
    }

    private static bool TryReadOptionValue(
        string[] args,
        ref int index,
        string optionName,
        string argument,
        out string value)
    {
        value = "";
        if (argument.StartsWith(optionName + "=", StringComparison.OrdinalIgnoreCase))
        {
            value = argument[(optionName.Length + 1)..];
            return value.Length > 0;
        }

        if (!string.Equals(argument, optionName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (index + 1 >= args.Length)
        {
            return false;
        }

        index++;
        value = args[index];
        return value.Length > 0;
    }

    private static string FormatOptionalTimestamp(DateTime? timestamp)
    {
        return timestamp?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "<none>";
    }

    private static string FormatPreviewList(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return "<none>";
        }

        var preview = string.Join(", ", values.Take(ClientPlaytestAuditPreviewLimit));
        return values.Count > ClientPlaytestAuditPreviewLimit
            ? preview + ", ..."
            : preview;
    }

    private static void PrintClientPlaytestAuditFullList(string label, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        Console.WriteLine($"{label} all: unique={values.Count} values={string.Join(", ", values)}");
    }
}

internal sealed record ClientPlaytestAuditOptions(
    string ClientLogPath,
    DateTime? Since,
    int? SinceLine,
    IReadOnlyList<string> ServerLogPaths,
    bool ListAll,
    bool NoFailOnCatch);

internal sealed record ClientPlaytestAuditReport(
    string ClientLogPath,
    DateTime? Since,
    int? SinceLine,
    bool NoFailOnCatch,
    ClientPlaytestClientSummary Client,
    IReadOnlyList<ClientPlaytestCatchSummary> Catches,
    IReadOnlyList<ClientPlaytestServerSummary> ServerLogs)
{
    public int TotalCatchCount { get; } = Catches.Sum(static item => item.Count);

    public int CriticalCatchCount { get; } = Catches
        .Where(static item => item.IsCritical)
        .Sum(static item => item.Count);

    public bool HasFailingCriticalCatch => CriticalCatchCount > 0 && !NoFailOnCatch;

    public int ServerUnhandledLines { get; } = ServerLogs.Sum(static item => item.UnhandledLines);

    public int ServerUnsupportedLines { get; } = ServerLogs.Sum(static item => item.UnsupportedLines);

    public int ServerErrorLines { get; } = ServerLogs.Sum(static item => item.ErrorLines);
}

internal sealed record ClientPlaytestClientSummary(
    int ScannedLines,
    int IncludedLines,
    int LuaErrors,
    int MissingTextures,
    IReadOnlyList<string> MissingTextureIds,
    int AniWarnings,
    int ItemTypeReadErrors,
    IReadOnlyList<string> ItemTypeReadErrorIds,
    IReadOnlyList<string> MissingStringKeys,
    int ClientCheckLines,
    IReadOnlyList<string> ClientCheckLinePreviews,
    int ClientSymptomLines,
    IReadOnlyList<string> ClientSymptomPreviews);

internal sealed record ClientPlaytestCatchSummary(
    int Opcode,
    int Count,
    int FirstLine,
    int LastLine,
    bool IsCritical);

internal sealed record ClientPlaytestServerSummary(
    string Path,
    int ScannedLines,
    int IncludedLines,
    int UnhandledLines,
    int UnsupportedLines,
    int ErrorLines);

internal static class ClientPlaytestLogAuditor
{
    private static readonly Regex ClientCatchPattern = new(
        @"catch\s+error\s+in\s+process\s+msg\s*:?\s*(?<opcode>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex MissingTexturePattern = new(
        @"texture\s*\[\s*(?<id>\d+)\s*\]\s*not\s*found",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ItemTypeReadErrorPattern = new(
        @"\bWARNING:\s*Read Error in ItemType\.dat,\s*itemtypeID=(?<id>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex MissingStringKeyPattern = new(
        @"CStringManagerW::GetStr\(\)\s*Can't find string\[(?<key>[^\]]+)\]!",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ClientCheckLinePattern = new(
        @"\b(?:CHECKF?|IF_NOT)\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ClientSymptomHintPattern = new(
        @"black\s+screen|black\s+map|map\s+black|invisible|no\s+hp\s+bar|missing\s+hp|can't\s+load\s+map|load\s+map\s+failed|map\s+object|role\s+.*(?:not\s+found|failed)|monster\s+.*(?:not\s+found|failed)",
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

    private static readonly Regex ServerErrorPattern = new(
        @"\b(error|exception|failed|failure|fail)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static ClientPlaytestAuditReport Audit(ClientPlaytestAuditOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!File.Exists(options.ClientLogPath))
        {
            throw new FileNotFoundException($"Client debug log not found: {options.ClientLogPath}", options.ClientLogPath);
        }

        foreach (var serverLogPath in options.ServerLogPaths)
        {
            if (!File.Exists(serverLogPath))
            {
                throw new FileNotFoundException($"Server log not found: {serverLogPath}", serverLogPath);
            }
        }

        var client = ScanClientLog(options.ClientLogPath, options.Since, options.SinceLine, out var catches);
        var serverLogs = options.ServerLogPaths
            .Select(path => ScanServerLog(path, options.Since))
            .ToArray();

        return new ClientPlaytestAuditReport(
            options.ClientLogPath,
            options.Since,
            options.SinceLine,
            options.NoFailOnCatch,
            client,
            catches,
            serverLogs);
    }

    public static bool TryParseTimestamp(string value, DateTime? baseDate, out DateTime timestamp)
    {
        value = value.Trim();
        var clientFormats = new[]
        {
            "ddd MMM d HH:mm:ss yyyy",
            "ddd MMM dd HH:mm:ss yyyy",
        };
        if (DateTime.TryParseExact(
            value,
            clientFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out timestamp))
        {
            return true;
        }

        var isoFormats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
        };
        if (DateTime.TryParseExact(
            value,
            isoFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out timestamp))
        {
            return true;
        }

        if (baseDate is not null &&
            TimeSpan.TryParseExact(value, "c", CultureInfo.InvariantCulture, out var time))
        {
            timestamp = baseDate.Value.Date + time;
            return true;
        }

        timestamp = default;
        return false;
    }

    private static ClientPlaytestClientSummary ScanClientLog(
        string path,
        DateTime? since,
        int? sinceLine,
        out IReadOnlyList<ClientPlaytestCatchSummary> catches)
    {
        var scannedLines = 0;
        var includedLines = 0;
        var luaErrors = 0;
        var missingTextures = 0;
        var aniWarnings = 0;
        var itemTypeReadErrors = 0;
        var clientCheckLines = 0;
        var clientSymptomLines = 0;
        var catchCounters = new SortedDictionary<int, ClientPlaytestOpcodeCounter>();
        var missingTextureIds = new ClientPlaytestUniqueValues();
        var itemTypeReadErrorIds = new ClientPlaytestUniqueValues();
        var missingStringKeys = new ClientPlaytestUniqueValues();
        var clientCheckLinePreviews = new ClientPlaytestUniqueValues();
        var clientSymptomPreviews = new ClientPlaytestUniqueValues();
        DateTime? lastTimestamp = null;

        foreach (var line in ReadSharedLogLines(path))
        {
            scannedLines++;
            if (!ShouldIncludeLine(line, scannedLines, since, sinceLine, ref lastTimestamp))
            {
                continue;
            }

            includedLines++;
            var catchMatch = ClientCatchPattern.Match(line);
            if (catchMatch.Success &&
                int.TryParse(catchMatch.Groups["opcode"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var opcode))
            {
                if (!catchCounters.TryGetValue(opcode, out var counter))
                {
                    counter = new ClientPlaytestOpcodeCounter(opcode, scannedLines);
                    catchCounters.Add(opcode, counter);
                }

                counter.Record(scannedLines);
            }

            if (line.Contains("Lua error", StringComparison.OrdinalIgnoreCase))
            {
                luaErrors++;
            }

            var missingTextureMatch = MissingTexturePattern.Match(line);
            if (missingTextureMatch.Success)
            {
                missingTextures++;
                missingTextureIds.Add(missingTextureMatch.Groups["id"].Value);
            }

            if (line.Contains("CreateNewAni(", StringComparison.OrdinalIgnoreCase) ||
                (line.Contains("Ani Index", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("Not Found In", StringComparison.OrdinalIgnoreCase)))
            {
                aniWarnings++;
            }

            if (line.Contains("Read Error in ItemType.dat", StringComparison.OrdinalIgnoreCase))
            {
                itemTypeReadErrors++;
                var itemTypeReadErrorMatch = ItemTypeReadErrorPattern.Match(line);
                if (itemTypeReadErrorMatch.Success)
                {
                    itemTypeReadErrorIds.Add(itemTypeReadErrorMatch.Groups["id"].Value);
                }
            }

            var missingStringKeyMatch = MissingStringKeyPattern.Match(line);
            if (missingStringKeyMatch.Success)
            {
                missingStringKeys.Add(missingStringKeyMatch.Groups["key"].Value.Trim());
            }

            if (ClientCheckLinePattern.IsMatch(line))
            {
                clientCheckLines++;
                clientCheckLinePreviews.Add(FormatClientCheckLinePreview(line));
            }

            if (ClientSymptomHintPattern.IsMatch(line))
            {
                clientSymptomLines++;
                clientSymptomPreviews.Add(FormatClientSymptomPreview(line));
            }
        }

        catches = catchCounters.Values
            .Select(static counter => counter.ToSummary())
            .ToArray();

        return new ClientPlaytestClientSummary(
            scannedLines,
            includedLines,
            luaErrors,
            missingTextures,
            missingTextureIds.Values,
            aniWarnings,
            itemTypeReadErrors,
            itemTypeReadErrorIds.Values,
            missingStringKeys.Values,
            clientCheckLines,
            clientCheckLinePreviews.Values,
            clientSymptomLines,
            clientSymptomPreviews.Values);
    }

    private static string FormatClientSymptomPreview(string line)
    {
        var timestampMarker = line.IndexOf(" -- ", StringComparison.Ordinal);
        var preview = timestampMarker >= 0 ? line[..timestampMarker] : line;
        return TrimPreview(preview.Trim(' ', '\t', '\uFFFD'), 220);
    }

    private static string FormatClientCheckLinePreview(string line)
    {
        const int maxPreviewLength = 220;
        var trimmed = line.Trim();
        var checkMatch = ClientCheckLinePattern.Match(trimmed);
        if (!checkMatch.Success)
        {
            return TrimPreview(trimmed, maxPreviewLength);
        }

        var locationMarker = trimmed.IndexOf(" in ", checkMatch.Index, StringComparison.OrdinalIgnoreCase);
        if (locationMarker < 0)
        {
            return TrimPreview(trimmed[checkMatch.Index..].Trim(' ', '\t', '\uFFFD'), maxPreviewLength);
        }

        var expression = trimmed[checkMatch.Index..locationMarker].Trim(' ', '\t', '\uFFFD');
        var location = trimmed[(locationMarker + " in ".Length)..];
        var timestampMarker = location.IndexOf(" -- ", StringComparison.Ordinal);
        if (timestampMarker >= 0)
        {
            location = location[..timestampMarker];
        }

        location = location.Trim(' ', '\t', '\uFFFD');
        var commaIndex = location.LastIndexOf(',');
        var filePart = commaIndex >= 0 ? location[..commaIndex].Trim() : location;
        var linePart = commaIndex >= 0 ? location[commaIndex..].Trim() : "";
        var fileName = Path.GetFileName(filePart);
        var preview = string.IsNullOrWhiteSpace(fileName)
            ? expression
            : $"{expression} in {fileName}{linePart}";
        return TrimPreview(preview, maxPreviewLength);
    }

    private static string TrimPreview(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..(maxLength - 3)] + "...";
    }

    private static ClientPlaytestServerSummary ScanServerLog(
        string path,
        DateTime? since)
    {
        var scannedLines = 0;
        var includedLines = 0;
        var unhandledLines = 0;
        var unsupportedLines = 0;
        var errorLines = 0;
        DateTime? lastTimestamp = null;

        foreach (var line in ReadSharedLogLines(path))
        {
            scannedLines++;
            if (!ShouldIncludeLine(line, scannedLines, since, null, ref lastTimestamp))
            {
                continue;
            }

            if (IsServerLogMarkerLine(line))
            {
                continue;
            }

            includedLines++;
            if (line.Contains("unhandled", StringComparison.OrdinalIgnoreCase))
            {
                unhandledLines++;
            }

            if (line.Contains("unsupported", StringComparison.OrdinalIgnoreCase))
            {
                unsupportedLines++;
            }

            if (ServerErrorPattern.IsMatch(line))
            {
                errorLines++;
            }
        }

        return new ClientPlaytestServerSummary(
            path,
            scannedLines,
            includedLines,
            unhandledLines,
            unsupportedLines,
            errorLines);
    }

    private static bool IsServerLogMarkerLine(string line)
    {
        return line.TrimStart().StartsWith("Runtime error log file:", StringComparison.OrdinalIgnoreCase);
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
        if (match.Success && TryParseTimestamp(match.Groups["stamp"].Value, since, out timestamp))
        {
            return true;
        }

        match = IsoDateStampPattern.Match(line);
        if (match.Success && TryParseTimestamp(match.Groups["stamp"].Value, since, out timestamp))
        {
            return true;
        }

        match = TimeOnlyStampPattern.Match(line);
        if (match.Success && TryParseTimestamp(match.Groups["stamp"].Value, since, out timestamp))
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

    private sealed class ClientPlaytestOpcodeCounter
    {
        public ClientPlaytestOpcodeCounter(int opcode, int firstLine)
        {
            Opcode = opcode;
            FirstLine = firstLine;
        }

        public int Opcode { get; }

        public int Count { get; private set; }

        public int FirstLine { get; }

        public int LastLine { get; private set; }

        public void Record(int lineNumber)
        {
            Count++;
            LastLine = lineNumber;
        }

        public ClientPlaytestCatchSummary ToSummary()
        {
            return new ClientPlaytestCatchSummary(
                Opcode,
                Count,
                FirstLine,
                LastLine,
                Opcode is 2200 or 2176);
        }
    }

    private sealed class ClientPlaytestUniqueValues
    {
        private readonly HashSet<string> seen = new(StringComparer.Ordinal);
        private readonly List<string> values = [];

        public IReadOnlyList<string> Values => values;

        public void Add(string value)
        {
            if (value.Length > 0 && seen.Add(value))
            {
                values.Add(value);
            }
        }
    }
}

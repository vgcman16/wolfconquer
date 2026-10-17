using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

internal static partial class Program
{
    private const string ClientLoginCacheAuditUsage =
        "Usage: WolfConquer.DataTool client-login-cache-audit <clientRoot>";

    private static readonly string[] ClientLoginCacheBinaryRelativePaths =
    [
        "regkey.dat",
        "Env_DX8/regkey.dat",
        "Env_DX9/regkey.dat",
    ];

    private const int ClientLoginCacheMaxAccEuiTmpCandidates = 24;
    private const int ClientLoginCacheBlobInspectionByteLimit = 8192;
    private const int ClientLoginCacheHeaderSignatureByteCount = 16;
    private const int ClientLoginCacheRepeatedBlock4Size = 4;
    private const int ClientLoginCacheRepeatedBlock8Size = 8;
    private const int ClientLoginCacheMinimumPrintableTokenLength = 3;

    private static readonly string[] ClientLoginCacheIniRelativePaths =
    [
        "ini/GameSetUp.ini",
        "ini/MsgBoxEx.ini",
        "ini/LoginServerAccountCfg.ini",
    ];

    private static readonly string[] ClientLoginCacheSensitiveKeyTokens =
    [
        "account",
        "pass",
        "pwd",
        "token",
        "secret",
        "key",
        "user",
    ];

    private static readonly string[] ClientLoginCacheRelevantNameTokens =
    [
        "account",
        "address",
        "character",
        "gate",
        "host",
        "ip",
        "last",
        "login",
        "player",
        "port",
        "profile",
        "remember",
        "role",
        "save",
        "select",
        "server",
        "user",
        "world",
    ];

    private static readonly string[] ClientLoginCacheVisibleBlobLabelTokens =
    [
        "gate",
        "host",
        "login",
        "profile",
        "server",
        "world",
    ];

    private static readonly Regex ClientLoginCacheDebugMissingFileRegex = new(
        @"Can't\s+Load\s+File",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ClientLoginCacheDebugLuaErrorRegex = new(
        @"\bLua\s+error\b|ToStringEx|MicroSpotData",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ClientLoginCacheDebugTimeRegex = new(
        @"(?<time>\d{1,2}:\d{2}:\d{2})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    static int ClientLoginCacheAudit(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine(args.Length < 2 ? "Missing client root path." : "Too many positional arguments.");
            Console.Error.WriteLine(ClientLoginCacheAuditUsage);
            return 1;
        }

        var clientRoot = Path.GetFullPath(args[1]);
        if (!Directory.Exists(clientRoot))
        {
            Console.Error.WriteLine($"Client root not found: {clientRoot}");
            Console.Error.WriteLine(ClientLoginCacheAuditUsage);
            return 1;
        }

        ClientLoginCacheAuditReport report;
        try
        {
            report = ClientLoginCacheAuditor.Audit(clientRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not audit client login cache: {ex.Message}");
            return 1;
        }

        Console.WriteLine(ClientLoginCacheAuditor.FormatReport(report));
        return 0;
    }

    private sealed record ClientLoginCacheAuditReport(
        string ClientRoot,
        IReadOnlyList<ClientLoginCacheFileSummary> BinaryCandidates,
        int AccEuiTmpCandidateCount,
        int AccEuiTmpOmittedCount,
        IReadOnlyList<ClientLoginCacheIniSummary> IniCandidates,
        IReadOnlyList<ClientLoginCacheSetupSummary> SetupSummaries,
        ClientLoginCacheDebugSummary? NewestDebugLog);

    private sealed record ClientLoginCacheFileSummary(
        string RelativePath,
        bool Exists,
        long Length,
        DateTime LastWriteTimeUtc,
        string Sha256Prefix,
        ClientLoginCacheBlobSummary? BlobSummary);

    private sealed record ClientLoginCacheBlobSummary(
        string DecodeVerdict,
        int InspectedBytes,
        bool InspectionTruncated,
        string HeaderSignatureHash,
        int LeadingRepeated4ByteBlocks,
        int LeadingRepeated8ByteBlocks,
        int PrintableAsciiBytes,
        int PrintableAsciiTokenCount,
        int PrintableUtf16LeCharacters,
        int PrintableUtf16LeTokenCount,
        IReadOnlyList<string> AsciiLabels,
        IReadOnlyList<string> Utf16LeLabels);

    private sealed record ClientLoginCacheIniSummary(
        string RelativePath,
        bool Exists,
        long Length,
        DateTime LastWriteTimeUtc,
        IReadOnlyList<ClientLoginCacheIniSectionSummary> Sections);

    private sealed record ClientLoginCacheIniSectionSummary(
        string Name,
        IReadOnlyList<string> Keys);

    private sealed record ClientLoginCacheSetupSummary(
        string Server,
        string Profile,
        string RelativePath,
        long Length,
        DateTime LastWriteTimeUtc,
        IReadOnlyList<ClientLoginCacheIniSectionSummary> Sections);

    private sealed record ClientLoginCacheDebugSummary(
        string RelativePath,
        long Length,
        DateTime LastWriteTimeUtc,
        int LineCount,
        int WarningCount,
        string FirstWarningTime,
        string LastWarningTime,
        IReadOnlyDictionary<string, int> Counts);

    private static class ClientLoginCacheAuditor
    {
        public static ClientLoginCacheAuditReport Audit(string clientRoot)
        {
            var accEuiTmpCandidates = FindAccEuiTmpCandidates(clientRoot, out var accEuiTmpOmittedCount);
            var binaryCandidates = ClientLoginCacheBinaryRelativePaths
                .Concat(accEuiTmpCandidates)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(relativePath => AuditBinaryCandidate(clientRoot, relativePath))
                .ToList();
            var iniCandidates = ClientLoginCacheIniRelativePaths
                .Select(relativePath => AuditIniCandidate(clientRoot, relativePath))
                .ToList();
            var setupSummaries = AuditSetupSummaries(clientRoot);
            var newestDebugLog = AuditNewestDebugLog(clientRoot);

            return new ClientLoginCacheAuditReport(
                clientRoot,
                binaryCandidates,
                accEuiTmpCandidates.Count,
                accEuiTmpOmittedCount,
                iniCandidates,
                setupSummaries,
                newestDebugLog);
        }

        public static string FormatReport(ClientLoginCacheAuditReport report)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Client login cache audit:");
            builder.AppendLine($"Client root: {report.ClientRoot}");

            var presentBinaryCount = report.BinaryCandidates.Count(candidate => candidate.Exists);
            builder.AppendLine(
                $"Binary candidates: present={presentBinaryCount} missing={report.BinaryCandidates.Count - presentBinaryCount} " +
                $"accEuiTmp={report.AccEuiTmpCandidateCount} accEuiTmpOmitted={report.AccEuiTmpOmittedCount}");
            foreach (var candidate in report.BinaryCandidates)
            {
                builder.Append("binary ");
                AppendFileSummary(builder, candidate);
            }

            var presentIniCount = report.IniCandidates.Count(candidate => candidate.Exists);
            builder.AppendLine(
                $"INI candidates: present={presentIniCount} missing={report.IniCandidates.Count - presentIniCount}");
            foreach (var candidate in report.IniCandidates)
            {
                builder.Append($"ini {candidate.RelativePath} ");
                if (!candidate.Exists)
                {
                    builder.AppendLine("missing");
                    continue;
                }

                builder.AppendLine(
                    $"exists length={candidate.Length} mtimeUtc={FormatUtc(candidate.LastWriteTimeUtc)} sections={FormatSections(candidate.Sections)}");
            }

            var setupCount = report.SetupSummaries.Count;
            builder.AppendLine($"Setup.ini summaries: {setupCount}/{setupCount}");
            foreach (var setup in report.SetupSummaries.Take(12))
            {
                builder.AppendLine(
                    $"setup server={setup.Server} profile={setup.Profile} {setup.RelativePath} exists length={setup.Length} " +
                    $"mtimeUtc={FormatUtc(setup.LastWriteTimeUtc)} sections={FormatSections(setup.Sections)}");
            }

            if (report.SetupSummaries.Count > 12)
            {
                builder.AppendLine($"setup ... {report.SetupSummaries.Count - 12} more omitted");
            }

            builder.AppendLine("Newest debug log:");
            if (report.NewestDebugLog is null)
            {
                builder.AppendLine("debug-log <none>");
            }
            else
            {
                var debug = report.NewestDebugLog;
                builder.AppendLine(
                    $"debug-log {debug.RelativePath} length={debug.Length} mtimeUtc={FormatUtc(debug.LastWriteTimeUtc)} " +
                    $"lines={debug.LineCount} warnings={debug.WarningCount} firstWarning={debug.FirstWarningTime} " +
                    $"lastWarning={debug.LastWarningTime} counts={FormatCounts(debug.Counts)}");
            }

            return builder.ToString().TrimEnd();
        }

        private static void AppendFileSummary(StringBuilder builder, ClientLoginCacheFileSummary candidate)
        {
            if (!candidate.Exists)
            {
                builder.AppendLine($"{candidate.RelativePath} missing");
                return;
            }

            builder.AppendLine(
                $"{candidate.RelativePath} exists length={candidate.Length} mtimeUtc={FormatUtc(candidate.LastWriteTimeUtc)} sha256={candidate.Sha256Prefix}");
            if (candidate.BlobSummary is null)
            {
                return;
            }

            var summary = candidate.BlobSummary;
            builder.AppendLine(
                $"blob {candidate.RelativePath} Decode: {summary.DecodeVerdict} inspectedBytes={summary.InspectedBytes} " +
                $"truncated={FormatBool(summary.InspectionTruncated)} header16Sha256={summary.HeaderSignatureHash} " +
                $"leadRepeat4={summary.LeadingRepeated4ByteBlocks} leadRepeat8={summary.LeadingRepeated8ByteBlocks} " +
                $"asciiPrintableBytes={summary.PrintableAsciiBytes} asciiTokens={summary.PrintableAsciiTokenCount} " +
                $"utf16lePrintableChars={summary.PrintableUtf16LeCharacters} utf16leTokens={summary.PrintableUtf16LeTokenCount} " +
                $"asciiLabels={FormatLabels(summary.AsciiLabels)} utf16leLabels={FormatLabels(summary.Utf16LeLabels)} " +
                "values=redacted");
        }

        private static ClientLoginCacheFileSummary AuditBinaryCandidate(string clientRoot, string relativePath)
        {
            var path = ResolveClientRelativePath(clientRoot, relativePath);
            if (!File.Exists(path))
            {
                return new ClientLoginCacheFileSummary(relativePath, false, 0, DateTime.MinValue, "", null);
            }

            var info = new FileInfo(path);
            return new ClientLoginCacheFileSummary(
                relativePath,
                true,
                info.Length,
                info.LastWriteTimeUtc,
                ComputeSha256Prefix(path),
                InspectBlob(path));
        }

        private static IReadOnlyList<string> FindAccEuiTmpCandidates(string clientRoot, out int omittedCount)
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                MatchCasing = MatchCasing.CaseInsensitive,
            };

            var candidates = Directory.EnumerateFiles(clientRoot, "*accEui.tmp", options)
                .Select(path => NormalizeRelativePath(Path.GetRelativePath(clientRoot, path)))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            omittedCount = Math.Max(0, candidates.Count - ClientLoginCacheMaxAccEuiTmpCandidates);
            return candidates.Take(ClientLoginCacheMaxAccEuiTmpCandidates).ToList();
        }

        private static ClientLoginCacheBlobSummary InspectBlob(string path)
        {
            var sample = ReadBlobInspectionSample(path, out var truncated);
            var asciiText = Encoding.ASCII.GetString(sample);
            var utf16LeText = Encoding.Unicode.GetString(sample[..(sample.Length - (sample.Length % 2))]);

            return new ClientLoginCacheBlobSummary(
                "unsupported/opaque",
                sample.Length,
                truncated,
                ComputeByteHashPrefix(sample.AsSpan(0, Math.Min(sample.Length, ClientLoginCacheHeaderSignatureByteCount))),
                CountLeadingRepeatedBlocks(sample, ClientLoginCacheRepeatedBlock4Size),
                CountLeadingRepeatedBlocks(sample, ClientLoginCacheRepeatedBlock8Size),
                CountPrintableAsciiBytes(sample),
                CountPrintableAsciiTokens(sample),
                CountPrintableUtf16LeCharacters(sample),
                CountPrintableUtf16LeTokens(sample),
                FindVisibleBlobLabels(asciiText),
                FindVisibleBlobLabels(utf16LeText));
        }

        private static byte[] ReadBlobInspectionSample(string path, out bool truncated)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var limit = (int)Math.Min(stream.Length, ClientLoginCacheBlobInspectionByteLimit);
            var buffer = new byte[limit];
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = stream.Read(buffer, offset, buffer.Length - offset);
                if (read == 0)
                {
                    break;
                }

                offset += read;
            }

            truncated = stream.Length > ClientLoginCacheBlobInspectionByteLimit;
            return offset == buffer.Length ? buffer : buffer[..offset];
        }

        private static int CountLeadingRepeatedBlocks(byte[] bytes, int blockSize)
        {
            if (bytes.Length < blockSize)
            {
                return 0;
            }

            var firstBlock = bytes.AsSpan(0, blockSize);
            var count = 0;
            for (var offset = 0; offset + blockSize <= bytes.Length; offset += blockSize)
            {
                if (!bytes.AsSpan(offset, blockSize).SequenceEqual(firstBlock))
                {
                    break;
                }

                count++;
            }

            return count;
        }

        private static int CountPrintableAsciiBytes(byte[] bytes) =>
            bytes.Count(IsPrintableAsciiByte);

        private static int CountPrintableAsciiTokens(byte[] bytes)
        {
            var count = 0;
            var currentLength = 0;
            foreach (var value in bytes)
            {
                if (IsPrintableAsciiByte(value))
                {
                    currentLength++;
                    continue;
                }

                if (currentLength >= ClientLoginCacheMinimumPrintableTokenLength)
                {
                    count++;
                }

                currentLength = 0;
            }

            if (currentLength >= ClientLoginCacheMinimumPrintableTokenLength)
            {
                count++;
            }

            return count;
        }

        private static int CountPrintableUtf16LeCharacters(byte[] bytes)
        {
            var count = 0;
            for (var offset = 0; offset + 1 < bytes.Length; offset += 2)
            {
                if (IsPrintableUtf16LeCharacter(bytes, offset))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountPrintableUtf16LeTokens(byte[] bytes)
        {
            var count = 0;
            var currentLength = 0;
            for (var offset = 0; offset + 1 < bytes.Length; offset += 2)
            {
                if (IsPrintableUtf16LeCharacter(bytes, offset))
                {
                    currentLength++;
                    continue;
                }

                if (currentLength >= ClientLoginCacheMinimumPrintableTokenLength)
                {
                    count++;
                }

                currentLength = 0;
            }

            if (currentLength >= ClientLoginCacheMinimumPrintableTokenLength)
            {
                count++;
            }

            return count;
        }

        private static bool IsPrintableAsciiByte(byte value) =>
            value is >= 0x20 and <= 0x7e;

        private static bool IsPrintableUtf16LeCharacter(byte[] bytes, int offset)
        {
            var value = bytes[offset] | (bytes[offset + 1] << 8);
            return value is >= 0x20 and <= 0x7e;
        }

        private static IReadOnlyList<string> FindVisibleBlobLabels(string text) =>
            ClientLoginCacheVisibleBlobLabelTokens
                .Where(label => text.Contains(label, StringComparison.OrdinalIgnoreCase))
                .OrderBy(label => label, StringComparer.Ordinal)
                .ToList();

        private static ClientLoginCacheIniSummary AuditIniCandidate(string clientRoot, string relativePath)
        {
            var path = ResolveClientRelativePath(clientRoot, relativePath);
            if (!File.Exists(path))
            {
                return new ClientLoginCacheIniSummary(relativePath, false, 0, DateTime.MinValue, []);
            }

            var info = new FileInfo(path);
            return new ClientLoginCacheIniSummary(
                relativePath,
                true,
                info.Length,
                info.LastWriteTimeUtc,
                ReadIniSectionSummaries(path));
        }

        private static IReadOnlyList<ClientLoginCacheSetupSummary> AuditSetupSummaries(string clientRoot)
        {
            var logRoot = Path.Combine(clientRoot, "Log");
            if (!Directory.Exists(logRoot))
            {
                return [];
            }

            return Directory.EnumerateFiles(logRoot, "Setup.ini", SearchOption.AllDirectories)
                .Select(path => CreateSetupSummary(clientRoot, logRoot, path))
                .OrderByDescending(summary => summary.LastWriteTimeUtc)
                .ThenBy(summary => summary.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static ClientLoginCacheSetupSummary CreateSetupSummary(string clientRoot, string logRoot, string path)
        {
            var info = new FileInfo(path);
            var relativeToLog = Path.GetRelativePath(logRoot, path);
            var parts = relativeToLog.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var server = parts.Length > 0 ? parts[0] : "<unknown>";
            var profile = parts.Length > 1 ? parts[1] : "<unknown>";

            return new ClientLoginCacheSetupSummary(
                server,
                profile,
                NormalizeRelativePath(Path.GetRelativePath(clientRoot, path)),
                info.Length,
                info.LastWriteTimeUtc,
                ReadIniSectionSummaries(path));
        }

        private static ClientLoginCacheDebugSummary? AuditNewestDebugLog(string clientRoot)
        {
            var debugRoot = Path.Combine(clientRoot, "debug");
            if (!Directory.Exists(debugRoot))
            {
                return null;
            }

            var newest = Directory.EnumerateFiles(debugRoot, "*.log", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .Where(info => info.Exists)
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .FirstOrDefault();
            if (newest is null)
            {
                return null;
            }

            var lineCount = 0;
            var warningCount = 0;
            string? firstWarning = null;
            string? lastWarning = null;
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);

            foreach (var line in File.ReadLines(newest.FullName))
            {
                lineCount++;
                var kind = GetDebugWarningKind(line);
                if (kind is null)
                {
                    continue;
                }

                warningCount++;
                counts[kind] = counts.TryGetValue(kind, out var current) ? current + 1 : 1;
                var time = TryGetDebugLineTime(line) ?? $"line-{lineCount.ToString(CultureInfo.InvariantCulture)}";
                firstWarning ??= time;
                lastWarning = time;
            }

            return new ClientLoginCacheDebugSummary(
                NormalizeRelativePath(Path.GetRelativePath(clientRoot, newest.FullName)),
                newest.Length,
                newest.LastWriteTimeUtc,
                lineCount,
                warningCount,
                firstWarning ?? "<none>",
                lastWarning ?? "<none>",
                counts);
        }

        private static IReadOnlyList<ClientLoginCacheIniSectionSummary> ReadIniSectionSummaries(string path)
        {
            var sections = new List<ClientLoginCacheIniSectionSummary>();
            string? currentSection = null;
            var currentKeys = new List<string>();

            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                {
                    continue;
                }

                if (line.StartsWith('[') && line.EndsWith(']') && line.Length > 2)
                {
                    FlushSection();
                    currentSection = line[1..^1].Trim();
                    continue;
                }

                var equalsIndex = line.IndexOf('=');
                if (equalsIndex <= 0)
                {
                    continue;
                }

                currentSection ??= "<root>";
                var key = line[..equalsIndex].Trim();
                if (IsNoisyPreferenceKey(key))
                {
                    continue;
                }

                if (!IsRelevantName(currentSection) &&
                    !IsRelevantName(key) &&
                    !IsSensitiveKey(key))
                {
                    continue;
                }

                currentKeys.Add(FormatKeyName(key));
            }

            FlushSection();
            return sections;

            void FlushSection()
            {
                if (currentSection is null)
                {
                    return;
                }

                if (currentKeys.Count == 0)
                {
                    currentSection = null;
                    return;
                }

                sections.Add(new ClientLoginCacheIniSectionSummary(currentSection, currentKeys.ToList()));
                currentKeys.Clear();
            }
        }

        private static string FormatKeyName(string key) =>
            IsSensitiveKey(key)
                ? $"{key}[value-redacted]"
                : key;

        private static bool IsRelevantName(string value) =>
            ClientLoginCacheRelevantNameTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

        private static bool IsSensitiveKey(string key) =>
            ClientLoginCacheSensitiveKeyTokens.Any(token => key.Contains(token, StringComparison.OrdinalIgnoreCase));

        private static bool IsNoisyPreferenceKey(string key) =>
            key.StartsWith("HotKey", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("Skill", StringComparison.OrdinalIgnoreCase);

        private static string? GetDebugWarningKind(string line)
        {
            if (ClientLoginCacheDebugMissingFileRegex.IsMatch(line))
            {
                return "MissingFile";
            }

            if (ClientLoginCacheDebugLuaErrorRegex.IsMatch(line))
            {
                return "LuaError";
            }

            return null;
        }

        private static string? TryGetDebugLineTime(string line)
        {
            var match = ClientLoginCacheDebugTimeRegex.Match(line);
            return match.Success ? match.Groups["time"].Value : null;
        }

        private static string FormatSections(IReadOnlyList<ClientLoginCacheIniSectionSummary> sections) =>
            sections.Count == 0
                ? "<none>"
                : string.Join(
                    ";",
                    sections.Select(section => $"{section.Name}({string.Join(",", section.Keys)})"));

        private static string FormatCounts(IReadOnlyDictionary<string, int> counts) =>
            counts.Count == 0
                ? "<none>"
                : string.Join(",", counts.Select(pair => $"{pair.Key}:{pair.Value.ToString(CultureInfo.InvariantCulture)}"));

        private static string FormatLabels(IReadOnlyList<string> labels) =>
            labels.Count == 0
                ? "<none>"
                : string.Join(",", labels);

        private static string FormatBool(bool value) =>
            value ? "true" : "false";

        private static string ComputeSha256Prefix(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant()[..12];
        }

        private static string ComputeByteHashPrefix(ReadOnlySpan<byte> bytes) =>
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()[..12];

        private static string ResolveClientRelativePath(string clientRoot, string relativePath) =>
            Path.Combine(clientRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        private static string NormalizeRelativePath(string relativePath) =>
            relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

        private static string FormatUtc(DateTime value) =>
            value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}

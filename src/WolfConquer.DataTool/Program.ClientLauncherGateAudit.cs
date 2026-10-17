using System.Globalization;
using System.Text;

internal static partial class Program
{
    private const int ClientLauncherGateAuditDefaultTop = 20;
    private const int ClientLauncherGateAuditMaxLogs = 5;
    private const int ClientLauncherGateAuditMaxPreviewLength = 240;
    private static readonly string ClientLauncherGateOldUpdaterUrl =
        Environment.GetEnvironmentVariable("WOLFCONQUER_CLIENT_LEGACY_UPDATER_URL")
        ?? "https://example.invalid/legacy-updater/";
    private const string ClientLauncherGateNewUpdaterUrl = "http://127.0.0.1/ndlogin/zfplay/";
    private static readonly string[] ClientLauncherGateUpdaterFileNames = ["Update.exe", "._cache_Update.exe"];

    private const string ClientLauncherGateAuditUsage =
        "Usage: WolfConquer.DataTool client-launcher-gate-audit <clientRoot> [--top N]";

    static int ClientLauncherGateAudit(string[] args)
    {
        if (!TryParseClientLauncherGateAuditOptions(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(ClientLauncherGateAuditUsage);
            return 1;
        }

        ClientLauncherGateAuditReport report;
        try
        {
            report = CreateClientLauncherGateAuditReport(options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not audit client launcher gate: {ex.Message}");
            return 1;
        }

        PrintClientLauncherGateAuditReport(report, options);
        return 0;
    }

    private static bool TryParseClientLauncherGateAuditOptions(
        string[] args,
        out ClientLauncherGateAuditOptions options,
        out string error)
    {
        options = new ClientLauncherGateAuditOptions("", ClientLauncherGateAuditDefaultTop);
        error = "";

        if (args.Length < 2)
        {
            error = "Missing client root.";
            return false;
        }

        string clientRoot;
        try
        {
            clientRoot = Path.GetFullPath(args[1]);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"Invalid client root path: {ex.Message}";
            return false;
        }

        if (!Directory.Exists(clientRoot))
        {
            error = $"Client root not found: {clientRoot}";
            return false;
        }

        var topCount = ClientLauncherGateAuditDefaultTop;
        for (var index = 2; index < args.Length; index++)
        {
            var argument = args[index];
            if (TryReadOptionValue(args, ref index, "--top", argument, out var topValue))
            {
                if (!int.TryParse(topValue, NumberStyles.None, CultureInfo.InvariantCulture, out topCount) ||
                    topCount <= 0)
                {
                    error = "--top requires a positive integer.";
                    return false;
                }

                continue;
            }

            error = $"Unknown client-launcher-gate-audit option: {argument}";
            return false;
        }

        options = new ClientLauncherGateAuditOptions(clientRoot, topCount);
        return true;
    }

    private static ClientLauncherGateAuditReport CreateClientLauncherGateAuditReport(
        ClientLauncherGateAuditOptions options)
    {
        var configPath = Path.Combine(options.ClientRoot, "AutoPatch", "config.ini");
        var configReport = ReadClientLauncherGateConfig(configPath);
        var gameInfoReport = ReadClientLauncherGateGameInfo(Path.Combine(options.ClientRoot, "GameInfo.ini"));
        var iniGameInfoReport = ReadClientLauncherGateGameInfo(Path.Combine(options.ClientRoot, "ini", "GameInfo.ini"));
        var versionReport = ReadClientLauncherGateVersion(Path.Combine(options.ClientRoot, "version.dat"));
        var updaterPatchReports = ReadClientLauncherGateUpdaterPatchReports(options.ClientRoot);
        var logReport = ReadClientLauncherGateLogs(options.ClientRoot, options.TopCount);

        return new ClientLauncherGateAuditReport(
            options.ClientRoot,
            configReport,
            gameInfoReport,
            iniGameInfoReport,
            versionReport,
            updaterPatchReports,
            logReport.Logs,
            logReport.Md5Paths
                .OrderByDescending(path => path.LastSequence)
                .ThenBy(path => path.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            logReport.Hints);
    }

    private static ClientLauncherGateConfigReport ReadClientLauncherGateConfig(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return new ClientLauncherGateConfigReport(configPath, false, [], [], []);
        }

        var mainFields = new List<ClientLauncherGateConfigField>();
        var processFields = new List<ClientLauncherGateConfigField>();
        var patchFields = new List<ClientLauncherGateConfigField>();
        var section = "";
        var lineNumber = 0;

        foreach (var rawLine in File.ReadLines(configPath))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']') && line.Length > 2)
            {
                section = line[1..^1].Trim();
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            var field = new ClientLauncherGateConfigField(section, key, value, lineNumber);
            if (ClientLauncherGateContainsToken(key, "main"))
            {
                mainFields.Add(field);
            }

            if (ClientLauncherGateContainsToken(key, "process"))
            {
                processFields.Add(field);
            }

            if (ClientLauncherGateContainsToken(key, "patch") ||
                ClientLauncherGateContainsToken(section, "patch"))
            {
                patchFields.Add(field);
            }
        }

        return new ClientLauncherGateConfigReport(configPath, true, mainFields, processFields, patchFields);
    }

    private static ClientLauncherGateGameInfoReport ReadClientLauncherGateGameInfo(string gameInfoPath)
    {
        if (!File.Exists(gameInfoPath))
        {
            return new ClientLauncherGateGameInfoReport(gameInfoPath, false, null, null);
        }

        string? gamePath = null;
        string? gameProcess = null;
        foreach (var rawLine in File.ReadLines(gameInfoPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#') || line.StartsWith('['))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (key.Equals("GamePath", StringComparison.OrdinalIgnoreCase))
            {
                gamePath = value;
            }
            else if (key.Equals("GameProcess", StringComparison.OrdinalIgnoreCase))
            {
                gameProcess = value;
            }
        }

        return new ClientLauncherGateGameInfoReport(gameInfoPath, true, gamePath, gameProcess);
    }

    private static ClientLauncherGateVersionReport ReadClientLauncherGateVersion(string versionPath)
    {
        if (!File.Exists(versionPath))
        {
            return new ClientLauncherGateVersionReport(versionPath, false, null);
        }

        var version = File.ReadLines(versionPath)
            .Select(static line => line.Trim())
            .FirstOrDefault(static line => line.Length > 0);
        return new ClientLauncherGateVersionReport(versionPath, true, version);
    }

    private static IReadOnlyList<ClientLauncherGateUpdaterPatchReport> ReadClientLauncherGateUpdaterPatchReports(
        string clientRoot)
    {
        var oldUrlBytes = Encoding.ASCII.GetBytes(ClientLauncherGateOldUpdaterUrl);
        var newUrlBytes = Encoding.ASCII.GetBytes(ClientLauncherGateNewUpdaterUrl);

        return ClientLauncherGateUpdaterFileNames
            .Select(fileName =>
            {
                var path = Path.Combine(clientRoot, fileName);
                if (!File.Exists(path))
                {
                    return new ClientLauncherGateUpdaterPatchReport(fileName, path, false, false, false);
                }

                var bytes = File.ReadAllBytes(path);
                return new ClientLauncherGateUpdaterPatchReport(
                    fileName,
                    path,
                    true,
                    ContainsClientLauncherGateByteSequence(bytes, oldUrlBytes),
                    ContainsClientLauncherGateByteSequence(bytes, newUrlBytes));
            })
            .ToArray();
    }

    private static ClientLauncherGateLogReport ReadClientLauncherGateLogs(string clientRoot, int topCount)
    {
        var debugRoot = Path.Combine(clientRoot, "debug");
        if (!Directory.Exists(debugRoot))
        {
            return new ClientLauncherGateLogReport([], [], []);
        }

        var logs = Directory
            .EnumerateFiles(debugRoot, "tqan*.log", SearchOption.TopDirectoryOnly)
            .Select(static path => new FileInfo(path))
            .OrderByDescending(static file => file.LastWriteTimeUtc)
            .ThenBy(static file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Take(ClientLauncherGateAuditMaxLogs)
            .OrderBy(static file => file.LastWriteTimeUtc)
            .ThenBy(static file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var summaries = new List<ClientLauncherGateLogSummary>();
        var md5Paths = new Dictionary<string, MutableClientLauncherGateMd5Path>(StringComparer.OrdinalIgnoreCase);
        var hints = new Queue<ClientLauncherGateHint>();
        var sequence = 0;

        foreach (var log in logs)
        {
            var lineNumber = 0;
            var lastFileNamePath = "";
            foreach (var line in File.ReadLines(log.FullName))
            {
                lineNumber++;
                sequence++;

                if (TryExtractClientLauncherGateFileNamePath(line, out var fileNamePath))
                {
                    lastFileNamePath = fileNamePath;
                }

                if (TryExtractClientLauncherGateMd5Path(line, out var relativePath))
                {
                    if (relativePath.Length == 0 && lastFileNamePath.Length > 0)
                    {
                        relativePath = lastFileNamePath;
                    }

                    if (relativePath.Length == 0)
                    {
                        continue;
                    }

                    var presence = CreateClientLauncherGatePathPresence(clientRoot, relativePath);
                    if (!md5Paths.TryGetValue(relativePath, out var pathReport))
                    {
                        pathReport = new MutableClientLauncherGateMd5Path(relativePath, presence);
                        md5Paths.Add(relativePath, pathReport);
                    }

                    pathReport.Count++;
                    pathReport.LastLogPath = log.FullName;
                    pathReport.LastLineNumber = lineNumber;
                    pathReport.LastSequence = sequence;
                }

                if (TryCreateClientLauncherGateHint(log.FullName, lineNumber, sequence, line, out var hint))
                {
                    hints.Enqueue(hint);
                    while (hints.Count > topCount)
                    {
                        hints.Dequeue();
                    }
                }
            }

            summaries.Add(new ClientLauncherGateLogSummary(log.FullName, log.LastWriteTimeUtc, lineNumber));
        }

        return new ClientLauncherGateLogReport(
            summaries,
            md5Paths.Values
                .Select(static path => path.ToReport())
                .ToArray(),
            hints.ToArray());
    }

    private static bool TryExtractClientLauncherGateMd5Path(string line, out string relativePath)
    {
        relativePath = "";
        const string marker = "MD5 Get File Error";
        var markerIndex = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        var equalsMarkerIndex = line.IndexOf("MD5=Get File Error", StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0 && equalsMarkerIndex >= 0)
        {
            return true;
        }

        if (markerIndex < 0)
        {
            return false;
        }

        var tail = line[(markerIndex + marker.Length)..].Trim();
        tail = ClientLauncherGateTrimPathToken(tail);
        foreach (var prefix in new[] { "filename", "path", "file" })
        {
            if (tail.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                tail = ClientLauncherGateTrimPathToken(tail[prefix.Length..]);
                break;
            }
        }

        var whitespaceIndex = tail.IndexOfAny([' ', '\t']);
        if (whitespaceIndex > 0 && tail[..whitespaceIndex].IndexOfAny(['/', '\\']) >= 0)
        {
            tail = tail[..whitespaceIndex];
        }

        relativePath = NormalizeClientLauncherGateRelativePath(tail);
        return true;
    }

    private static bool TryExtractClientLauncherGateFileNamePath(string line, out string relativePath)
    {
        relativePath = "";
        const string marker = "FileName";
        var markerIndex = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return false;
        }

        var tail = line[(markerIndex + marker.Length)..];
        relativePath = NormalizeClientLauncherGateRelativePath(tail);
        return relativePath.Length > 0;
    }

    private static bool TryCreateClientLauncherGateHint(
        string logPath,
        int lineNumber,
        int sequence,
        string line,
        out ClientLauncherGateHint hint)
    {
        hint = new ClientLauncherGateHint("", 0, 0, "", "");

        string? category = null;
        if (line.Contains("MD5 Get File Error", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            category = "error";
        }
        else if (line.Contains("download", StringComparison.OrdinalIgnoreCase))
        {
            category = "download";
        }
        else if (line.Contains("update", StringComparison.OrdinalIgnoreCase))
        {
            category = "update";
        }
        else if (line.Contains("patch", StringComparison.OrdinalIgnoreCase))
        {
            category = "patch";
        }

        if (category is null)
        {
            return false;
        }

        hint = new ClientLauncherGateHint(logPath, lineNumber, sequence, category, TrimClientLauncherGatePreview(line));
        return true;
    }

    private static ClientLauncherGatePathPresence CreateClientLauncherGatePathPresence(
        string clientRoot,
        string relativePath)
    {
        var resolvedPath = ResolveClientLauncherGatePath(clientRoot, relativePath);
        var exists = File.Exists(resolvedPath);
        var delSiblingPath = resolvedPath + ".del";
        return new ClientLauncherGatePathPresence(resolvedPath, exists, File.Exists(delSiblingPath), delSiblingPath);
    }

    private static string ResolveClientLauncherGatePath(string clientRoot, string relativePath)
    {
        var nativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.IsPathRooted(nativePath)
            ? nativePath
            : Path.Combine(clientRoot, nativePath));
    }

    private static string NormalizeClientLauncherGateRelativePath(string value)
    {
        var normalized = ClientLauncherGateTrimPathToken(value).Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized;
    }

    private static string ClientLauncherGateTrimPathToken(string value)
    {
        return value.Trim().Trim(':', '=', '-', '>', '[', ']', '(', ')', '"', '\'', ' ');
    }

    private static bool ClientLauncherGateContainsToken(string value, string token)
    {
        return value.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsClientLauncherGateByteSequence(byte[] bytes, byte[] sequence)
    {
        if (sequence.Length == 0 || bytes.Length < sequence.Length)
        {
            return false;
        }

        for (var index = 0; index <= bytes.Length - sequence.Length; index++)
        {
            var matches = true;
            for (var sequenceIndex = 0; sequenceIndex < sequence.Length; sequenceIndex++)
            {
                if (bytes[index + sequenceIndex] != sequence[sequenceIndex])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    private static string TrimClientLauncherGatePreview(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length <= ClientLauncherGateAuditMaxPreviewLength
            ? trimmed
            : trimmed[..ClientLauncherGateAuditMaxPreviewLength] + "...";
    }

    private static void PrintClientLauncherGateAuditReport(
        ClientLauncherGateAuditReport report,
        ClientLauncherGateAuditOptions options)
    {
        Console.WriteLine("Client launcher gate audit:");
        Console.WriteLine($"Client root: {report.ClientRoot}");
        Console.WriteLine($"AutoPatch/config.ini: exists={FormatClientLauncherGateBool(report.Config.Exists)} path={report.Config.Path}");
        PrintClientLauncherGateConfigFields("Launcher main fields", report.Config.MainFields);
        PrintClientLauncherGateConfigFields("Launcher process fields", report.Config.ProcessFields);
        PrintClientLauncherGateConfigFields("Launcher patch fields", report.Config.PatchFields);
        PrintClientLauncherGateGameInfo("GameInfo.ini", report.GameInfo);
        PrintClientLauncherGateGameInfo("ini/GameInfo.ini", report.IniGameInfo);
        Console.WriteLine($"GameInfo comparison: status={GetClientLauncherGateGameInfoComparisonStatus(report.GameInfo, report.IniGameInfo)}");
        Console.WriteLine(
            $"version.dat: exists={FormatClientLauncherGateBool(report.Version.Exists)} " +
            $"version={report.Version.Version ?? "<missing>"} path={report.Version.Path}");

        Console.WriteLine($"Updater local routing patch state: {report.UpdaterPatchReports.Count}");
        foreach (var updater in report.UpdaterPatchReports)
        {
            Console.WriteLine(
                $"  {updater.FileName}: exists={FormatClientLauncherGateBool(updater.Exists)} " +
                $"oldSafeloginUrl={FormatClientLauncherGateBool(updater.ContainsOldSafeloginUrl)} " +
                $"localhostUrl={FormatClientLauncherGateBool(updater.ContainsLocalhostUrl)} " +
                $"path={updater.Path}");
        }

        Console.WriteLine($"Debug tqan logs considered: {report.Logs.Count}");
        foreach (var log in report.Logs)
        {
            Console.WriteLine(
                $"  {Path.GetFileName(log.Path)} lines={log.LineCount} " +
                $"lastWriteUtc={log.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss}");
        }

        Console.WriteLine($"MD5 get file error paths: {report.Md5Paths.Count}");
        foreach (var path in report.Md5Paths.Take(options.TopCount))
        {
            Console.WriteLine(
                $"  {path.RelativePath} count={path.Count} " +
                $"exists={FormatClientLauncherGateBool(path.Presence.Exists)} " +
                $"delSibling={FormatClientLauncherGateBool(path.Presence.DelSiblingExists)} " +
                $"last={Path.GetFileName(path.LastLogPath)}:{path.LastLineNumber} " +
                $"resolved={path.Presence.ResolvedPath}");
        }

        if (report.Md5Paths.Count == 0)
        {
            Console.WriteLine("  none");
        }

        Console.WriteLine($"Recent update/download/error hints: {report.Hints.Count}");
        foreach (var hint in report.Hints)
        {
            Console.WriteLine($"  {Path.GetFileName(hint.LogPath)}:{hint.LineNumber} [{hint.Category}] {hint.Preview}");
        }

        if (report.Hints.Count == 0)
        {
            Console.WriteLine("  none");
        }
    }

    private static void PrintClientLauncherGateGameInfo(
        string label,
        ClientLauncherGateGameInfoReport report)
    {
        Console.WriteLine(
            $"{label}: exists={FormatClientLauncherGateBool(report.Exists)} " +
            $"GamePath={report.GamePath ?? "<missing>"} " +
            $"GameProcess={report.GameProcess ?? "<missing>"} " +
            $"path={report.Path}");
    }

    private static string GetClientLauncherGateGameInfoComparisonStatus(
        ClientLauncherGateGameInfoReport root,
        ClientLauncherGateGameInfoReport ini)
    {
        if (!root.Exists && !ini.Exists)
        {
            return "both-missing";
        }

        if (root.Exists != ini.Exists)
        {
            return "single-present";
        }

        return string.Equals(root.GamePath, ini.GamePath, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(root.GameProcess, ini.GameProcess, StringComparison.OrdinalIgnoreCase)
                ? "match"
                : "mismatch";
    }

    private static void PrintClientLauncherGateConfigFields(
        string title,
        IReadOnlyList<ClientLauncherGateConfigField> fields)
    {
        Console.WriteLine($"{title}: {fields.Count}");
        foreach (var field in fields)
        {
            var section = field.Section.Length == 0 ? "<root>" : field.Section;
            Console.WriteLine($"  [{section}] {field.Key}={field.Value} line={field.LineNumber}");
        }
    }

    private static string FormatClientLauncherGateBool(bool value)
    {
        return value ? "true" : "false";
    }

    private sealed record ClientLauncherGateAuditOptions(string ClientRoot, int TopCount);

    private sealed record ClientLauncherGateAuditReport(
        string ClientRoot,
        ClientLauncherGateConfigReport Config,
        ClientLauncherGateGameInfoReport GameInfo,
        ClientLauncherGateGameInfoReport IniGameInfo,
        ClientLauncherGateVersionReport Version,
        IReadOnlyList<ClientLauncherGateUpdaterPatchReport> UpdaterPatchReports,
        IReadOnlyList<ClientLauncherGateLogSummary> Logs,
        IReadOnlyList<ClientLauncherGateMd5Path> Md5Paths,
        IReadOnlyList<ClientLauncherGateHint> Hints);

    private sealed record ClientLauncherGateConfigReport(
        string Path,
        bool Exists,
        IReadOnlyList<ClientLauncherGateConfigField> MainFields,
        IReadOnlyList<ClientLauncherGateConfigField> ProcessFields,
        IReadOnlyList<ClientLauncherGateConfigField> PatchFields);

    private sealed record ClientLauncherGateConfigField(string Section, string Key, string Value, int LineNumber);

    private sealed record ClientLauncherGateGameInfoReport(
        string Path,
        bool Exists,
        string? GamePath,
        string? GameProcess);

    private sealed record ClientLauncherGateVersionReport(string Path, bool Exists, string? Version);

    private sealed record ClientLauncherGateUpdaterPatchReport(
        string FileName,
        string Path,
        bool Exists,
        bool ContainsOldSafeloginUrl,
        bool ContainsLocalhostUrl);

    private sealed record ClientLauncherGateLogReport(
        IReadOnlyList<ClientLauncherGateLogSummary> Logs,
        IReadOnlyList<ClientLauncherGateMd5Path> Md5Paths,
        IReadOnlyList<ClientLauncherGateHint> Hints);

    private sealed record ClientLauncherGateLogSummary(string Path, DateTime LastWriteTimeUtc, int LineCount);

    private sealed record ClientLauncherGateMd5Path(
        string RelativePath,
        int Count,
        ClientLauncherGatePathPresence Presence,
        string LastLogPath,
        int LastLineNumber,
        int LastSequence);

    private sealed record ClientLauncherGatePathPresence(
        string ResolvedPath,
        bool Exists,
        bool DelSiblingExists,
        string DelSiblingPath);

    private sealed record ClientLauncherGateHint(
        string LogPath,
        int LineNumber,
        int Sequence,
        string Category,
        string Preview);

    private sealed class MutableClientLauncherGateMd5Path(
        string relativePath,
        ClientLauncherGatePathPresence presence)
    {
        public string RelativePath { get; } = relativePath;

        public ClientLauncherGatePathPresence Presence { get; } = presence;

        public int Count { get; set; }

        public string LastLogPath { get; set; } = "";

        public int LastLineNumber { get; set; }

        public int LastSequence { get; set; }

        public ClientLauncherGateMd5Path ToReport()
        {
            return new ClientLauncherGateMd5Path(
                RelativePath,
                Count,
                Presence,
                LastLogPath,
                LastLineNumber,
                LastSequence);
        }
    }
}

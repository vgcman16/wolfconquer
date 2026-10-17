using System.Globalization;
using System.Text.RegularExpressions;

internal static partial class Program
{
    private static readonly Regex ClientStartupTimestampRegex = new(
        @"^\s*\[?(?:(?<date>\d{4}[-/]\d{1,2}[-/]\d{1,2})[ T])?(?<time>\d{1,2}:\d{2}:\d{2})(?:\.\d+)?\]?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ClientStartupTrailingTimestampRegex = new(
        @"--\s+\w{3}\s+\w{3}\s+\d{1,2}\s+(?<time>\d{1,2}:\d{2}:\d{2})\s+\d{4}\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ClientStartupMissingTextureRegex = new(
        @"\btexture\s*\[\s*(?<textureId>\d+)\s*\]\s+not\s+found\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ClientStartupCreateNewAniRegex = new(
        @"\bCreateNewAni\s*\(\s*(?<aniPath>[^,\)]+?)\s*,\s*(?<aniIndex>[^\)]+?)\s*\)\s*failed\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ClientStartupMissingAniIndexRegex = new(
        @"\bAni\s+Index\s*\[\s*(?<aniIndex>[^\]]+?)\s*\]\s+Not\s+Found\s+In\s*\[\s*(?<aniPath>[^\]]+?)\s*\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ClientStartupTexturePathRegex = new(
        @"(?<path>[A-Za-z0-9_./\\:-]+\.(?:dds|png|jpg|jpeg|tga|bmp))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ClientStartupMissingFontRegex = new(
        @"\bCan\s+not\s+find\s+font:\s*\[(?<font>[^\]]+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ClientStartupLuaRequireBufferChunkLineRegex = new(
        @"\[string\s+""CLuaVM::requirebuffer\(\)""\]:(?<line>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ClientStartupLuaRequireBufferStackLineRegex = new(
        @"\bline\s+(?<line>\d+)\s+\[\s*CLuaVM::requirebuffer\(\)\s*:\s*line\s+0\s*\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ClientStartupLuaSourceFrameRegex = new(
        @"\[@(?<path>[^\]:]+?\.lua)\s*:\s*line\s*(?<line>-?\d+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ClientStartupLuaNearTokenRegex = new(
        @"\bnear\s+(?<token>'[^']*'|""[^""]*""|[^\s]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private const int MaxClientStartupTextureAniFiles = 256;
    private const int MaxClientStartupTextureAniLinesPerFile = 10_000;
    private const long MaxClientStartupTextureAniBytesPerFile = 1_048_576;
    private const int MaxClientStartupTextureAniReferences = 32;
    private const int MaxClientStartupCriticalAniLinesPerFile = 250_000;
    private const long MaxClientStartupCriticalAniBytesPerFile = 16_777_216;
    private const int MaxClientStartupLuaFiles = 4096;
    private const int MaxClientStartupLuaLineCandidates = 24;
    private const int MinClientStartupLuaCandidateLine = 128;

    static int ClientStartupAudit(string[] args)
    {
        ClientStartupAuditOptions options;
        try
        {
            options = ResolveClientStartupAuditOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Usage: WolfConquer.DataTool client-startup-audit <logPath> [--all] [--top N] [--gap-minutes N] [--client-root path] [--reference-root label=path]");
            return 1;
        }

        if (!File.Exists(options.LogPath))
        {
            Console.Error.WriteLine($"Client debug log not found: {options.LogPath}");
            return 1;
        }

        ClientStartupAuditReport report;
        try
        {
            report = CreateClientStartupAuditReport(options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not read client debug log: {ex.Message}");
            return 1;
        }

        PrintClientStartupAuditReport(report, options);
        return 0;
    }

    static ClientStartupAuditOptions ResolveClientStartupAuditOptions(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("Missing client debug log path.");
        }

        string? logPath = null;
        var includeAllGroups = false;
        var topCount = 20;
        var gapMinutes = 2;
        string? clientRoot = null;
        var referenceRoots = new List<ClientStartupReferenceRoot>();

        for (var index = 1; index < args.Length; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--all", StringComparison.OrdinalIgnoreCase))
            {
                includeAllGroups = true;
                continue;
            }

            if (string.Equals(arg, "--top", StringComparison.OrdinalIgnoreCase))
            {
                if (index == args.Length - 1)
                {
                    throw new ArgumentException("Missing value for --top.");
                }

                topCount = ParseClientStartupPositiveInteger(args[++index], "--top");
                continue;
            }

            const string topPrefix = "--top=";
            if (arg.StartsWith(topPrefix, StringComparison.OrdinalIgnoreCase))
            {
                topCount = ParseClientStartupPositiveInteger(arg[topPrefix.Length..], "--top");
                continue;
            }

            if (string.Equals(arg, "--gap-minutes", StringComparison.OrdinalIgnoreCase))
            {
                if (index == args.Length - 1)
                {
                    throw new ArgumentException("Missing value for --gap-minutes.");
                }

                gapMinutes = ParseClientStartupPositiveInteger(args[++index], "--gap-minutes");
                continue;
            }

            const string gapPrefix = "--gap-minutes=";
            if (arg.StartsWith(gapPrefix, StringComparison.OrdinalIgnoreCase))
            {
                gapMinutes = ParseClientStartupPositiveInteger(arg[gapPrefix.Length..], "--gap-minutes");
                continue;
            }

            if (string.Equals(arg, "--client-root", StringComparison.OrdinalIgnoreCase))
            {
                if (index == args.Length - 1)
                {
                    throw new ArgumentException("Missing value for --client-root.");
                }

                clientRoot = Path.GetFullPath(args[++index]);
                continue;
            }

            const string clientRootPrefix = "--client-root=";
            if (arg.StartsWith(clientRootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                clientRoot = Path.GetFullPath(arg[clientRootPrefix.Length..]);
                continue;
            }

            if (string.Equals(arg, "--reference-root", StringComparison.OrdinalIgnoreCase))
            {
                if (index == args.Length - 1)
                {
                    throw new ArgumentException("Missing value for --reference-root.");
                }

                referenceRoots.Add(ParseClientStartupReferenceRoot(args[++index]));
                continue;
            }

            const string referenceRootPrefix = "--reference-root=";
            if (arg.StartsWith(referenceRootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                referenceRoots.Add(ParseClientStartupReferenceRoot(arg[referenceRootPrefix.Length..]));
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown client-startup-audit option: {arg}");
            }

            if (logPath is not null)
            {
                throw new ArgumentException("Too many positional arguments for client-startup-audit.");
            }

            logPath = arg;
        }

        if (string.IsNullOrWhiteSpace(logPath))
        {
            throw new ArgumentException("Missing client debug log path.");
        }

        return new ClientStartupAuditOptions(
            Path.GetFullPath(logPath),
            includeAllGroups,
            topCount,
            TimeSpan.FromMinutes(gapMinutes),
            clientRoot,
            referenceRoots);
    }

    static int ParseClientStartupPositiveInteger(string value, string optionName)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed <= 0)
        {
            throw new FormatException($"{optionName} requires a positive integer.");
        }

        return parsed;
    }

    static ClientStartupReferenceRoot ParseClientStartupReferenceRoot(string value)
    {
        var separator = value.IndexOf('=', StringComparison.Ordinal);
        if (separator <= 0 || separator == value.Length - 1)
        {
            throw new ArgumentException("--reference-root requires label=path.");
        }

        var label = value[..separator].Trim();
        if (label.Length == 0 || label.Any(character => char.IsWhiteSpace(character)))
        {
            throw new ArgumentException("--reference-root label must be non-empty and contain no whitespace.");
        }

        return new ClientStartupReferenceRoot(label, Path.GetFullPath(value[(separator + 1)..]));
    }

    static ClientStartupAuditReport CreateClientStartupAuditReport(ClientStartupAuditOptions options)
    {
        var groups = new List<ClientStartupWarningGroup>();
        var currentWarnings = new List<ClientStartupWarning>();
        TimeSpan? currentStart = null;
        TimeSpan? currentEnd = null;
        TimeSpan? previousTimestamp = null;
        var firstLineNumber = 0;
        var lastLineNumber = 0;
        var linesScanned = 0;

        foreach (var line in File.ReadLines(options.LogPath))
        {
            linesScanned++;
            var warning = TryParseClientStartupWarning(line, linesScanned);
            if (warning is null)
            {
                continue;
            }

            if (ShouldStartNewClientStartupGroup(currentWarnings, previousTimestamp, warning.Timestamp, options.GroupGap))
            {
                AddClientStartupAuditGroup(
                    groups,
                    currentWarnings,
                    firstLineNumber,
                    lastLineNumber,
                    currentStart,
                    currentEnd);
                currentWarnings.Clear();
                currentStart = null;
                currentEnd = null;
                firstLineNumber = 0;
                lastLineNumber = 0;
            }

            if (currentWarnings.Count == 0)
            {
                firstLineNumber = warning.LineNumber;
                currentStart = warning.Timestamp;
            }

            currentWarnings.Add(warning);
            lastLineNumber = warning.LineNumber;
            currentEnd = warning.Timestamp ?? currentEnd;
            previousTimestamp = warning.Timestamp ?? previousTimestamp;
        }

        AddClientStartupAuditGroup(
            groups,
            currentWarnings,
            firstLineNumber,
            lastLineNumber,
            currentStart,
            currentEnd);

        return new ClientStartupAuditReport(options.LogPath, linesScanned, groups);
    }

    static bool ShouldStartNewClientStartupGroup(
        IReadOnlyCollection<ClientStartupWarning> currentWarnings,
        TimeSpan? previousTimestamp,
        TimeSpan? currentTimestamp,
        TimeSpan groupGap)
    {
        if (currentWarnings.Count == 0 || previousTimestamp is null || currentTimestamp is null)
        {
            return false;
        }

        var gap = currentTimestamp.Value - previousTimestamp.Value;
        return gap < TimeSpan.Zero || gap > groupGap;
    }

    static void AddClientStartupAuditGroup(
        List<ClientStartupWarningGroup> groups,
        IReadOnlyList<ClientStartupWarning> warnings,
        int firstLineNumber,
        int lastLineNumber,
        TimeSpan? startedAt,
        TimeSpan? endedAt)
    {
        if (warnings.Count == 0)
        {
            return;
        }

        groups.Add(new ClientStartupWarningGroup(
            groups.Count + 1,
            firstLineNumber,
            lastLineNumber,
            startedAt,
            endedAt,
            warnings.ToArray()));
    }

    static ClientStartupWarning? TryParseClientStartupWarning(string line, int lineNumber)
    {
        var timestamp = TryParseClientStartupTimestamp(line);
        var message = line.Trim();
        if (message.Length == 0)
        {
            return null;
        }

        if (TryExtractMissingClientStartupFile(message, out var missingFile))
        {
            return new ClientStartupWarning(
                ClientStartupWarningKind.MissingFile,
                missingFile,
                message,
                lineNumber,
                timestamp);
        }

        if (TryExtractClientStartupDownload(message, out var downloadFile))
        {
            return new ClientStartupWarning(
                ClientStartupWarningKind.Download,
                downloadFile,
                message,
                lineNumber,
                timestamp);
        }

        if (TryExtractClientStartupErrorDataFile(message, out var errorDataFile))
        {
            return new ClientStartupWarning(
                ClientStartupWarningKind.ErrorData,
                errorDataFile,
                message,
                lineNumber,
                timestamp);
        }

        if (TryExtractClientStartupInvalidIdentFile(message, out var invalidIdentFile))
        {
            return new ClientStartupWarning(
                ClientStartupWarningKind.InvalidIdent,
                invalidIdentFile,
                message,
                lineNumber,
                timestamp);
        }

        if (TryExtractClientStartupMissingTexture(message, out var textureId))
        {
            return new ClientStartupWarning(
                ClientStartupWarningKind.MissingTexture,
                FormatClientStartupTextureSubject(textureId),
                message,
                lineNumber,
                timestamp);
        }

        if (TryExtractClientStartupAniWarning(message, out var aniSubject))
        {
            return new ClientStartupWarning(
                ClientStartupWarningKind.AniWarning,
                aniSubject,
                message,
                lineNumber,
                timestamp);
        }

        if (TryExtractClientStartupFontRendererFailure(message, out var fontRendererSubject))
        {
            return new ClientStartupWarning(
                ClientStartupWarningKind.FontRendererFailure,
                fontRendererSubject,
                message,
                lineNumber,
                timestamp);
        }

        if (TryExtractClientStartupNamedPipeFailure(message, out var namedPipeSubject))
        {
            return new ClientStartupWarning(
                ClientStartupWarningKind.NamedPipeFailure,
                namedPipeSubject,
                message,
                lineNumber,
                timestamp);
        }

        if (TryExtractClientStartupLuaError(message, out var luaError))
        {
            return new ClientStartupWarning(
                ClientStartupWarningKind.LuaError,
                luaError,
                message,
                lineNumber,
                timestamp);
        }

        return null;
    }

    static TimeSpan? TryParseClientStartupTimestamp(string line)
    {
        var match = ClientStartupTimestampRegex.Match(line);
        if (!match.Success)
        {
            match = ClientStartupTrailingTimestampRegex.Match(line);
            if (!match.Success)
            {
                return null;
            }
        }

        return TimeSpan.TryParseExact(
            match.Groups["time"].Value,
            @"h\:mm\:ss",
            CultureInfo.InvariantCulture,
            out var singleDigitHour)
            ? singleDigitHour
            : TimeSpan.TryParseExact(
                match.Groups["time"].Value,
                @"hh\:mm\:ss",
                CultureInfo.InvariantCulture,
                out var twoDigitHour)
                ? twoDigitHour
                : null;
    }

    static bool TryExtractMissingClientStartupFile(string message, out string filePath)
    {
        filePath = string.Empty;
        var tail = GetTextAfterIgnoreCase(message, "Can't Load File Which is") ??
            GetTextAfterIgnoreCase(message, "Can't Load File") ??
            GetTextAfterIgnoreCase(message, "Load File Error") ??
            GetTextAfterIgnoreCase(message, "cannot open");
        if (tail is null && message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            tail = GetTextAfterIgnoreCase(message, "ERROR: file");
        }

        if (tail is null)
        {
            return false;
        }

        tail = StripClientStartupNoisePrefix(tail);
        return TryExtractClientStartupPathToken(tail, out filePath);
    }

    static bool TryExtractClientStartupDownload(string message, out string filePath)
    {
        filePath = string.Empty;
        var tail = GetTextAfterIgnoreCase(message, "Downloading=");
        if (tail is null)
        {
            return false;
        }

        return TryExtractClientStartupPathToken(tail, out filePath);
    }

    static bool TryExtractClientStartupErrorDataFile(string message, out string filePath)
    {
        filePath = string.Empty;
        if (!message.Contains("error data", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TryExtractSpecificClientStartupIniPath(message, "NpcX.ini", out filePath) ||
            TryExtractSpecificClientStartupIniPath(message, "Npc.ini", out filePath))
        {
            return true;
        }

        return TryExtractClientStartupPathToken(message, out filePath);
    }

    static bool TryExtractClientStartupInvalidIdentFile(string message, out string filePath)
    {
        filePath = string.Empty;
        var tail = GetTextAfterIgnoreCase(message, "Invalid Ident:");
        if (tail is null)
        {
            return false;
        }

        return TryExtractClientStartupPathToken(tail, out filePath);
    }

    static bool TryExtractClientStartupMissingTexture(string message, out string textureId)
    {
        textureId = string.Empty;
        var match = ClientStartupMissingTextureRegex.Match(message);
        if (!match.Success)
        {
            return false;
        }

        textureId = match.Groups["textureId"].Value;
        return textureId.Length > 0;
    }

    static bool TryExtractClientStartupAniWarning(string message, out string aniSubject)
    {
        aniSubject = string.Empty;
        var match = ClientStartupCreateNewAniRegex.Match(message);
        if (!match.Success)
        {
            match = ClientStartupMissingAniIndexRegex.Match(message);
            if (!match.Success)
            {
                return false;
            }
        }

        var aniPath = NormalizeClientStartupPath(match.Groups["aniPath"].Value);
        var aniIndex = NormalizeClientStartupAniIndex(match.Groups["aniIndex"].Value);
        if (aniPath.Length == 0 || aniIndex.Length == 0)
        {
            return false;
        }

        aniSubject = FormatClientStartupAniSubject(aniPath, aniIndex);
        return true;
    }

    static bool TryExtractClientStartupFontRendererFailure(string message, out string subject)
    {
        subject = string.Empty;
        var match = ClientStartupMissingFontRegex.Match(message);
        if (match.Success)
        {
            subject = NormalizeClientStartupPath(match.Groups["font"].Value);
            return subject.Length > 0;
        }

        if (!message.Contains("TextRender.cpp", StringComparison.OrdinalIgnoreCase) ||
            !message.Contains("ASSERT(", StringComparison.OrdinalIgnoreCase) ||
            !message.Contains("s_FontSet", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        subject = "TextRender.cpp#s_FontSet";
        return true;
    }

    static bool TryExtractClientStartupNamedPipeFailure(string message, out string subject)
    {
        subject = string.Empty;
        if (!message.Contains("Failed to open the appointed named pipe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        subject = "named-pipe/open";
        return true;
    }

    static bool TryExtractClientStartupLuaError(string message, out string error)
    {
        error = string.Empty;
        var tail = GetTextAfterIgnoreCase(message, "Lua error:");
        if (tail is null)
        {
            return false;
        }

        var end = tail.IndexOf(" -- ", StringComparison.Ordinal);
        if (end >= 0)
        {
            tail = tail[..end];
        }

        error = tail.Trim();
        if (error.Length == 0)
        {
            error = "Lua error";
        }

        return true;
    }

    static bool TryExtractSpecificClientStartupIniPath(string message, string fileName, out string filePath)
    {
        filePath = string.Empty;
        var index = message.IndexOf(fileName, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        var start = index;
        while (start > 0 && IsClientStartupPathCharacter(message[start - 1]))
        {
            start--;
        }

        filePath = NormalizeClientStartupPath(message[start..(index + fileName.Length)]);
        return true;
    }

    static string? GetTextAfterIgnoreCase(string value, string marker)
    {
        var index = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0
            ? null
            : value[(index + marker.Length)..];
    }

    static string StripClientStartupNoisePrefix(string value)
    {
        var result = value.Trim();
        while (result.Length > 0)
        {
            result = result.TrimStart(':', '=', '-', ' ', '\t');

            if (result.StartsWith("Reason", StringComparison.OrdinalIgnoreCase))
            {
                result = result["Reason".Length..];
                continue;
            }

            if (result.StartsWith("Which is", StringComparison.OrdinalIgnoreCase))
            {
                result = result["Which is".Length..];
                continue;
            }

            break;
        }

        return result;
    }

    static bool TryExtractClientStartupPathToken(string value, out string filePath)
    {
        filePath = string.Empty;
        var text = StripClientStartupNoisePrefix(value);
        var start = -1;

        for (var index = 0; index < text.Length; index++)
        {
            if (!IsClientStartupPathCharacter(text[index]))
            {
                continue;
            }

            start = index;
            break;
        }

        if (start < 0)
        {
            return false;
        }

        var end = start;
        while (end < text.Length && IsClientStartupPathCharacter(text[end]))
        {
            end++;
        }

        var candidate = NormalizeClientStartupPath(text[start..end]);
        if (!LooksLikeClientStartupPath(candidate))
        {
            return false;
        }

        filePath = candidate;
        return true;
    }

    static bool IsClientStartupPathCharacter(char value)
    {
        return char.IsLetterOrDigit(value) ||
            value is '_' or '-' or '.' or '/' or '\\' or ':';
    }

    static bool LooksLikeClientStartupPath(string value)
    {
        return value.Contains('/', StringComparison.Ordinal) ||
            value.EndsWith(".ini", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".dat", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
    }

    static string NormalizeClientStartupPath(string value)
    {
        return value.Trim().Trim('"', '\'', '.', ',', ';', ':').Replace('\\', '/');
    }

    static void PrintClientStartupAuditReport(ClientStartupAuditReport report, ClientStartupAuditOptions options)
    {
        Console.WriteLine("Client startup audit:");
        Console.WriteLine($"Log: {report.LogPath}");
        Console.WriteLine($"Lines scanned: {report.LinesScanned:N0}");
        Console.WriteLine($"Recognized warning lines: {report.WarningCount:N0}");
        Console.WriteLine($"Startup warning groups: {report.Groups.Count:N0}");

        IReadOnlyList<ClientStartupWarningGroup> groups = report.Groups.Count == 0
            ? Array.Empty<ClientStartupWarningGroup>()
            : options.IncludeAllGroups
                ? report.Groups
                : [report.Groups[^1]];

        PrintClientStartupCriticalResources(
            options,
            groups.SelectMany(group => group.Warnings).ToArray());

        if (report.Groups.Count == 0)
        {
            Console.WriteLine("No startup warnings recognized.");
            return;
        }

        foreach (var group in groups)
        {
            Console.WriteLine(
                options.IncludeAllGroups
                    ? $"Group {group.Index}/{report.Groups.Count}: {FormatClientStartupAuditGroupRange(group)}"
                    : $"Latest group: {group.Index}/{report.Groups.Count} {FormatClientStartupAuditGroupRange(group)}");
            PrintClientStartupAuditCategory("Lua errors", group.Warnings, ClientStartupWarningKind.LuaError, options.TopCount);
            PrintClientStartupLuaEvidence(group.Warnings, options);
            PrintClientStartupAuditCategory("Invalid ident files", group.Warnings, ClientStartupWarningKind.InvalidIdent, options.TopCount);
            PrintClientStartupFileProvenance("Invalid ident provenance", group.Warnings, ClientStartupWarningKind.InvalidIdent, options);
            PrintClientStartupAuditCategory("Missing textures", group.Warnings, ClientStartupWarningKind.MissingTexture, options.TopCount);
            PrintClientStartupTextureProvenance(group.Warnings, options);
            PrintClientStartupAuditCategory("ANI warnings", group.Warnings, ClientStartupWarningKind.AniWarning, options.TopCount);
            PrintClientStartupAniProvenance(group.Warnings, options);
            PrintClientStartupAuditCategory("Font renderer failures", group.Warnings, ClientStartupWarningKind.FontRendererFailure, options.TopCount);
            PrintClientStartupAuditCategory("Named pipe failures", group.Warnings, ClientStartupWarningKind.NamedPipeFailure, options.TopCount);
            PrintClientStartupAuditCategory("Missing files", group.Warnings, ClientStartupWarningKind.MissingFile, options.TopCount);
            PrintClientStartupMissingFileProvenance(group.Warnings, options);
            PrintClientStartupAuditCategory("Error data rows", group.Warnings, ClientStartupWarningKind.ErrorData, options.TopCount);
            PrintClientStartupAuditCategory("Download attempts", group.Warnings, ClientStartupWarningKind.Download, options.TopCount);
        }
    }

    static void PrintClientStartupCriticalResources(
        ClientStartupAuditOptions options,
        IReadOnlyList<ClientStartupWarning> warnings)
    {
        if (options.ClientRoot is null)
        {
            return;
        }

        var invalidIdentFiles = warnings
            .Where(warning => warning.Kind == ClientStartupWarningKind.InvalidIdent)
            .Select(warning => warning.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Console.WriteLine("Startup-critical resources:");
        PrintClientStartupCriticalFileResource(
            options.ClientRoot,
            "ini/activity_main.lua",
            ["ini/activity_main.lua"],
            invalidIdentFiles);
        PrintClientStartupCriticalFileResource(
            options.ClientRoot,
            "ini/CommonTip.lua",
            ["ini/CommonTip.lua"],
            invalidIdentFiles);
        PrintClientStartupCriticalFileResource(
            options.ClientRoot,
            "ini/singleemotion.lua",
            ["ini/singleemotion.lua"],
            invalidIdentFiles);
        PrintClientStartupCriticalFileResource(
            options.ClientRoot,
            "ini/Appkey.dat",
            ["ini/Appkey.dat"],
            invalidIdentFiles);
        PrintClientStartupCriticalFileResource(
            options.ClientRoot,
            "ini/3dtexture.dbc",
            ["ini/3dtexture.dbc"],
            invalidIdentFiles,
            ["ini/3dtexture.ini"]);
        PrintClientStartupCriticalFileResource(
            options.ClientRoot,
            "ini/Font.ini",
            ["ini/Font.ini"],
            invalidIdentFiles);
        PrintClientStartupCriticalFontRegistrationResource(options.ClientRoot, "Font/GB13000.ttq 14");
        PrintClientStartupCriticalFileResource(
            options.ClientRoot,
            "Font/GB13000.ttq",
            ["Font/GB13000.ttq"],
            invalidIdentFiles);
        PrintClientStartupCriticalFileResource(
            options.ClientRoot,
            "ini/FontSetting.ini",
            ["ini/FontSetting.ini"],
            invalidIdentFiles);
        PrintClientStartupCriticalAniResource(options.ClientRoot, "ani/Control.ani", "Complex_clpic1");
    }

    static void PrintClientStartupCriticalFileResource(
        string clientRoot,
        string subject,
        IReadOnlyList<string> primaryCandidates,
        ISet<string> invalidIdentFiles,
        IReadOnlyList<string>? fallbackCandidates = null)
    {
        var primaryMatches = GetExistingClientStartupCandidatePaths(clientRoot, primaryCandidates)
            .ToArray();
        var fallbackMatches = fallbackCandidates is null
            ? Array.Empty<string>()
            : GetExistingClientStartupCandidatePaths(clientRoot, fallbackCandidates)
                .ToArray();
        var allCandidates = fallbackCandidates is null
            ? primaryCandidates
            : primaryCandidates
                .Concat(fallbackCandidates)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        var allMatches = primaryMatches
            .Concat(fallbackMatches)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var requestedPathInvalid = invalidIdentFiles.Contains(NormalizeClientStartupPath(subject)) ||
            primaryCandidates.Any(candidate => invalidIdentFiles.Contains(NormalizeClientStartupPath(candidate)));
        var status = requestedPathInvalid
            ? "invalid-ident"
            : primaryMatches.Length > 0
                ? "present"
                : fallbackMatches.Length > 0
                    ? "fallback-present"
                    : "missing";

        Console.WriteLine(
            $"  {subject} status={status} matches={FormatClientStartupAuditList(allMatches)} " +
            $"candidates={FormatClientStartupAuditList(allCandidates)}");
    }

    static void PrintClientStartupCriticalFontRegistrationResource(string clientRoot, string registration)
    {
        const string fontIniPath = "ini/Font.ini";
        var presence = GetClientStartupCriticalTextEntryPresence(clientRoot, fontIniPath, registration);
        var subject = $"{fontIniPath}#{registration}";
        var match = presence.FileExists
            ? presence.EntryExists
                ? subject
                : fontIniPath
            : "none";

        Console.WriteLine(
            $"  {subject} status={FormatClientStartupTextEntryStatus(presence)} matches={match}");
    }

    static void PrintClientStartupCriticalAniResource(string clientRoot, string aniPath, string aniIndex)
    {
        var presence = GetClientStartupCriticalAniPresence(clientRoot, aniPath, aniIndex);
        var match = presence.FileExists
            ? FormatClientStartupAniMatch(aniPath, aniIndex, presence.IndexExists)
            : "none";

        Console.WriteLine(
            $"  {FormatClientStartupAniSubject(aniPath, aniIndex)} status={FormatClientStartupAniStatus(presence)} " +
            $"matches={match}");
    }

    static string FormatClientStartupAuditList(IReadOnlyList<string> values)
    {
        return values.Count == 0
            ? "none"
            : string.Join(",", values);
    }

    static ClientStartupAniPresence GetClientStartupCriticalAniPresence(string rootPath, string aniPath, string aniIndex)
    {
        var fullPath = Path.Combine(rootPath, aniPath.Replace('/', Path.DirectorySeparatorChar));
        if (!IsRelativeClientStartupCandidate(aniPath) || !File.Exists(fullPath))
        {
            return new ClientStartupAniPresence(false, false);
        }

        try
        {
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length > MaxClientStartupCriticalAniBytesPerFile)
            {
                return new ClientStartupAniPresence(true, false);
            }

            var lineCount = 0;
            foreach (var line in File.ReadLines(fullPath))
            {
                lineCount++;
                if (lineCount > MaxClientStartupCriticalAniLinesPerFile)
                {
                    break;
                }

                if (IsClientStartupAniIndexLine(line, aniIndex))
                {
                    return new ClientStartupAniPresence(true, true);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ClientStartupAniPresence(true, false);
        }

        return new ClientStartupAniPresence(true, false);
    }

    static ClientStartupTextEntryPresence GetClientStartupCriticalTextEntryPresence(
        string rootPath,
        string filePath,
        string requiredEntry)
    {
        var fullPath = Path.Combine(rootPath, filePath.Replace('/', Path.DirectorySeparatorChar));
        if (!IsRelativeClientStartupCandidate(filePath) || !File.Exists(fullPath))
        {
            return new ClientStartupTextEntryPresence(false, false);
        }

        try
        {
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length > MaxClientStartupCriticalAniBytesPerFile)
            {
                return new ClientStartupTextEntryPresence(true, false);
            }

            var lineCount = 0;
            foreach (var line in File.ReadLines(fullPath))
            {
                lineCount++;
                if (lineCount > MaxClientStartupCriticalAniLinesPerFile)
                {
                    break;
                }

                if (string.Equals(line.Trim(), requiredEntry, StringComparison.OrdinalIgnoreCase))
                {
                    return new ClientStartupTextEntryPresence(true, true);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ClientStartupTextEntryPresence(true, false);
        }

        return new ClientStartupTextEntryPresence(true, false);
    }

    static string FormatClientStartupAuditGroupRange(ClientStartupWarningGroup group)
    {
        var timeRange = group.StartedAt is null
            ? "time=<unknown>"
            : group.EndedAt is null || group.EndedAt == group.StartedAt
                ? $"time={group.StartedAt:hh\\:mm\\:ss}"
                : $"time={group.StartedAt:hh\\:mm\\:ss}-{group.EndedAt:hh\\:mm\\:ss}";
        return $"lines {group.FirstLineNumber}-{group.LastLineNumber} {timeRange} warnings={group.Warnings.Count:N0}";
    }

    static void PrintClientStartupAuditCategory(
        string title,
        IReadOnlyList<ClientStartupWarning> warnings,
        ClientStartupWarningKind kind,
        int topCount)
    {
        var categoryWarnings = warnings
            .Where(warning => warning.Kind == kind)
            .ToArray();
        var items = categoryWarnings
            .GroupBy(warning => warning.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                FilePath = group.Key,
                Count = group.Count(),
                FirstLineNumber = group.Min(warning => warning.LineNumber),
                LastLineNumber = group.Max(warning => warning.LineNumber),
                Sample = group.First().Message,
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
            .Take(topCount)
            .ToArray();

        Console.WriteLine($"{title}: {categoryWarnings.Length:N0}");
        foreach (var item in items)
        {
            Console.WriteLine(
                $"  {item.FilePath} count={item.Count:N0} firstLine={item.FirstLineNumber:N0} " +
                $"lastLine={item.LastLineNumber:N0} sample={item.Sample}");
        }
    }

    static void PrintClientStartupLuaEvidence(
        IReadOnlyList<ClientStartupWarning> warnings,
        ClientStartupAuditOptions options)
    {
        var evidence = GetClientStartupLuaRequireBufferEvidence(warnings, options.ClientRoot);
        if (evidence.Count == 0)
        {
            return;
        }

        Console.WriteLine("Lua failure evidence:");
        foreach (var item in evidence)
        {
            var sourceFramesText = item.SourceFrames.Count == 0
                ? "none"
                : string.Join(",", item.SourceFrames);
            var candidatesText = options.ClientRoot is null
                ? "not-checked"
                : item.ClientRootCandidates.Count == 0
                    ? "none"
                    : string.Join(",", item.ClientRootCandidates.Select(FormatClientStartupLuaLineCandidate));
            var generatedBufferHint = FormatClientStartupLuaGeneratedBufferHint(item, options.ClientRoot is not null);

            Console.WriteLine(
                $"  requirebuffer line={item.LineNumber} occurrences={item.Occurrences} " +
                $"firstLogLine={item.FirstLogLine} lastLogLine={item.LastLogLine} " +
                $"sourceFrames={sourceFramesText} clientRootLuaLineCandidates={candidatesText}{generatedBufferHint}");
        }
    }

    static IReadOnlyList<ClientStartupLuaRequireBufferEvidence> GetClientStartupLuaRequireBufferEvidence(
        IReadOnlyList<ClientStartupWarning> warnings,
        string? clientRoot)
    {
        var warningsByLine = new SortedDictionary<int, List<ClientStartupWarning>>();
        var generatedChunkLines = new SortedSet<int>();
        var nearTokensByLine = new SortedDictionary<int, SortedSet<string>>();
        var sourceFrames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var warning in warnings.Where(warning => warning.Kind == ClientStartupWarningKind.LuaError))
        {
            var lineNumbers = ExtractClientStartupLuaRequireBufferLineNumbers(warning.Message);
            foreach (var lineNumber in ExtractClientStartupLuaRequireBufferChunkLineNumbers(warning.Message))
            {
                generatedChunkLines.Add(lineNumber);
            }

            var nearTokens = ExtractClientStartupLuaNearTokens(warning.Message);
            foreach (var lineNumber in lineNumbers)
            {
                if (!warningsByLine.TryGetValue(lineNumber, out var lineWarnings))
                {
                    lineWarnings = [];
                    warningsByLine.Add(lineNumber, lineWarnings);
                }

                lineWarnings.Add(warning);
                if (nearTokens.Count > 0)
                {
                    if (!nearTokensByLine.TryGetValue(lineNumber, out var lineNearTokens))
                    {
                        lineNearTokens = new SortedSet<string>(StringComparer.Ordinal);
                        nearTokensByLine.Add(lineNumber, lineNearTokens);
                    }

                    foreach (var nearToken in nearTokens)
                    {
                        lineNearTokens.Add(nearToken);
                    }
                }
            }

            foreach (var sourceFrame in ExtractClientStartupLuaSourceFrames(warning.Message))
            {
                sourceFrames.Add(sourceFrame);
            }
        }

        if (warningsByLine.Count == 0)
        {
            return Array.Empty<ClientStartupLuaRequireBufferEvidence>();
        }

        var candidateMap = clientRoot is null
            ? new Dictionary<int, IReadOnlyList<ClientStartupLuaLineCandidate>>()
            : GetClientStartupLuaLineCandidatesByLine(clientRoot, warningsByLine.Keys);
        var sourceFrameList = sourceFrames.ToArray();

        return warningsByLine
            .Select(entry =>
            {
                var lineWarnings = entry.Value;
                var candidates = candidateMap.TryGetValue(entry.Key, out var lineCandidates)
                    ? lineCandidates
                    : Array.Empty<ClientStartupLuaLineCandidate>();
                var nearTokens = nearTokensByLine.TryGetValue(entry.Key, out var lineNearTokens)
                    ? lineNearTokens.ToArray()
                    : Array.Empty<string>();

                return new ClientStartupLuaRequireBufferEvidence(
                    entry.Key,
                    lineWarnings.Count,
                    lineWarnings.Min(warning => warning.LineNumber),
                    lineWarnings.Max(warning => warning.LineNumber),
                    sourceFrameList,
                    candidates,
                    nearTokens,
                    generatedChunkLines.Contains(entry.Key));
            })
            .ToArray();
    }

    static IReadOnlyList<int> ExtractClientStartupLuaRequireBufferLineNumbers(string message)
    {
        var lines = new SortedSet<int>();
        AddClientStartupLuaLineMatches(ClientStartupLuaRequireBufferChunkLineRegex, message, lines);
        AddClientStartupLuaLineMatches(ClientStartupLuaRequireBufferStackLineRegex, message, lines);
        return lines.ToArray();
    }

    static IReadOnlyList<int> ExtractClientStartupLuaRequireBufferChunkLineNumbers(string message)
    {
        var lines = new SortedSet<int>();
        AddClientStartupLuaLineMatches(ClientStartupLuaRequireBufferChunkLineRegex, message, lines);
        return lines.ToArray();
    }

    static void AddClientStartupLuaLineMatches(Regex regex, string message, ISet<int> lines)
    {
        foreach (Match match in regex.Matches(message))
        {
            if (int.TryParse(match.Groups["line"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var lineNumber) &&
                lineNumber > 0)
            {
                lines.Add(lineNumber);
            }
        }
    }

    static IReadOnlyList<string> ExtractClientStartupLuaSourceFrames(string message)
    {
        var frames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ClientStartupLuaSourceFrameRegex.Matches(message))
        {
            var path = NormalizeClientStartupPath(match.Groups["path"].Value);
            if (!IsRelativeClientStartupCandidate(path) ||
                !int.TryParse(match.Groups["line"].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var lineNumber) ||
                lineNumber <= 0)
            {
                continue;
            }

            frames.Add($"{path}:{lineNumber}");
        }

        return frames.ToArray();
    }

    static IReadOnlyList<string> ExtractClientStartupLuaNearTokens(string message)
    {
        var tokens = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match match in ClientStartupLuaNearTokenRegex.Matches(message))
        {
            var token = match.Groups["token"].Value.Trim().TrimEnd('.', ',', ';', ':');
            if (token.Length > 0)
            {
                tokens.Add($"near {token}");
            }
        }

        return tokens.ToArray();
    }

    static IReadOnlyDictionary<int, IReadOnlyList<ClientStartupLuaLineCandidate>> GetClientStartupLuaLineCandidatesByLine(
        string rootPath,
        IEnumerable<int> lineNumbers)
    {
        var requestedLines = lineNumbers
            .Where(lineNumber => lineNumber > 0)
            .Distinct()
            .Order()
            .ToArray();
        var candidateLines = requestedLines
            .Where(lineNumber => lineNumber >= MinClientStartupLuaCandidateLine)
            .ToArray();
        var result = requestedLines.ToDictionary(
            lineNumber => lineNumber,
            _ => new List<ClientStartupLuaLineCandidate>());
        if (candidateLines.Length == 0 || !Directory.Exists(rootPath))
        {
            return result.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<ClientStartupLuaLineCandidate>)entry.Value);
        }

        var highestRequestedLine = candidateLines[^1];
        foreach (var luaFile in EnumerateClientStartupLuaFiles(rootPath))
        {
            if (candidateLines.All(lineNumber => result[lineNumber].Count >= MaxClientStartupLuaLineCandidates))
            {
                break;
            }

            var countedLines = CountClientStartupLinesUpTo(luaFile, highestRequestedLine);
            if (countedLines <= 0)
            {
                continue;
            }

            var relativePath = NormalizeClientStartupPath(Path.GetRelativePath(rootPath, luaFile));
            if (!IsRelativeClientStartupCandidate(relativePath))
            {
                continue;
            }

            foreach (var lineNumber in candidateLines)
            {
                if (countedLines < lineNumber ||
                    result[lineNumber].Count >= MaxClientStartupLuaLineCandidates)
                {
                    continue;
                }

                result[lineNumber].Add(new ClientStartupLuaLineCandidate(relativePath, lineNumber));
            }
        }

        return result.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<ClientStartupLuaLineCandidate>)entry.Value
                .OrderBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    static IReadOnlyList<string> EnumerateClientStartupLuaFiles(string rootPath)
    {
        try
        {
            return Directory
                .EnumerateFiles(rootPath, "*.lua", SearchOption.AllDirectories)
                .Where(path => !IsClientStartupLuaCandidateExcluded(rootPath, path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Take(MaxClientStartupLuaFiles)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return Array.Empty<string>();
        }
    }

    static bool IsClientStartupLuaCandidateExcluded(string rootPath, string filePath)
    {
        var relativePath = Path.GetRelativePath(rootPath, filePath).Replace('\\', '/');
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return true;
        }

        return segments.Any(segment =>
            segment.StartsWith(".", StringComparison.Ordinal) ||
            string.Equals(segment, "ini.oldcompat", StringComparison.OrdinalIgnoreCase) ||
            segment.StartsWith("ini.oldcompat-", StringComparison.OrdinalIgnoreCase) ||
            segment.StartsWith("Env_DX", StringComparison.OrdinalIgnoreCase));
    }

    static int CountClientStartupLinesUpTo(string filePath, int maximumLineNumber)
    {
        var lineCount = 0;
        try
        {
            foreach (var _ in File.ReadLines(filePath))
            {
                lineCount++;
                if (lineCount >= maximumLineNumber)
                {
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }

        return lineCount;
    }

    static string FormatClientStartupLuaLineCandidate(ClientStartupLuaLineCandidate candidate)
    {
        return $"{candidate.Path}(lines>={candidate.MinimumLineCount})";
    }

    static string FormatClientStartupLuaGeneratedBufferHint(
        ClientStartupLuaRequireBufferEvidence evidence,
        bool checkedClientRoot)
    {
        if (!checkedClientRoot ||
            !evidence.HasGeneratedRequireBufferChunk ||
            evidence.ClientRootCandidates.Count > 0)
        {
            return string.Empty;
        }

        var nearText = evidence.NearTokens.Count == 0
            ? string.Empty
            : $"{string.Join(",", evidence.NearTokens)}; ";
        return $" hint={nearText}likely generated/decompressed Lua buffer rather than a plain root Lua file";
    }

    static void PrintClientStartupMissingFileProvenance(
        IReadOnlyList<ClientStartupWarning> warnings,
        ClientStartupAuditOptions options)
    {
        PrintClientStartupFileProvenance("Missing file provenance", warnings, ClientStartupWarningKind.MissingFile, options);
    }

    static void PrintClientStartupFileProvenance(
        string title,
        IReadOnlyList<ClientStartupWarning> warnings,
        ClientStartupWarningKind kind,
        ClientStartupAuditOptions options)
    {
        if (options.ClientRoot is null && options.ReferenceRoots.Count == 0)
        {
            return;
        }

        var missingFiles = warnings
            .Where(warning => warning.Kind == kind)
            .Select(warning => warning.FilePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (missingFiles.Length == 0)
        {
            return;
        }

        Console.WriteLine($"{title}:");
        foreach (var missingFile in missingFiles)
        {
            var candidates = GetClientStartupFileCandidates(missingFile);
            var clientMatches = options.ClientRoot is null
                ? Array.Empty<string>()
                : GetExistingClientStartupCandidatePaths(options.ClientRoot, candidates)
                    .ToArray();
            var referenceMatches = options.ReferenceRoots
                .SelectMany(referenceRoot => GetExistingClientStartupCandidatePaths(referenceRoot.RootPath, candidates)
                    .Select(candidate => $"{referenceRoot.Label}:{candidate}"))
                .ToArray();
            var candidateText = string.Join(",", candidates);
            var clientText = clientMatches.Length == 0
                ? "false"
                : "true";
            var referencesText = referenceMatches.Length == 0
                ? "none"
                : string.Join(",", referenceMatches.Select(match => match.Split(':', 2)[0]).Distinct(StringComparer.OrdinalIgnoreCase));
            var matchesText = referenceMatches.Length == 0
                ? "none"
                : string.Join(",", referenceMatches);

            Console.WriteLine(
                $"  {missingFile} clientRootPresent={clientText} references={referencesText} " +
                $"matches={matchesText} candidates={candidateText}");
        }
    }

    static void PrintClientStartupTextureProvenance(
        IReadOnlyList<ClientStartupWarning> warnings,
        ClientStartupAuditOptions options)
    {
        if (options.ClientRoot is null && options.ReferenceRoots.Count == 0)
        {
            return;
        }

        var textures = warnings
            .Where(warning => warning.Kind == ClientStartupWarningKind.MissingTexture)
            .Select(warning => warning.FilePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (textures.Length == 0)
        {
            return;
        }

        Console.WriteLine("Texture provenance:");
        foreach (var texture in textures)
        {
            if (!TryParseClientStartupTextureSubject(texture, out var textureId))
            {
                continue;
            }

            var baseCandidates = GetClientStartupTextureCandidates(textureId);
            var allCandidates = new SortedSet<string>(baseCandidates, StringComparer.OrdinalIgnoreCase);
            var allAniReferences = new List<string>();
            ClientStartupTextureEvidence? clientEvidence = null;
            if (options.ClientRoot is not null)
            {
                clientEvidence = GetClientStartupTextureEvidence(options.ClientRoot, textureId, baseCandidates);
                AddClientStartupTextureEvidence(allCandidates, allAniReferences, "clientRoot", clientEvidence);
            }

            var referenceEvidence = options.ReferenceRoots
                .Select(referenceRoot =>
                {
                    var evidence = GetClientStartupTextureEvidence(referenceRoot.RootPath, textureId, baseCandidates);
                    AddClientStartupTextureEvidence(allCandidates, allAniReferences, referenceRoot.Label, evidence);
                    return new
                    {
                        referenceRoot.Label,
                        Evidence = evidence,
                    };
                })
                .ToArray();

            var referenceMatches = referenceEvidence
                .Where(item => item.Evidence.ExistingCandidates.Count > 0)
                .ToArray();
            var clientText = clientEvidence is null
                ? "not-checked"
                : clientEvidence.ExistingCandidates.Count == 0
                    ? "false"
                    : "true";
            var referencesText = referenceMatches.Length == 0
                ? "none"
                : string.Join(",", referenceMatches.Select(match => match.Label));
            var matches = new List<string>();
            if (clientEvidence is not null)
            {
                matches.AddRange(clientEvidence.ExistingCandidates.Select(candidate => $"clientRoot:{candidate}"));
            }

            matches.AddRange(referenceMatches.SelectMany(match =>
                match.Evidence.ExistingCandidates.Select(candidate => $"{match.Label}:{candidate}")));
            var matchesText = matches.Count == 0
                ? "none"
                : string.Join(",", matches);
            var aniRefsText = allAniReferences.Count == 0
                ? "none"
                : string.Join(",", allAniReferences.Distinct(StringComparer.OrdinalIgnoreCase));

            Console.WriteLine(
                $"  {texture} clientRootPresent={clientText} references={referencesText} " +
                $"matches={matchesText} aniRefs={aniRefsText} candidates={string.Join(",", allCandidates)}");
        }
    }

    static void AddClientStartupTextureEvidence(
        ISet<string> allCandidates,
        List<string> allAniReferences,
        string label,
        ClientStartupTextureEvidence evidence)
    {
        foreach (var candidate in evidence.DiscoveredCandidates)
        {
            allCandidates.Add(candidate);
        }

        allAniReferences.AddRange(evidence.AniReferences.Select(reference => $"{label}:{reference}"));
    }

    static ClientStartupTextureEvidence GetClientStartupTextureEvidence(
        string rootPath,
        string textureId,
        IReadOnlyList<string> baseCandidates)
    {
        var aniReferences = GetClientStartupTextureAniReferences(rootPath, textureId);
        var discoveredCandidates = aniReferences
            .SelectMany(reference => reference.Candidates)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var candidates = baseCandidates
            .Concat(discoveredCandidates)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existingCandidates = GetExistingClientStartupCandidatePaths(rootPath, candidates)
            .ToArray();

        return new ClientStartupTextureEvidence(
            existingCandidates,
            aniReferences
                .Select(reference => reference.AniPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            discoveredCandidates);
    }

    static IReadOnlyList<ClientStartupTextureAniReference> GetClientStartupTextureAniReferences(
        string rootPath,
        string textureId)
    {
        var aniRoot = Path.Combine(rootPath, "ani");
        if (!Directory.Exists(aniRoot))
        {
            return Array.Empty<ClientStartupTextureAniReference>();
        }

        string[] aniFiles;
        try
        {
            aniFiles = Directory
                .EnumerateFiles(aniRoot, "*.ani", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Take(MaxClientStartupTextureAniFiles)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<ClientStartupTextureAniReference>();
        }

        var references = new List<ClientStartupTextureAniReference>();
        foreach (var aniFile in aniFiles)
        {
            if (references.Count >= MaxClientStartupTextureAniReferences)
            {
                break;
            }

            var candidates = GetClientStartupTextureCandidatesFromAniFile(aniFile, textureId);
            if (candidates.Count == 0)
            {
                continue;
            }

            references.Add(new ClientStartupTextureAniReference(
                NormalizeClientStartupPath(Path.GetRelativePath(rootPath, aniFile)),
                candidates));
        }

        return references;
    }

    static IReadOnlyList<string> GetClientStartupTextureCandidatesFromAniFile(string aniFile, string textureId)
    {
        try
        {
            var fileInfo = new FileInfo(aniFile);
            if (fileInfo.Length > MaxClientStartupTextureAniBytesPerFile)
            {
                return Array.Empty<string>();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }

        var candidates = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var foundTextureId = false;
        try
        {
            var lineCount = 0;
            foreach (var line in File.ReadLines(aniFile))
            {
                lineCount++;
                if (lineCount > MaxClientStartupTextureAniLinesPerFile)
                {
                    break;
                }

                if (!ContainsClientStartupTextureId(line, textureId))
                {
                    continue;
                }

                foundTextureId = true;
                foreach (var candidate in ExtractClientStartupTexturePathCandidates(line))
                {
                    candidates.Add(candidate);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }

        if (foundTextureId && candidates.Count == 0)
        {
            candidates.Add(GetDefaultClientStartupTextureCandidate(textureId));
        }

        return candidates.ToArray();
    }

    static bool ContainsClientStartupTextureId(string line, string textureId)
    {
        var index = line.IndexOf(textureId, StringComparison.Ordinal);
        while (index >= 0)
        {
            var beforeIsDigit = index > 0 && char.IsDigit(line[index - 1]);
            var afterIndex = index + textureId.Length;
            var afterIsDigit = afterIndex < line.Length && char.IsDigit(line[afterIndex]);
            if (!beforeIsDigit && !afterIsDigit)
            {
                return true;
            }

            index = line.IndexOf(textureId, index + textureId.Length, StringComparison.Ordinal);
        }

        return false;
    }

    static IEnumerable<string> ExtractClientStartupTexturePathCandidates(string line)
    {
        foreach (Match match in ClientStartupTexturePathRegex.Matches(line))
        {
            var candidate = NormalizeClientStartupPath(match.Groups["path"].Value);
            if (IsRelativeClientStartupCandidate(candidate))
            {
                yield return candidate;
            }
        }
    }

    static void PrintClientStartupAniProvenance(
        IReadOnlyList<ClientStartupWarning> warnings,
        ClientStartupAuditOptions options)
    {
        if (options.ClientRoot is null && options.ReferenceRoots.Count == 0)
        {
            return;
        }

        var aniWarnings = warnings
            .Where(warning => warning.Kind == ClientStartupWarningKind.AniWarning)
            .Select(warning => warning.FilePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (aniWarnings.Length == 0)
        {
            return;
        }

        Console.WriteLine("ANI provenance:");
        foreach (var aniWarning in aniWarnings)
        {
            if (!TryParseClientStartupAniSubject(aniWarning, out var aniPath, out var aniIndex))
            {
                continue;
            }

            ClientStartupAniPresence? clientPresence = null;
            if (options.ClientRoot is not null)
            {
                clientPresence = GetClientStartupAniPresence(options.ClientRoot, aniPath, aniIndex);
            }

            var referencePresence = options.ReferenceRoots
                .Select(referenceRoot => new
                {
                    referenceRoot.Label,
                    Presence = GetClientStartupAniPresence(referenceRoot.RootPath, aniPath, aniIndex),
                })
                .ToArray();
            var referenceMatches = referencePresence
                .Where(item => item.Presence.FileExists)
                .ToArray();
            var matches = new List<string>();
            if (clientPresence is not null && clientPresence.FileExists)
            {
                matches.Add($"clientRoot:{FormatClientStartupAniMatch(aniPath, aniIndex, clientPresence.IndexExists)}");
            }

            matches.AddRange(referenceMatches.Select(match =>
                $"{match.Label}:{FormatClientStartupAniMatch(aniPath, aniIndex, match.Presence.IndexExists)}"));
            var referencesText = referenceMatches.Length == 0
                ? "none"
                : string.Join(",", referenceMatches.Select(match => $"{match.Label}:{FormatClientStartupAniStatus(match.Presence)}"));
            var clientText = clientPresence is null
                ? "not-checked"
                : FormatClientStartupAniStatus(clientPresence);

            Console.WriteLine(
                $"  {aniWarning} clientRoot={clientText} references={referencesText} " +
                $"matches={(matches.Count == 0 ? "none" : string.Join(",", matches))}");
        }
    }

    static ClientStartupAniPresence GetClientStartupAniPresence(string rootPath, string aniPath, string aniIndex)
    {
        var fullPath = Path.Combine(rootPath, aniPath.Replace('/', Path.DirectorySeparatorChar));
        if (!IsRelativeClientStartupCandidate(aniPath) || !File.Exists(fullPath))
        {
            return new ClientStartupAniPresence(false, false);
        }

        try
        {
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length > MaxClientStartupTextureAniBytesPerFile)
            {
                return new ClientStartupAniPresence(true, false);
            }

            var lineCount = 0;
            foreach (var line in File.ReadLines(fullPath))
            {
                lineCount++;
                if (lineCount > MaxClientStartupTextureAniLinesPerFile)
                {
                    break;
                }

                if (IsClientStartupAniIndexLine(line, aniIndex))
                {
                    return new ClientStartupAniPresence(true, true);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ClientStartupAniPresence(true, false);
        }

        return new ClientStartupAniPresence(true, false);
    }

    static string FormatClientStartupAniMatch(string aniPath, string aniIndex, bool indexExists)
    {
        return indexExists
            ? $"{aniPath}#{aniIndex}"
            : aniPath;
    }

    static string FormatClientStartupAniStatus(ClientStartupAniPresence presence)
    {
        if (!presence.FileExists)
        {
            return "missing";
        }

        return presence.IndexExists
            ? "file+index"
            : "file-only";
    }

    static string FormatClientStartupTextEntryStatus(ClientStartupTextEntryPresence presence)
    {
        if (!presence.FileExists)
        {
            return "missing";
        }

        return presence.EntryExists
            ? "file+entry"
            : "file-only";
    }

    static IReadOnlyList<string> GetClientStartupFileCandidates(string filePath)
    {
        var normalized = NormalizeClientStartupPath(filePath);
        var candidates = new List<string> { normalized };
        const string iniPrefix = "ini/";
        const string luaSuffix = ".lua";
        if (normalized.StartsWith(iniPrefix, StringComparison.OrdinalIgnoreCase) &&
            normalized.EndsWith(luaSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileNameWithoutExtension(normalized);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                candidates.Add($"ini/luacfg/{fileName}.dat");
            }
        }

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    static IReadOnlyList<string> GetClientStartupTextureCandidates(string textureId)
    {
        return [GetDefaultClientStartupTextureCandidate(textureId)];
    }

    static string GetDefaultClientStartupTextureCandidate(string textureId)
    {
        return $"c3/texture/{textureId}.dds";
    }

    static IEnumerable<string> GetExistingClientStartupCandidatePaths(string rootPath, IReadOnlyList<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!IsRelativeClientStartupCandidate(candidate))
            {
                continue;
            }

            var fullPath = Path.Combine(rootPath, candidate.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
            {
                yield return candidate;
            }
        }
    }

    static bool IsRelativeClientStartupCandidate(string value)
    {
        if (value.Length == 0 ||
            value.StartsWith("/", StringComparison.Ordinal) ||
            value.StartsWith("\\", StringComparison.Ordinal) ||
            (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':'))
        {
            return false;
        }

        var pathParts = value.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (pathParts.Any(part => string.Equals(part, "..", StringComparison.Ordinal)))
        {
            return false;
        }

        return !Path.IsPathRooted(value.Replace('/', Path.DirectorySeparatorChar));
    }

    static string FormatClientStartupTextureSubject(string textureId)
    {
        return $"texture:{textureId}";
    }

    static bool TryParseClientStartupTextureSubject(string subject, out string textureId)
    {
        const string prefix = "texture:";
        if (!subject.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            textureId = string.Empty;
            return false;
        }

        textureId = subject[prefix.Length..];
        return textureId.Length > 0;
    }

    static string FormatClientStartupAniSubject(string aniPath, string aniIndex)
    {
        return $"{aniPath}#{aniIndex}";
    }

    static bool TryParseClientStartupAniSubject(string subject, out string aniPath, out string aniIndex)
    {
        var separator = subject.LastIndexOf('#');
        if (separator <= 0 || separator == subject.Length - 1)
        {
            aniPath = string.Empty;
            aniIndex = string.Empty;
            return false;
        }

        aniPath = subject[..separator];
        aniIndex = subject[(separator + 1)..];
        return aniPath.Length > 0 && aniIndex.Length > 0;
    }

    static string NormalizeClientStartupAniIndex(string value)
    {
        return value.Trim().Trim('"', '\'', '.', ',', ';', ':');
    }

    static bool IsClientStartupAniIndexLine(string line, string aniIndex)
    {
        var trimmed = line.Trim();
        return trimmed.Length == aniIndex.Length + 2 &&
            trimmed[0] == '[' &&
            trimmed[^1] == ']' &&
            string.Equals(trimmed[1..^1], aniIndex, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record ClientStartupAuditOptions(
    string LogPath,
    bool IncludeAllGroups,
    int TopCount,
    TimeSpan GroupGap,
    string? ClientRoot,
    IReadOnlyList<ClientStartupReferenceRoot> ReferenceRoots);

internal sealed record ClientStartupReferenceRoot(
    string Label,
    string RootPath);

internal sealed record ClientStartupTextureEvidence(
    IReadOnlyList<string> ExistingCandidates,
    IReadOnlyList<string> AniReferences,
    IReadOnlyList<string> DiscoveredCandidates);

internal sealed record ClientStartupTextureAniReference(
    string AniPath,
    IReadOnlyList<string> Candidates);

internal sealed record ClientStartupLuaRequireBufferEvidence(
    int LineNumber,
    int Occurrences,
    int FirstLogLine,
    int LastLogLine,
    IReadOnlyList<string> SourceFrames,
    IReadOnlyList<ClientStartupLuaLineCandidate> ClientRootCandidates,
    IReadOnlyList<string> NearTokens,
    bool HasGeneratedRequireBufferChunk);

internal sealed record ClientStartupLuaLineCandidate(
    string Path,
    int MinimumLineCount);

internal sealed record ClientStartupAniPresence(
    bool FileExists,
    bool IndexExists);

internal sealed record ClientStartupTextEntryPresence(
    bool FileExists,
    bool EntryExists);

internal sealed record ClientStartupAuditReport(
    string LogPath,
    int LinesScanned,
    IReadOnlyList<ClientStartupWarningGroup> Groups)
{
    public int WarningCount => Groups.Sum(group => group.Warnings.Count);
}

internal sealed record ClientStartupWarningGroup(
    int Index,
    int FirstLineNumber,
    int LastLineNumber,
    TimeSpan? StartedAt,
    TimeSpan? EndedAt,
    IReadOnlyList<ClientStartupWarning> Warnings);

internal sealed record ClientStartupWarning(
    ClientStartupWarningKind Kind,
    string FilePath,
    string Message,
    int LineNumber,
    TimeSpan? Timestamp);

internal enum ClientStartupWarningKind
{
    LuaError,
    MissingFile,
    ErrorData,
    Download,
    InvalidIdent,
    MissingTexture,
    AniWarning,
    FontRendererFailure,
    NamedPipeFailure,
}

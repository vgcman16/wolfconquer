using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

internal static partial class Program
{
    private const string ClientTextureWarningOwnerAuditUsage =
        "Usage: WolfConquer.DataTool client-texture-warning-owner-audit <clientDebugLogPath> <clientRoot> [--since-line N] [--texture id] [...] [--top N]";

    static int ClientTextureWarningOwnerAudit(string[] args)
    {
        if (!TryParseClientTextureWarningOwnerAuditOptions(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(ClientTextureWarningOwnerAuditUsage);
            return 1;
        }

        ClientTextureWarningOwnerAuditReport report;
        try
        {
            report = ClientTextureWarningOwnerAuditor.Audit(options);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not audit texture warning owners: {ex.Message}");
            Console.Error.WriteLine(ClientTextureWarningOwnerAuditUsage);
            return 1;
        }

        Console.WriteLine("Client texture warning owner audit:");
        Console.WriteLine($"Client log: {report.ClientLogPath}");
        Console.WriteLine($"Client root: {report.ClientRoot}");
        Console.WriteLine($"Since line: {report.SinceLine?.ToString(CultureInfo.InvariantCulture) ?? "<none>"}");
        Console.WriteLine($"Texture filter: {FormatClientTextureFilter(report.TextureFilter)}");
        Console.WriteLine($"Warnings: total={report.TotalWarningCount} uniqueIds={report.UniqueWarningIdCount} reportedIds={report.Items.Count}");
        Console.WriteLine($"Text files: scanned={report.ScannedTextFiles} skipped={report.SkippedFiles}");

        if (report.Items.Count == 0)
        {
            Console.WriteLine("Texture ids: <none>");
            return 0;
        }

        Console.WriteLine("Texture ids:");
        foreach (var item in report.Items)
        {
            Console.WriteLine($"  texture {item.Id} logCount={item.LogCount}");
            if (item.Owners.Count == 0)
            {
                Console.WriteLine("    <no text owner found>");
                continue;
            }

            foreach (var owner in item.Owners)
            {
                Console.WriteLine(
                    $"    {owner.RelativePath} hits={owner.HitCount} firstLines={string.Join(", ", owner.FirstLines)}");
                foreach (var asset in owner.AssetReferences)
                {
                    Console.WriteLine(
                        $"      asset line={asset.LineNumber} path={asset.RelativePath} status={(asset.Exists ? "exists" : "missing")}");
                }
            }

            if (item.TotalOwnerCount > item.Owners.Count)
            {
                Console.WriteLine($"    Additional owners omitted: {item.TotalOwnerCount - item.Owners.Count}");
            }
        }

        return 0;
    }

    private static bool TryParseClientTextureWarningOwnerAuditOptions(
        string[] args,
        out ClientTextureWarningOwnerAuditOptions options,
        out string error)
    {
        options = new ClientTextureWarningOwnerAuditOptions("", "", null, [], 50);
        error = "";

        if (args.Length < 3)
        {
            error = "Missing client debug log path or client root.";
            return false;
        }

        var clientLogPath = Path.GetFullPath(args[1]);
        var clientRoot = Path.GetFullPath(args[2]);
        int? sinceLine = null;
        var textureIds = new SortedSet<string>(StringComparer.Ordinal);
        var top = 50;

        for (var index = 3; index < args.Length; index++)
        {
            var argument = args[index];
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

            if (TryReadOptionValue(args, ref index, "--texture", argument, out var textureId))
            {
                if (!IsNumericTextureId(textureId))
                {
                    error = $"Invalid --texture value: {textureId}";
                    return false;
                }

                textureIds.Add(textureId);
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

        options = new ClientTextureWarningOwnerAuditOptions(clientLogPath, clientRoot, sinceLine, textureIds.ToArray(), top);
        return true;
    }

    private static bool IsNumericTextureId(string value)
    {
        return value.Length > 0 && value.All(static character => character is >= '0' and <= '9');
    }

    private static string FormatClientTextureFilter(IReadOnlyCollection<string> textureIds)
    {
        return textureIds.Count == 0
            ? "<all>"
            : string.Join(", ", textureIds);
    }
}

internal sealed record ClientTextureWarningOwnerAuditOptions(
    string ClientLogPath,
    string ClientRoot,
    int? SinceLine,
    IReadOnlyCollection<string> TextureIds,
    int Top);

internal sealed record ClientTextureWarningOwnerAuditReport(
    string ClientLogPath,
    string ClientRoot,
    int? SinceLine,
    IReadOnlyCollection<string> TextureFilter,
    int TotalWarningCount,
    int UniqueWarningIdCount,
    int ScannedTextFiles,
    int SkippedFiles,
    IReadOnlyList<ClientTextureWarningOwnerItem> Items);

internal sealed record ClientTextureWarningOwnerItem(
    string Id,
    int LogCount,
    int TotalOwnerCount,
    IReadOnlyList<ClientTextureWarningTextOwner> Owners);

internal sealed record ClientTextureWarningTextOwner(
    string RelativePath,
    int HitCount,
    IReadOnlyList<int> FirstLines,
    IReadOnlyList<ClientTextureWarningAssetReference> AssetReferences);

internal sealed record ClientTextureWarningAssetReference(
    int LineNumber,
    string RelativePath,
    bool Exists);

internal static class ClientTextureWarningOwnerAuditor
{
    private const long MaxTextFileBytes = 5 * 1024 * 1024;
    private const int FirstLineLimit = 5;
    private const int AssetReferenceLimit = 5;
    private const int AssetReferenceContextRadius = 4;
    private const int OwnerReportLimit = 20;

    private static readonly Regex MissingTexturePattern = new(
        @"texture\s*\[\s*(?<id>\d+)\s*\]\s*not\s*found",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex AssetPathPattern = new(
        @"(?<path>(?:[A-Za-z]:)?[^\s""'<>|=]+[\\/][^\s""'<>|=]+\.(?:dds|png|jpg|jpeg|bmp|tga))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> IncludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ini",
        ".ani",
        ".lua",
        ".txt",
        ".dat",
    };

    public static ClientTextureWarningOwnerAuditReport Audit(ClientTextureWarningOwnerAuditOptions options)
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
        var textureFilter = new HashSet<string>(options.TextureIds, StringComparer.Ordinal);
        var counters = ReadWarningCounters(fullLogPath, options.SinceLine, textureFilter);
        var topIds = counters
            .OrderByDescending(static pair => pair.Value)
            .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
            .Take(options.Top)
            .Select(static pair => pair.Key)
            .ToArray();

        var ownerMap = topIds.ToDictionary(static id => id, static _ => new List<ClientTextureWarningTextOwner>(), StringComparer.Ordinal);
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
                var relativePath = FormatRelativePath(fullClientRoot, path);
                foreach (var owner in FindOwners(fullClientRoot, relativePath, text, topIds))
                {
                    ownerMap[owner.Id].Add(owner.Owner);
                }
            }
        }

        var items = topIds
            .Select(id =>
            {
                var sortedOwners = ownerMap[id]
                    .OrderByDescending(static owner => owner.HitCount)
                    .ThenBy(static owner => owner.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new ClientTextureWarningOwnerItem(
                    id,
                    counters[id],
                    sortedOwners.Length,
                    sortedOwners.Take(OwnerReportLimit).ToArray());
            })
            .ToArray();

        return new ClientTextureWarningOwnerAuditReport(
            fullLogPath,
            fullClientRoot,
            options.SinceLine,
            options.TextureIds.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            counters.Values.Sum(),
            counters.Count,
            scannedTextFiles,
            skippedFiles,
            items);
    }

    private static SortedDictionary<string, int> ReadWarningCounters(
        string path,
        int? sinceLine,
        IReadOnlySet<string> textureFilter)
    {
        var counters = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var lineNumber = 0;

        foreach (var line in ReadSharedLogLines(path))
        {
            lineNumber++;
            if (sinceLine is not null && lineNumber < sinceLine.Value)
            {
                continue;
            }

            var match = MissingTexturePattern.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var id = match.Groups["id"].Value;
            if (textureFilter.Count > 0 && !textureFilter.Contains(id))
            {
                continue;
            }

            counters[id] = counters.TryGetValue(id, out var count) ? count + 1 : 1;
        }

        return counters;
    }

    private static IEnumerable<string> EnumerateCandidateFiles(string clientRoot)
    {
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
        };

        return Directory
            .EnumerateFiles(clientRoot, "*", enumerationOptions)
            .Where(static path => IncludedExtensions.Contains(Path.GetExtension(path)))
            .Where(path => !IsSkippedCandidatePath(clientRoot, path))
            .OrderBy(path => FormatRelativePath(clientRoot, path), StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsSkippedCandidatePath(string clientRoot, string path)
    {
        var relative = FormatRelativePath(clientRoot, path);
        return relative.StartsWith("debug/", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("Log/", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("Chrome/", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("Env_DX8/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadCandidateText(string path, out string text)
    {
        text = "";
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxTextFileBytes)
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
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return false;
    }

    private static IEnumerable<(string Id, ClientTextureWarningTextOwner Owner)> FindOwners(
        string clientRoot,
        string relativePath,
        string text,
        IReadOnlyList<string> ids)
    {
        var lines = SplitLines(text).ToArray();
        var collectors = ids.ToDictionary(static id => id, static _ => new TextureOwnerCollector(), StringComparer.Ordinal);

        for (var index = 0; index < lines.Length; index++)
        {
            foreach (var id in ids)
            {
                if (!ContainsTextureOwnerToken(lines[index], id))
                {
                    continue;
                }

                collectors[id].RecordLine(index + 1);
                RecordNearbyAssetReferences(collectors[id], clientRoot, lines, index, id);
            }
        }

        foreach (var pair in collectors.Where(static pair => pair.Value.HitCount > 0))
        {
            yield return (pair.Key, new ClientTextureWarningTextOwner(
                relativePath,
                pair.Value.HitCount,
                pair.Value.FirstLines,
                pair.Value.AssetReferences));
        }
    }

    private static void RecordNearbyAssetReferences(
        TextureOwnerCollector collector,
        string clientRoot,
        IReadOnlyList<string> lines,
        int matchIndex,
        string id)
    {
        var firstIndex = Math.Max(0, matchIndex - AssetReferenceContextRadius);
        var lastIndex = Math.Min(lines.Count - 1, matchIndex + AssetReferenceContextRadius);
        var references = new List<(ClientTextureWarningAssetReference Reference, bool PathContainsId, int Distance)>();

        for (var index = firstIndex; index <= lastIndex; index++)
        {
            var lineNumber = index + 1;
            foreach (var assetPath in ExtractAssetPaths(lines[index]))
            {
                references.Add((
                    CreateAssetReference(clientRoot, lineNumber, assetPath),
                    AssetPathMatchesTextureId(assetPath, id),
                    Math.Abs(index - matchIndex)));
            }
        }

        var anyPathContainsId = references.Any(static item => item.PathContainsId);
        foreach (var reference in references
            .Where(item => !anyPathContainsId || item.PathContainsId)
            .OrderBy(static item => item.Distance)
            .ThenBy(static item => item.Reference.LineNumber)
            .ThenBy(static item => item.Reference.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(static item => item.Reference))
        {
            collector.RecordAssetReference(reference);
        }
    }

    private static IEnumerable<string> ExtractAssetPaths(string line)
    {
        foreach (Match match in AssetPathPattern.Matches(line))
        {
            var path = NormalizeRelativePath(match.Groups["path"].Value);
            if (path.Length > 0)
            {
                yield return path;
            }
        }
    }

    private static ClientTextureWarningAssetReference CreateAssetReference(
        string clientRoot,
        int lineNumber,
        string relativePath)
    {
        var exists = false;
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(
                clientRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var normalizedRoot = Path.GetFullPath(clientRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            exists =
                (fullPath.Length == normalizedRoot.Length ||
                    fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) &&
                File.Exists(fullPath);
        }
        catch (ArgumentException)
        {
        }

        return new ClientTextureWarningAssetReference(lineNumber, relativePath, exists);
    }

    private static bool ContainsTextureOwnerToken(string line, string id)
    {
        return ContainsDelimitedTextureId(line, id) ||
            ExtractAssetPaths(line).Any(assetPath => AssetPathMatchesTextureId(assetPath, id));
    }

    private static bool AssetPathMatchesTextureId(string path, string id)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        return fileName.EndsWith(id, StringComparison.Ordinal) ||
            ContainsDelimitedTextureId(path, id);
    }

    private static bool ContainsDelimitedTextureId(string line, string id)
    {
        var index = line.IndexOf(id, StringComparison.Ordinal);
        while (index >= 0)
        {
            var beforeOk = index == 0 || !char.IsDigit(line[index - 1]);
            var afterIndex = index + id.Length;
            var afterOk = afterIndex >= line.Length || !char.IsDigit(line[afterIndex]);
            if (beforeOk && afterOk)
            {
                return true;
            }

            index = line.IndexOf(id, index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static string FormatRelativePath(string root, string path)
    {
        return NormalizeRelativePath(Path.GetRelativePath(root, path));
    }

    private static string NormalizeRelativePath(string path)
    {
        path = path.Trim().Trim('"', '\'');
        path = path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        while (path.StartsWith("./", StringComparison.Ordinal))
        {
            path = path[2..];
        }

        return path.TrimStart('/', '\\');
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

    private sealed class TextureOwnerCollector
    {
        private readonly List<int> firstLines = [];
        private readonly List<ClientTextureWarningAssetReference> assetReferences = [];
        private readonly HashSet<string> seenAssetReferences = new(StringComparer.OrdinalIgnoreCase);

        public int HitCount { get; private set; }

        public IReadOnlyList<int> FirstLines => firstLines;

        public IReadOnlyList<ClientTextureWarningAssetReference> AssetReferences => assetReferences;

        public void RecordLine(int lineNumber)
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

        public void RecordAssetReference(ClientTextureWarningAssetReference reference)
        {
            if (assetReferences.Count >= AssetReferenceLimit)
            {
                return;
            }

            var key = reference.LineNumber.ToString(CultureInfo.InvariantCulture) + "|" + reference.RelativePath;
            if (seenAssetReferences.Add(key))
            {
                assetReferences.Add(reference);
            }
        }
    }
}

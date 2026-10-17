using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;

internal static partial class Program
{
    private const string ClientOverlayItemRefAuditUsage =
        "Usage: WolfConquer.DataTool client-overlay-item-ref-audit <clientRoot> <baselineClientRoot|knownItemSourcePath> <relativeSideTablePath> [relativeSideTablePath ...]";

    static int ClientOverlayItemRefAudit(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine(ClientOverlayItemRefAuditUsage);
            return 1;
        }

        var clientRoot = Path.GetFullPath(args[1]);
        if (!Directory.Exists(clientRoot))
        {
            Console.Error.WriteLine($"Client path not found: {clientRoot}");
            return 1;
        }

        ClientOverlayItemRefAuditReport report;
        try
        {
            report = ClientOverlayItemRefAuditor.Audit(
                clientRoot,
                args[2],
                args.Skip(3).ToArray());
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or DecoderFallbackException or FormatException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Could not audit client overlay item references: {ex.Message}");
            return 1;
        }

        Console.WriteLine("Client overlay item ref audit:");
        Console.WriteLine($"Client: {report.ClientRoot}");
        Console.WriteLine($"Known item source: {report.KnownItemSourcePath}");
        Console.WriteLine($"Known item ids: {report.KnownItemIds}");
        Console.WriteLine($"Side-table files: {report.Files.Count}");
        Console.WriteLine($"Files with missing item ids: {report.Files.Count(static file => file.MissingItemIds.Count > 0)}");
        Console.WriteLine($"Unique missing item ids: {report.UniqueMissingItemIds.Count}");

        foreach (var file in report.Files)
        {
            Console.WriteLine($"{file.RelativePath}: uniqueRefs={file.UniqueReferencedItemIds.Count} missing={file.MissingItemIds.Count}");
            foreach (var missing in file.MissingItemIds)
            {
                Console.WriteLine($"  missing {missing.ItemId} lines={string.Join(',', missing.Lines)}");
            }
        }

        if (report.UniqueMissingItemIds.Count == 0)
        {
            Console.WriteLine("No missing item ids found.");
            return 0;
        }

        return 2;
    }
}

internal sealed record ClientOverlayItemRefAuditReport(
    string ClientRoot,
    string KnownItemSourcePath,
    int KnownItemIds,
    IReadOnlyList<ClientOverlayItemRefFileAudit> Files)
{
    public IReadOnlyList<int> UniqueMissingItemIds { get; } = Files
        .SelectMany(static file => file.MissingItemIds.Select(static missing => missing.ItemId))
        .Distinct()
        .Order()
        .ToArray();
}

internal sealed record ClientOverlayItemRefFileAudit(
    string RelativePath,
    IReadOnlyList<int> UniqueReferencedItemIds,
    IReadOnlyList<ClientOverlayMissingItemRef> MissingItemIds);

internal sealed record ClientOverlayMissingItemRef(int ItemId, IReadOnlyList<int> Lines);

internal static partial class ClientOverlayItemRefAuditor
{
    private static readonly Regex NumericTokenPattern = new(
        @"(?<!\d)(?<id>\d{5,9})(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BracketedItemPattern = new(
        @"\[\s*item\s*,?\s*(?<id>\d{1,9})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex NamedItemIdPattern = new(
        @"\b(?:itemtypeid|itemtype|itemid|item_type_id)\b[^\d\r\n]{0,24}(?<id>\d{1,9})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LeadingItemIdPattern = new(
        @"^\s*(?<id>\d{1,9})(?:\s|,|;|@@|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ClientOverlayItemRefAuditReport Audit(
        string clientRoot,
        string knownItemSourcePathOrDbServerPath,
        IReadOnlyCollection<string> relativeSideTablePaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(knownItemSourcePathOrDbServerPath);
        ArgumentNullException.ThrowIfNull(relativeSideTablePaths);

        if (relativeSideTablePaths.Count == 0)
        {
            throw new ArgumentException("At least one relative side-table path is required.", nameof(relativeSideTablePaths));
        }

        var fullClientRoot = RequireDirectory(clientRoot, "Client root");
        var knownItemSourcePath = ResolveKnownItemSourcePath(knownItemSourcePathOrDbServerPath);
        var knownItemIds = LoadKnownItemIds(knownItemSourcePath);
        if (knownItemIds.Count == 0)
        {
            throw new InvalidOperationException(
                $"No known item ids could be parsed from {knownItemSourcePath}. Provide a plaintext/decoded itemtype source or a simple item id list.");
        }

        var files = relativeSideTablePaths
            .Select(NormalizeSideTableRelativePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(relativePath => AuditSideTableFile(fullClientRoot, relativePath, knownItemIds))
            .ToArray();

        return new ClientOverlayItemRefAuditReport(
            fullClientRoot,
            knownItemSourcePath,
            knownItemIds.Count,
            files);
    }

    private static ClientOverlayItemRefFileAudit AuditSideTableFile(
        string clientRoot,
        string relativePath,
        IReadOnlySet<int> knownItemIds)
    {
        var filePath = ResolveClientRelativeFilePath(clientRoot, relativePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Side-table file not found: {filePath}", filePath);
        }

        var referencesByItemId = new SortedDictionary<int, SortedSet<int>>();
        var lineNumber = 0;
        foreach (var line in SplitLines(ReadTextFile(filePath)))
        {
            lineNumber++;
            foreach (var itemId in ExtractCandidateItemIds(line))
            {
                if (!referencesByItemId.TryGetValue(itemId, out var lines))
                {
                    lines = [];
                    referencesByItemId.Add(itemId, lines);
                }

                lines.Add(lineNumber);
            }
        }

        var missing = referencesByItemId
            .Where(entry => !knownItemIds.Contains(entry.Key))
            .Select(entry => new ClientOverlayMissingItemRef(entry.Key, entry.Value.ToArray()))
            .ToArray();

        return new ClientOverlayItemRefFileAudit(
            relativePath,
            referencesByItemId.Keys.ToArray(),
            missing);
    }

    private static IReadOnlySet<int> LoadKnownItemIds(string knownItemSourcePath)
    {
        var itemIds = new SortedSet<int>();
        foreach (var line in SplitLines(ReadTextFile(knownItemSourcePath)))
        {
            var trimmed = line.Trim();
            if (IsSkippedLine(trimmed))
            {
                continue;
            }

            if (TryParseItemTypeRowId(trimmed, out var itemTypeId) ||
                TryParseLeadingItemId(trimmed, out itemTypeId))
            {
                itemIds.Add(itemTypeId);
            }
        }

        return itemIds;
    }

    private static IEnumerable<int> ExtractCandidateItemIds(string line)
    {
        var trimmed = line.Trim();
        if (IsSkippedLine(trimmed))
        {
            yield break;
        }

        var itemIds = new SortedSet<int>();
        foreach (Match match in BracketedItemPattern.Matches(line))
        {
            if (TryParsePositiveInt(match.Groups["id"].Value, out var itemId))
            {
                itemIds.Add(itemId);
            }
        }

        foreach (Match match in NamedItemIdPattern.Matches(line))
        {
            if (TryParsePositiveInt(match.Groups["id"].Value, out var itemId))
            {
                itemIds.Add(itemId);
            }
        }

        if (IsItemReferenceHintLine(line))
        {
            foreach (Match match in NumericTokenPattern.Matches(line))
            {
                if (TryParsePositiveInt(match.Groups["id"].Value, out var itemId))
                {
                    itemIds.Add(itemId);
                }
            }
        }

        foreach (var itemId in itemIds)
        {
            yield return itemId;
        }
    }

    private static bool IsItemReferenceHintLine(string line)
    {
        if (BracketedItemPattern.IsMatch(line) || NamedItemIdPattern.IsMatch(line))
        {
            return true;
        }

        var key = ExtractKey(line);
        if (key.Length == 0)
        {
            return false;
        }

        var lowerKey = key.ToLowerInvariant();
        if (lowerKey.Contains("time", StringComparison.Ordinal) ||
            lowerKey.Contains("date", StringComparison.Ordinal))
        {
            return false;
        }

        return lowerKey.Contains("item", StringComparison.Ordinal) ||
            lowerKey.Contains("goods", StringComparison.Ordinal) ||
            lowerKey.Contains("material", StringComparison.Ordinal) ||
            lowerKey.StartsWith("prize", StringComparison.Ordinal) ||
            lowerKey.StartsWith("reward", StringComparison.Ordinal) ||
            lowerKey.StartsWith("award", StringComparison.Ordinal) ||
            lowerKey.StartsWith("bonus", StringComparison.Ordinal) ||
            lowerKey.StartsWith("drop", StringComparison.Ordinal) ||
            lowerKey.StartsWith("consume", StringComparison.Ordinal) ||
            lowerKey.StartsWith("cost", StringComparison.Ordinal) ||
            lowerKey.StartsWith("need", StringComparison.Ordinal);
    }

    private static string ExtractKey(string line)
    {
        var separatorIndex = line.IndexOf('=');
        if (separatorIndex < 0)
        {
            separatorIndex = line.IndexOf(':');
        }

        return separatorIndex > 0 ? line[..separatorIndex].Trim() : string.Empty;
    }

    private static bool TryParseItemTypeRowId(string line, out int itemTypeId)
    {
        itemTypeId = 0;
        if (!line.Contains("@@", StringComparison.Ordinal))
        {
            return false;
        }

        var fields = AtAtTableParser.SplitRow(line);
        return fields.Count >= 2 && TryParsePositiveInt(fields[0].Trim(), out itemTypeId);
    }

    private static bool TryParseLeadingItemId(string line, out int itemId)
    {
        itemId = 0;
        var match = LeadingItemIdPattern.Match(line);
        return match.Success && TryParsePositiveInt(match.Groups["id"].Value, out itemId);
    }

    private static bool TryParsePositiveInt(string value, out int itemId)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out itemId) && itemId > 0;
    }

    private static bool IsSkippedLine(string trimmedLine)
    {
        return trimmedLine.Length == 0 ||
            trimmedLine.StartsWith("//", StringComparison.Ordinal) ||
            trimmedLine.StartsWith(';') ||
            trimmedLine.StartsWith('#');
    }

    private static string ResolveKnownItemSourcePath(string knownItemSourcePathOrDbServerPath)
    {
        var fullPath = Path.GetFullPath(knownItemSourcePathOrDbServerPath);
        if (File.Exists(fullPath))
        {
            return fullPath;
        }

        if (!Directory.Exists(fullPath))
        {
            throw new FileNotFoundException($"Known item source path not found: {fullPath}", fullPath);
        }

        foreach (var relativePath in KnownItemSourceCandidates)
        {
            var candidatePath = Path.Combine(fullPath, relativePath);
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        throw new FileNotFoundException(
            $"Known item source path is a directory, but no itemtype source was found under: {fullPath}",
            fullPath);
    }

    private static string NormalizeSideTableRelativePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var normalized = relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        if (Path.IsPathRooted(normalized) || normalized.Split('/').Any(static segment => segment == ".."))
        {
            throw new ArgumentException($"Side-table path must be relative to the client root and must not escape it: {relativePath}");
        }

        return normalized;
    }

    private static string ResolveClientRelativeFilePath(string clientRoot, string relativePath)
    {
        var filePath = Path.GetFullPath(Path.Combine(
            clientRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = clientRoot.EndsWith(Path.DirectorySeparatorChar)
            ? clientRoot
            : clientRoot + Path.DirectorySeparatorChar;
        if (!filePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Side-table path escapes the client root: {relativePath}");
        }

        return filePath;
    }

    private static string RequireDirectory(string path, string label)
    {
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"{label} does not exist: {fullPath}");
        }

        return fullPath;
    }

    private static string ReadTextFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Contains((byte)0))
        {
            throw new InvalidOperationException(
                $"Text parsing is not supported for binary-looking sources: {path}. Provide a plaintext/decoded itemtype source or item id list.");
        }

        foreach (var encoding in GetTextEncodings())
        {
            try
            {
                return encoding.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
            }
        }

        throw new DecoderFallbackException($"Could not decode {path} as UTF-8 or code page 936 text.");
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

    private static readonly string[] KnownItemSourceCandidates =
    [
        Path.Combine("ini", "itemtype.dat"),
        Path.Combine("ini", "ItemType.dat"),
        Path.Combine("ini", "itemtype.txt"),
        Path.Combine("ini", "ItemType.txt"),
        Path.Combine("DBServer", "itemtype.txt"),
        Path.Combine("DBServer", "ItemType.txt"),
        "itemtype.txt",
        "ItemType.txt",
    ];
}

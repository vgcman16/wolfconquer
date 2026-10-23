using System.Text;
using System.Text.RegularExpressions;

internal static partial class Program
{
    private const string ClientStringRefAuditUsage =
        "Usage: WolfConquer.DataTool client-string-ref-audit <clientRoot> <relativeReferenceFile> <relativeResourceFile> [relativeResourceFile ...]";

    static int ClientStringRefAudit(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine(ClientStringRefAuditUsage);
            return 1;
        }

        ClientStringRefAuditReport report;
        try
        {
            report = ClientStringRefAuditor.Audit(
                args[1],
                args[2],
                args.Skip(3).ToArray());
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            Console.Error.WriteLine($"Could not audit client string references: {ex.Message}");
            Console.Error.WriteLine(ClientStringRefAuditUsage);
            return 1;
        }

        Console.WriteLine("Client string ref audit:");
        Console.WriteLine($"Client: {report.ClientRoot}");
        Console.WriteLine($"Reference file: {report.ReferenceFile}");
        Console.WriteLine($"Resource files: {report.ResourceFiles.Count}");
        Console.WriteLine($"Referenced keys: {report.ReferencedKeys.Count}");
        Console.WriteLine($"Resource keys: {report.ResourceKeys.Count}");
        Console.WriteLine($"Available keys: {report.AvailableKeys.Count}");
        Console.WriteLine($"Missing keys: {report.MissingKeys.Count}");

        if (report.MissingKeys.Count == 0)
        {
            Console.WriteLine("Missing key list: <none>");
            return 0;
        }

        Console.WriteLine("Missing key list:");
        foreach (var chunk in report.MissingKeys.Chunk(12))
        {
            Console.WriteLine($"  {string.Join(", ", chunk)}");
        }

        return 2;
    }
}

internal sealed record ClientStringRefAuditReport(
    string ClientRoot,
    string ReferenceFile,
    IReadOnlyList<string> ResourceFiles,
    IReadOnlyList<string> ReferencedKeys,
    IReadOnlyList<string> ResourceKeys,
    IReadOnlyList<string> AvailableKeys,
    IReadOnlyList<string> MissingKeys);

internal static class ClientStringRefAuditor
{
    private static readonly Regex QuotedStringKeyPattern = new(
        @"(?<quote>[""'])(?<key>STR_[A-Z0-9_]+)\k<quote>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BareStringKeyPattern = new(
        @"(?<![A-Z0-9_])(?<key>STR_[A-Z0-9_]+)(?![A-Z0-9_])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ResourceKeyPattern = new(
        @"^STR_[A-Z0-9_]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ClientStringRefAuditReport Audit(
        string clientRoot,
        string relativeReferenceFile,
        IReadOnlyCollection<string> relativeResourceFiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeReferenceFile);
        ArgumentNullException.ThrowIfNull(relativeResourceFiles);

        if (relativeResourceFiles.Count == 0)
        {
            throw new ArgumentException("At least one relative resource file is required.", nameof(relativeResourceFiles));
        }

        var fullClientRoot = RequireDirectory(clientRoot, "Client root");
        var referenceFile = NormalizeClientRelativePath(relativeReferenceFile, "Reference file");
        var resourceFiles = relativeResourceFiles
            .Select(path => NormalizeClientRelativePath(path, "Resource file"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var referencedKeys = ExtractReferencedKeys(ReadClientRelativeTextFile(fullClientRoot, referenceFile));
        var resourceKeys = resourceFiles
            .SelectMany(resourceFile => ExtractResourceKeys(ReadClientRelativeTextFile(fullClientRoot, resourceFile)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var resourceKeySet = new HashSet<string>(resourceKeys, StringComparer.OrdinalIgnoreCase);
        var availableKeys = referencedKeys
            .Where(resourceKeySet.Contains)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var missingKeys = referencedKeys
            .Where(key => !resourceKeySet.Contains(key))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ClientStringRefAuditReport(
            fullClientRoot,
            referenceFile,
            resourceFiles,
            referencedKeys,
            resourceKeys,
            availableKeys,
            missingKeys);
    }

    private static IReadOnlyList<string> ExtractReferencedKeys(string text)
    {
        var keys = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in QuotedStringKeyPattern.Matches(text))
        {
            keys.Add(match.Groups["key"].Value);
        }

        foreach (Match match in BareStringKeyPattern.Matches(text))
        {
            keys.Add(match.Groups["key"].Value);
        }

        return keys.ToArray();
    }

    private static IEnumerable<string> ExtractResourceKeys(string text)
    {
        foreach (var line in SplitLines(text))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 ||
                trimmed.StartsWith(';') ||
                trimmed.StartsWith('#') ||
                trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            if (ResourceKeyPattern.IsMatch(key))
            {
                yield return key;
            }
        }
    }

    private static string ReadClientRelativeTextFile(string clientRoot, string relativePath)
    {
        var fullPath = ResolveClientRelativeFilePath(clientRoot, relativePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Client file not found: {fullPath}", fullPath);
        }

        return ReadTextFile(fullPath);
    }

    private static string NormalizeClientRelativePath(string relativePath, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var trimmed = relativePath.Trim();
        var normalized = trimmed.Replace('\\', '/');
        if (normalized.StartsWith('/') ||
            normalized.StartsWith('\\') ||
            IsWindowsDriveRootedPath(normalized) ||
            Path.IsPathRooted(trimmed) ||
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(static segment => segment == ".."))
        {
            throw new ArgumentException($"{label} must be relative to the client root and must not escape it: {relativePath}");
        }

        return normalized;
    }

    private static string ResolveClientRelativeFilePath(string clientRoot, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(
            clientRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = clientRoot.EndsWith(Path.DirectorySeparatorChar)
            ? clientRoot
            : clientRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Client path escapes the client root: {relativePath}");
        }

        return fullPath;
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

    private static bool IsWindowsDriveRootedPath(string path)
    {
        return path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':';
    }
}

using System.Globalization;
using System.Text;

internal static partial class Program
{
    private const string ClientLuaDriftAuditUsage =
        "Usage: WolfConquer.DataTool client-lua-drift-audit <clientRoot> --reference-root <path> [--symbol <text>] [--top N]";

    private const int ClientLuaDriftAuditDefaultTopCount = 20;
    private const int ClientLuaDriftAuditMaxTextBytes = 4 * 1024 * 1024;

    static int ClientLuaDriftAudit(string[] args)
    {
        ClientLuaDriftAuditOptions options;
        try
        {
            options = ResolveClientLuaDriftAuditOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ClientLuaDriftAuditUsage);
            return 1;
        }

        if (!Directory.Exists(options.ClientRoot))
        {
            Console.Error.WriteLine($"Client root not found: {options.ClientRoot}");
            return 1;
        }

        if (!Directory.Exists(options.ReferenceRoot))
        {
            Console.Error.WriteLine($"Reference root not found: {options.ReferenceRoot}");
            return 1;
        }

        var currentIniRoot = Path.Combine(options.ClientRoot, "ini");
        if (!Directory.Exists(currentIniRoot))
        {
            Console.Error.WriteLine($"Current ini root not found: {currentIniRoot}");
            return 1;
        }

        var referenceIniRoot = Path.Combine(options.ReferenceRoot, "ini");
        if (!Directory.Exists(referenceIniRoot))
        {
            Console.Error.WriteLine($"Reference ini root not found: {referenceIniRoot}");
            return 1;
        }

        ClientLuaDriftAuditReport report;
        try
        {
            report = CreateClientLuaDriftAuditReport(options, currentIniRoot, referenceIniRoot);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not compare client Lua/resource files: {ex.Message}");
            return 1;
        }

        PrintClientLuaDriftAuditReport(report, options);
        return report.HasDrift ? 2 : 0;
    }

    private static ClientLuaDriftAuditOptions ResolveClientLuaDriftAuditOptions(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("Missing client root.");
        }

        string? clientRoot = null;
        string? referenceRoot = null;
        string? symbol = null;
        var topCount = ClientLuaDriftAuditDefaultTopCount;

        for (var index = 1; index < args.Length; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--reference-root", StringComparison.OrdinalIgnoreCase))
            {
                if (index == args.Length - 1)
                {
                    throw new ArgumentException("Missing value for --reference-root.");
                }

                referenceRoot = Path.GetFullPath(args[++index]);
                continue;
            }

            const string referenceRootPrefix = "--reference-root=";
            if (arg.StartsWith(referenceRootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                referenceRoot = Path.GetFullPath(arg[referenceRootPrefix.Length..]);
                continue;
            }

            if (string.Equals(arg, "--symbol", StringComparison.OrdinalIgnoreCase))
            {
                if (index == args.Length - 1)
                {
                    throw new ArgumentException("Missing value for --symbol.");
                }

                symbol = RequireClientLuaDriftSymbol(args[++index]);
                continue;
            }

            const string symbolPrefix = "--symbol=";
            if (arg.StartsWith(symbolPrefix, StringComparison.OrdinalIgnoreCase))
            {
                symbol = RequireClientLuaDriftSymbol(arg[symbolPrefix.Length..]);
                continue;
            }

            if (string.Equals(arg, "--top", StringComparison.OrdinalIgnoreCase))
            {
                if (index == args.Length - 1)
                {
                    throw new ArgumentException("Missing value for --top.");
                }

                topCount = ParseClientLuaDriftPositiveInteger(args[++index], "--top");
                continue;
            }

            const string topPrefix = "--top=";
            if (arg.StartsWith(topPrefix, StringComparison.OrdinalIgnoreCase))
            {
                topCount = ParseClientLuaDriftPositiveInteger(arg[topPrefix.Length..], "--top");
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown client-lua-drift-audit option: {arg}");
            }

            if (clientRoot is not null)
            {
                throw new ArgumentException("Too many positional arguments for client-lua-drift-audit.");
            }

            clientRoot = Path.GetFullPath(arg);
        }

        if (string.IsNullOrWhiteSpace(clientRoot))
        {
            throw new ArgumentException("Missing client root.");
        }

        if (string.IsNullOrWhiteSpace(referenceRoot))
        {
            throw new ArgumentException("Missing required --reference-root.");
        }

        return new ClientLuaDriftAuditOptions(clientRoot, referenceRoot, symbol, topCount);
    }

    private static string RequireClientLuaDriftSymbol(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("--symbol requires non-empty text.");
        }

        return value;
    }

    private static int ParseClientLuaDriftPositiveInteger(string value, string optionName)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed <= 0)
        {
            throw new FormatException($"{optionName} requires a positive integer.");
        }

        return parsed;
    }

    private static ClientLuaDriftAuditReport CreateClientLuaDriftAuditReport(
        ClientLuaDriftAuditOptions options,
        string currentIniRoot,
        string referenceIniRoot)
    {
        var currentFiles = BuildClientLuaDriftIndex(currentIniRoot);
        var referenceFiles = BuildClientLuaDriftIndex(referenceIniRoot);
        var fileRows = new List<ClientLuaDriftFileRow>();
        var groupCounts = new Dictionary<string, ClientLuaDriftGroupBuilder>(StringComparer.OrdinalIgnoreCase);

        var relativePaths = currentFiles.Keys
            .Concat(referenceFiles.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);

        foreach (var relativePath in relativePaths)
        {
            currentFiles.TryGetValue(relativePath, out var currentFile);
            referenceFiles.TryGetValue(relativePath, out var referenceFile);
            var state = ResolveClientLuaDriftState(currentFile, referenceFile);
            var groupName = GetClientLuaDriftGroupName(relativePath);
            var group = GetClientLuaDriftGroupBuilder(groupCounts, groupName);
            group.Add(state);
            fileRows.Add(new ClientLuaDriftFileRow(relativePath, state, currentFile?.FullPath, referenceFile?.FullPath));
        }

        var groups = groupCounts.Values
            .Select(builder => builder.ToGroup())
            .OrderByDescending(group => group.DriftCount)
            .ThenByDescending(group => group.SameCount)
            .ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var symbolEvidence = Array.Empty<ClientLuaDriftSymbolEvidence>();
        var symbolUnreadableFiles = 0;
        if (options.Symbol is not null)
        {
            (symbolEvidence, symbolUnreadableFiles) = FindClientLuaDriftSymbolEvidence(fileRows, options.Symbol);
        }

        return new ClientLuaDriftAuditReport(
            options.ClientRoot,
            options.ReferenceRoot,
            fileRows.Count(file => file.State == ClientLuaDriftState.Same),
            fileRows.Count(file => file.State == ClientLuaDriftState.Different),
            fileRows.Count(file => file.State == ClientLuaDriftState.CurrentOnly),
            fileRows.Count(file => file.State == ClientLuaDriftState.ReferenceOnly),
            groups,
            symbolEvidence,
            symbolUnreadableFiles);
    }

    private static IReadOnlyDictionary<string, ClientLuaDriftIndexedFile> BuildClientLuaDriftIndex(string iniRoot)
    {
        var files = new Dictionary<string, ClientLuaDriftIndexedFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in EnumerateClientLuaDriftFiles(iniRoot).Order(StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = NormalizeClientLuaDriftRelativePath(Path.GetRelativePath(iniRoot, path));
            var info = new FileInfo(path);
            files[relativePath] = new ClientLuaDriftIndexedFile(relativePath, path, info.Length);
        }

        return files;
    }

    private static IEnumerable<string> EnumerateClientLuaDriftFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (IsClientLuaDriftTrackedFile(file))
            {
                yield return file;
            }
        }

        foreach (var subdirectory in Directory.EnumerateDirectories(directory).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (ShouldSkipClientLuaDriftDirectory(Path.GetFileName(subdirectory)))
            {
                continue;
            }

            foreach (var file in EnumerateClientLuaDriftFiles(subdirectory))
            {
                yield return file;
            }
        }
    }

    private static bool ShouldSkipClientLuaDriftDirectory(string directoryName)
    {
        return directoryName.Length > 0 && directoryName[0] == '.' ||
            directoryName.StartsWith("ini.oldcompat-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsClientLuaDriftTrackedFile(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".lua", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".dat", StringComparison.OrdinalIgnoreCase);
    }

    private static ClientLuaDriftState ResolveClientLuaDriftState(
        ClientLuaDriftIndexedFile? currentFile,
        ClientLuaDriftIndexedFile? referenceFile)
    {
        if (currentFile is null)
        {
            return ClientLuaDriftState.ReferenceOnly;
        }

        if (referenceFile is null)
        {
            return ClientLuaDriftState.CurrentOnly;
        }

        return ClientLuaDriftFilesEqual(currentFile, referenceFile)
            ? ClientLuaDriftState.Same
            : ClientLuaDriftState.Different;
    }

    private static bool ClientLuaDriftFilesEqual(ClientLuaDriftIndexedFile currentFile, ClientLuaDriftIndexedFile referenceFile)
    {
        if (currentFile.Length != referenceFile.Length)
        {
            return false;
        }

        using var currentStream = File.OpenRead(currentFile.FullPath);
        using var referenceStream = File.OpenRead(referenceFile.FullPath);
        Span<byte> currentBuffer = stackalloc byte[8192];
        Span<byte> referenceBuffer = stackalloc byte[8192];

        while (true)
        {
            var currentRead = currentStream.Read(currentBuffer);
            var referenceRead = referenceStream.Read(referenceBuffer);
            if (currentRead != referenceRead)
            {
                return false;
            }

            if (currentRead == 0)
            {
                return true;
            }

            if (!currentBuffer[..currentRead].SequenceEqual(referenceBuffer[..referenceRead]))
            {
                return false;
            }
        }
    }

    private static string NormalizeClientLuaDriftRelativePath(string relativePath)
    {
        return relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string GetClientLuaDriftGroupName(string relativePath)
    {
        var separator = relativePath.IndexOf('/', StringComparison.Ordinal);
        return separator < 0 ? "<ini-root>" : relativePath[..separator];
    }

    private static ClientLuaDriftGroupBuilder GetClientLuaDriftGroupBuilder(
        Dictionary<string, ClientLuaDriftGroupBuilder> groups,
        string name)
    {
        if (!groups.TryGetValue(name, out var group))
        {
            group = new ClientLuaDriftGroupBuilder(name);
            groups.Add(name, group);
        }

        return group;
    }

    private static (ClientLuaDriftSymbolEvidence[] Evidence, int UnreadableFiles) FindClientLuaDriftSymbolEvidence(
        IReadOnlyList<ClientLuaDriftFileRow> fileRows,
        string symbol)
    {
        var evidence = new List<ClientLuaDriftSymbolEvidence>();
        var unreadableFiles = 0;
        foreach (var file in fileRows
            .Where(file => file.CurrentPath is not null &&
                (file.State == ClientLuaDriftState.CurrentOnly || file.State == ClientLuaDriftState.Different))
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            if (!TryReadClientLuaDriftText(file.CurrentPath!, out var text, out var encoding))
            {
                unreadableFiles++;
                continue;
            }

            var symbolIndex = text.IndexOf(symbol, StringComparison.Ordinal);
            if (symbolIndex < 0)
            {
                continue;
            }

            var location = CreateClientLuaDriftSymbolLocation(text, symbolIndex, encoding);
            evidence.Add(new ClientLuaDriftSymbolEvidence(
                file.RelativePath,
                file.State,
                location.LineNumber,
                location.ByteOffset,
                location.LineText));
        }

        return (evidence.ToArray(), unreadableFiles);
    }

    private static bool TryReadClientLuaDriftText(string path, out string text, out Encoding encoding)
    {
        text = string.Empty;
        encoding = Encoding.UTF8;
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > ClientLuaDriftAuditMaxTextBytes || bytes.Contains((byte)0))
        {
            return false;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        foreach (var candidate in GetClientLuaDriftTextEncodings())
        {
            try
            {
                text = candidate.GetString(bytes);
                encoding = candidate;
                if (text.Length > 0 && text[0] == '\uFEFF')
                {
                    text = text[1..];
                }

                return true;
            }
            catch (DecoderFallbackException)
            {
            }
        }

        text = string.Empty;
        encoding = Encoding.UTF8;
        return false;
    }

    private static IEnumerable<Encoding> GetClientLuaDriftTextEncodings()
    {
        yield return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        yield return Encoding.GetEncoding(
            936,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }

    private static ClientLuaDriftSymbolLocation CreateClientLuaDriftSymbolLocation(
        string text,
        int symbolIndex,
        Encoding encoding)
    {
        var lineNumber = 1;
        var lineStart = 0;
        for (var index = 0; index < symbolIndex; index++)
        {
            if (text[index] == '\n')
            {
                lineNumber++;
                lineStart = index + 1;
            }
        }

        var lineEnd = text.IndexOfAny(['\r', '\n'], lineStart);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        var lineText = CompactClientLuaDriftLine(text[lineStart..lineEnd]);
        var byteOffset = encoding.GetByteCount(text[..symbolIndex]);
        return new ClientLuaDriftSymbolLocation(lineNumber, byteOffset, lineText);
    }

    private static string CompactClientLuaDriftLine(string line)
    {
        var compact = line.Trim();
        return compact.Length <= 120 ? compact : compact[..117] + "...";
    }

    private static void PrintClientLuaDriftAuditReport(ClientLuaDriftAuditReport report, ClientLuaDriftAuditOptions options)
    {
        Console.WriteLine("Client Lua drift audit:");
        Console.WriteLine($"Current: {report.ClientRoot}");
        Console.WriteLine($"Reference: {report.ReferenceRoot}");
        Console.WriteLine("Scope: ini/**/*.lua, ini/**/*.dat");
        Console.WriteLine($"Same files: {report.SameCount}");
        Console.WriteLine($"Different files: {report.DifferentCount}");
        Console.WriteLine($"Current-only files: {report.CurrentOnlyCount}");
        Console.WriteLine($"Reference-only files: {report.ReferenceOnlyCount}");

        var shownGroups = Math.Min(options.TopCount, report.Groups.Count);
        Console.WriteLine($"Group summary (top {shownGroups} of {report.Groups.Count}):");
        foreach (var group in report.Groups.Take(options.TopCount))
        {
            Console.WriteLine(
                $"  {group.Name} same={group.SameCount} different={group.DifferentCount} " +
                $"current-only={group.CurrentOnlyCount} reference-only={group.ReferenceOnlyCount}");
        }

        if (shownGroups < report.Groups.Count)
        {
            Console.WriteLine($"  ... {report.Groups.Count - shownGroups} more group(s)");
        }

        if (options.Symbol is not null)
        {
            var shownEvidence = Math.Min(options.TopCount, report.SymbolEvidence.Count);
            Console.WriteLine($"Symbol: {options.Symbol}");
            Console.WriteLine($"Symbol matches: {report.SymbolEvidence.Count} (showing {shownEvidence})");
            foreach (var evidence in report.SymbolEvidence.Take(options.TopCount))
            {
                Console.WriteLine(
                    $"  {FormatClientLuaDriftState(evidence.State)} {evidence.RelativePath} " +
                    $"line={evidence.LineNumber} offset={evidence.ByteOffset} text={evidence.LineText}");
            }

            if (shownEvidence < report.SymbolEvidence.Count)
            {
                Console.WriteLine($"  ... {report.SymbolEvidence.Count - shownEvidence} more symbol match(es)");
            }

            if (report.SymbolUnreadableFiles > 0)
            {
                Console.WriteLine($"Symbol unreadable files skipped: {report.SymbolUnreadableFiles}");
            }
        }

        if (!report.HasDrift)
        {
            Console.WriteLine("No startup Lua/resource drift found.");
        }
    }

    private static string FormatClientLuaDriftState(ClientLuaDriftState state)
    {
        return state switch
        {
            ClientLuaDriftState.Same => "same",
            ClientLuaDriftState.Different => "different",
            ClientLuaDriftState.CurrentOnly => "current-only",
            ClientLuaDriftState.ReferenceOnly => "reference-only",
            _ => state.ToString(),
        };
    }
}

internal sealed record ClientLuaDriftAuditOptions(
    string ClientRoot,
    string ReferenceRoot,
    string? Symbol,
    int TopCount);

internal sealed record ClientLuaDriftAuditReport(
    string ClientRoot,
    string ReferenceRoot,
    int SameCount,
    int DifferentCount,
    int CurrentOnlyCount,
    int ReferenceOnlyCount,
    IReadOnlyList<ClientLuaDriftGroup> Groups,
    IReadOnlyList<ClientLuaDriftSymbolEvidence> SymbolEvidence,
    int SymbolUnreadableFiles)
{
    public bool HasDrift => DifferentCount > 0 || CurrentOnlyCount > 0 || ReferenceOnlyCount > 0;
}

internal sealed record ClientLuaDriftGroup(
    string Name,
    int SameCount,
    int DifferentCount,
    int CurrentOnlyCount,
    int ReferenceOnlyCount)
{
    public int DriftCount => DifferentCount + CurrentOnlyCount + ReferenceOnlyCount;
}

internal sealed record ClientLuaDriftSymbolEvidence(
    string RelativePath,
    ClientLuaDriftState State,
    int LineNumber,
    int ByteOffset,
    string LineText);

internal enum ClientLuaDriftState
{
    Same,
    Different,
    CurrentOnly,
    ReferenceOnly,
}

internal sealed record ClientLuaDriftIndexedFile(
    string RelativePath,
    string FullPath,
    long Length);

internal sealed record ClientLuaDriftFileRow(
    string RelativePath,
    ClientLuaDriftState State,
    string? CurrentPath,
    string? ReferencePath);

internal sealed record ClientLuaDriftSymbolLocation(
    int LineNumber,
    int ByteOffset,
    string LineText);

internal sealed class ClientLuaDriftGroupBuilder(string name)
{
    public string Name { get; } = name;

    public int SameCount { get; private set; }

    public int DifferentCount { get; private set; }

    public int CurrentOnlyCount { get; private set; }

    public int ReferenceOnlyCount { get; private set; }

    public void Add(ClientLuaDriftState state)
    {
        switch (state)
        {
            case ClientLuaDriftState.Same:
                SameCount++;
                break;
            case ClientLuaDriftState.Different:
                DifferentCount++;
                break;
            case ClientLuaDriftState.CurrentOnly:
                CurrentOnlyCount++;
                break;
            case ClientLuaDriftState.ReferenceOnly:
                ReferenceOnlyCount++;
                break;
        }
    }

    public ClientLuaDriftGroup ToGroup()
    {
        return new ClientLuaDriftGroup(Name, SameCount, DifferentCount, CurrentOnlyCount, ReferenceOnlyCount);
    }
}

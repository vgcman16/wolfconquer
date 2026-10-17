using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

internal static partial class Program
{
    private static readonly string[] ClientLaunchEvidenceDefaultStrings =
    [
        "play.exe",
        "._cache_play.exe",
        "Conquer.exe",
        "._cache_Conquer.exe",
        "Update.exe",
        "._cache_Update.exe",
        "Env_DX9",
        "AutoPatch",
        "GameInfo.ini",
        "version.dat",
        "zf.dat",
        "IMAGE_BACKGROUNDPIC.png",
    ];

    private const string ClientLaunchEvidenceUsage =
        "Usage: WolfConquer.DataTool client-launch-evidence [--string text] [--no-default-strings] [--max-string-offsets N] [--max-import-dlls N] <path|label=path> [...]";

    private const int ClientLaunchEvidenceDefaultMaxStringOffsets = 8;
    private const int ClientLaunchEvidenceMaxStringOffsetsLimit = 128;
    private const int ClientLaunchEvidenceDefaultMaxImportDlls = 80;
    private const int ClientLaunchEvidenceMaxImportDllsLimit = 512;
    private const int ClientLaunchEvidenceImportDescriptorLimit = 512;
    private const int ClientLaunchEvidenceImportNameLimit = 512;

    static int ClientLaunchEvidence(string[] args)
    {
        ClientLaunchEvidenceOptions options;
        try
        {
            options = ResolveClientLaunchEvidenceOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ClientLaunchEvidenceUsage);
            return 1;
        }

        foreach (var input in options.Inputs)
        {
            if (!File.Exists(input.Path))
            {
                Console.Error.WriteLine($"Client executable not found: label={input.Label} path={input.Path}");
                return 1;
            }
        }

        try
        {
            var reports = options.Inputs
                .Select(input => BuildClientLaunchEvidenceReport(input, options))
                .ToArray();
            PrintClientLaunchEvidence(options, reports);
            return 0;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            Console.Error.WriteLine($"Client launch evidence failed: {ex.Message}");
            return 1;
        }
    }

    static ClientLaunchEvidenceOptions ResolveClientLaunchEvidenceOptions(string[] args)
    {
        var inputs = new List<ClientLaunchEvidenceInput>();
        var strings = new List<string>();
        var includeDefaultStrings = true;
        var maxStringOffsets = ClientLaunchEvidenceDefaultMaxStringOffsets;
        var maxImportDlls = ClientLaunchEvidenceDefaultMaxImportDlls;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--no-default-strings", StringComparison.OrdinalIgnoreCase))
            {
                includeDefaultStrings = false;
                continue;
            }

            if (string.Equals(argument, "--string", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, "--term", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException($"{argument} requires text.");
                }

                AddClientLaunchEvidenceString(strings, args[++index]);
                continue;
            }

            const string stringPrefix = "--string=";
            if (argument.StartsWith(stringPrefix, StringComparison.OrdinalIgnoreCase))
            {
                AddClientLaunchEvidenceString(strings, argument[stringPrefix.Length..]);
                continue;
            }

            const string termPrefix = "--term=";
            if (argument.StartsWith(termPrefix, StringComparison.OrdinalIgnoreCase))
            {
                AddClientLaunchEvidenceString(strings, argument[termPrefix.Length..]);
                continue;
            }

            if (string.Equals(argument, "--max-string-offsets", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-launch-evidence --max-string-offsets requires a count.");
                }

                maxStringOffsets = ParseClientLaunchEvidenceInteger(
                    args[++index],
                    "--max-string-offsets",
                    minimum: 0,
                    maximum: ClientLaunchEvidenceMaxStringOffsetsLimit);
                continue;
            }

            const string maxStringOffsetsPrefix = "--max-string-offsets=";
            if (argument.StartsWith(maxStringOffsetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                maxStringOffsets = ParseClientLaunchEvidenceInteger(
                    argument[maxStringOffsetsPrefix.Length..],
                    "--max-string-offsets",
                    minimum: 0,
                    maximum: ClientLaunchEvidenceMaxStringOffsetsLimit);
                continue;
            }

            if (string.Equals(argument, "--max-import-dlls", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-launch-evidence --max-import-dlls requires a count.");
                }

                maxImportDlls = ParseClientLaunchEvidenceInteger(
                    args[++index],
                    "--max-import-dlls",
                    minimum: 1,
                    maximum: ClientLaunchEvidenceMaxImportDllsLimit);
                continue;
            }

            const string maxImportDllsPrefix = "--max-import-dlls=";
            if (argument.StartsWith(maxImportDllsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                maxImportDlls = ParseClientLaunchEvidenceInteger(
                    argument[maxImportDllsPrefix.Length..],
                    "--max-import-dlls",
                    minimum: 1,
                    maximum: ClientLaunchEvidenceMaxImportDllsLimit);
                continue;
            }

            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown client-launch-evidence option: {argument}");
            }

            inputs.Add(ParseClientLaunchEvidenceInput(argument));
        }

        if (inputs.Count == 0)
        {
            throw new ArgumentException("client-launch-evidence requires at least one client executable path.");
        }

        if (includeDefaultStrings)
        {
            foreach (var value in ClientLaunchEvidenceDefaultStrings)
            {
                AddClientLaunchEvidenceString(strings, value);
            }
        }

        if (strings.Count == 0)
        {
            throw new ArgumentException("client-launch-evidence needs at least one string term unless default strings are enabled.");
        }

        return new ClientLaunchEvidenceOptions(inputs, strings, maxStringOffsets, maxImportDlls);
    }

    private static void AddClientLaunchEvidenceString(List<string> strings, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("client-launch-evidence string terms must be non-empty.");
        }

        if (value.Any(c => c > sbyte.MaxValue))
        {
            throw new ArgumentException("client-launch-evidence string terms must be ASCII so ASCII and UTF-16LE scans are comparable.");
        }

        if (!strings.Contains(value, StringComparer.Ordinal))
        {
            strings.Add(value);
        }
    }

    private static int ParseClientLaunchEvidenceInteger(
        string value,
        string optionName,
        int minimum,
        int maximum)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < minimum ||
            parsed > maximum)
        {
            throw new ArgumentException(
                $"client-launch-evidence {optionName} must be between {minimum.ToString(CultureInfo.InvariantCulture)} and {maximum.ToString(CultureInfo.InvariantCulture)}.");
        }

        return parsed;
    }

    private static ClientLaunchEvidenceInput ParseClientLaunchEvidenceInput(string value)
    {
        var separatorIndex = value.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex > 0 && separatorIndex < value.Length - 1)
        {
            var label = value[..separatorIndex].Trim();
            if (label.Length == 0 || label.Any(char.IsWhiteSpace))
            {
                throw new ArgumentException("client-launch-evidence labels must be non-empty and contain no whitespace.");
            }

            return new ClientLaunchEvidenceInput(label, Path.GetFullPath(value[(separatorIndex + 1)..]));
        }

        var path = Path.GetFullPath(value);
        var inferredLabel = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(inferredLabel))
        {
            inferredLabel = "client";
        }

        return new ClientLaunchEvidenceInput(inferredLabel, path);
    }

    private static ClientLaunchEvidenceReport BuildClientLaunchEvidenceReport(
        ClientLaunchEvidenceInput input,
        ClientLaunchEvidenceOptions options)
    {
        var fileInfo = new FileInfo(input.Path);
        var attributes = File.GetAttributes(input.Path);
        var bytes = File.ReadAllBytes(input.Path);
        var version = ReadClientLaunchEvidenceVersion(input.Path);
        var pe = AnalyzeClientLaunchEvidencePe(bytes, options.MaxImportDlls);
        var stringHits = AnalyzeClientLaunchEvidenceStrings(bytes, options.Strings, options.MaxStringOffsets);

        return new ClientLaunchEvidenceReport(
            input.Label,
            input.Path,
            fileInfo.Length,
            ComputeClientLaunchEvidenceSha256(input.Path),
            attributes.ToString(),
            version,
            pe,
            stringHits);
    }

    private static string ComputeClientLaunchEvidenceSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static ClientLaunchEvidenceVersionInfo ReadClientLaunchEvidenceVersion(string path)
    {
        var info = FileVersionInfo.GetVersionInfo(path);
        return new ClientLaunchEvidenceVersionInfo(
            FormatClientLaunchEvidenceOptional(info.FileVersion),
            FormatClientLaunchEvidenceOptional(info.ProductVersion),
            FormatClientLaunchEvidenceOptional(info.ProductName),
            FormatClientLaunchEvidenceOptional(info.OriginalFilename));
    }

    private static IReadOnlyList<ClientLaunchEvidenceStringHit> AnalyzeClientLaunchEvidenceStrings(
        byte[] bytes,
        IReadOnlyList<string> strings,
        int maxOffsets)
    {
        var hits = new List<ClientLaunchEvidenceStringHit>(strings.Count * 2);
        foreach (var pattern in CreateGeneratedBufferStringScanPatterns(strings))
        {
            var bounded = FindClientLaunchEvidenceStringOffsets(bytes, pattern.Bytes, maxOffsets);
            hits.Add(new ClientLaunchEvidenceStringHit(
                pattern.Term,
                pattern.EncodingName,
                bounded.TotalCount,
                bounded.Offsets,
                bounded.Truncated));
        }

        return hits;
    }

    private static ClientLaunchEvidenceBoundedOffsets FindClientLaunchEvidenceStringOffsets(
        byte[] bytes,
        byte[] pattern,
        int maxOffsets)
    {
        var offsets = new List<long>(Math.Min(maxOffsets, 16));
        if (pattern.Length == 0 || pattern.Length > bytes.Length)
        {
            return new ClientLaunchEvidenceBoundedOffsets(0, offsets, Truncated: false);
        }

        var source = bytes.AsSpan();
        var needle = pattern.AsSpan();
        var searchOffset = 0;
        var totalCount = 0;
        while (searchOffset <= source.Length - needle.Length)
        {
            var relativeOffset = source[searchOffset..].IndexOf(needle);
            if (relativeOffset < 0)
            {
                break;
            }

            var absoluteOffset = searchOffset + relativeOffset;
            totalCount++;
            if (offsets.Count < maxOffsets)
            {
                offsets.Add(absoluteOffset);
            }

            searchOffset = absoluteOffset + 1;
        }

        return new ClientLaunchEvidenceBoundedOffsets(totalCount, offsets, totalCount > offsets.Count);
    }

    private static void PrintClientLaunchEvidence(
        ClientLaunchEvidenceOptions options,
        IReadOnlyList<ClientLaunchEvidenceReport> reports)
    {
        Console.WriteLine("Client launch evidence:");
        Console.WriteLine("Mode: no-launch metadata-only");
        Console.WriteLine($"Inputs: {reports.Count.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Strings: {string.Join(", ", options.Strings)}");
        Console.WriteLine("String encodings: ascii, utf-16le");
        Console.WriteLine($"Max string offsets: {options.MaxStringOffsets.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Max import DLLs: {options.MaxImportDlls.ToString(CultureInfo.InvariantCulture)}");

        foreach (var report in reports)
        {
            Console.WriteLine($"Executable: label={report.Label} path={report.Path}");
            Console.WriteLine($"  Size: {report.SizeBytes.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine($"  SHA256: {report.Sha256}");
            Console.WriteLine($"  Attributes: {report.Attributes}");
            Console.WriteLine(
                $"  Version: file={report.Version.FileVersion} product={report.Version.ProductVersion} productName={report.Version.ProductName} originalFilename={report.Version.OriginalFilename}");
            if (report.Pe.IsPe)
            {
                var importError = string.IsNullOrWhiteSpace(report.Pe.ImportReadError)
                    ? string.Empty
                    : $" importError={report.Pe.ImportReadError}";
                Console.WriteLine(
                    $"  PE: valid=True machine={report.Pe.Machine} subsystem={report.Pe.Subsystem} " +
                    $"importDllCount={report.Pe.ImportDllNames.Count.ToString(CultureInfo.InvariantCulture)} " +
                    $"imports={FormatClientLaunchEvidenceList(report.Pe.ImportDllNames, options.MaxImportDlls, report.Pe.ImportDllNamesTruncated)}{importError}");
            }
            else
            {
                Console.WriteLine($"  PE: valid=False reason={report.Pe.Reason}");
            }

            foreach (var hit in report.StringHits)
            {
                Console.WriteLine(
                    $"  String: term={hit.Term} encoding={hit.EncodingName} present={FormatClientLaunchEvidenceBool(hit.Present)} " +
                    $"count={hit.Count.ToString(CultureInfo.InvariantCulture)} offsets={FormatClientLaunchEvidenceOffsets(hit.Offsets, hit.Truncated)}");
            }
        }

        PrintClientLaunchEvidenceComparison(reports);
    }

    private static void PrintClientLaunchEvidenceComparison(IReadOnlyList<ClientLaunchEvidenceReport> reports)
    {
        Console.WriteLine("Comparison:");
        Console.WriteLine($"  Same size: {FormatClientLaunchEvidenceBool(AllClientLaunchEvidenceSame(reports, report => report.SizeBytes.ToString(CultureInfo.InvariantCulture)))}");
        Console.WriteLine($"  Same SHA256: {FormatClientLaunchEvidenceBool(AllClientLaunchEvidenceSame(reports, report => report.Sha256))}");
        Console.WriteLine($"  Same attributes: {FormatClientLaunchEvidenceBool(AllClientLaunchEvidenceSame(reports, report => report.Attributes))}");
        Console.WriteLine($"  Same file version: {FormatClientLaunchEvidenceBool(AllClientLaunchEvidenceSame(reports, report => report.Version.FileVersion))}");
        Console.WriteLine($"  Same product version: {FormatClientLaunchEvidenceBool(AllClientLaunchEvidenceSame(reports, report => report.Version.ProductVersion))}");
        Console.WriteLine($"  Same PE machine: {FormatClientLaunchEvidenceBool(AllClientLaunchEvidenceSame(reports, report => report.Pe.Machine))}");
        Console.WriteLine($"  Same PE subsystem: {FormatClientLaunchEvidenceBool(AllClientLaunchEvidenceSame(reports, report => report.Pe.Subsystem))}");
        Console.WriteLine($"  Same import DLL set: {FormatClientLaunchEvidenceBool(AllClientLaunchEvidenceSame(reports, report => NormalizeClientLaunchEvidenceSet(report.Pe.ImportDllNames)))}");
        Console.WriteLine($"  Same string presence: {FormatClientLaunchEvidenceBool(AllClientLaunchEvidenceStringPresenceSame(reports))}");
    }

    private static bool AllClientLaunchEvidenceSame<T>(
        IReadOnlyList<ClientLaunchEvidenceReport> reports,
        Func<ClientLaunchEvidenceReport, T> selector)
    {
        if (reports.Count <= 1)
        {
            return true;
        }

        var first = selector(reports[0]);
        return reports.Skip(1).All(report => EqualityComparer<T>.Default.Equals(first, selector(report)));
    }

    private static bool AllClientLaunchEvidenceStringPresenceSame(IReadOnlyList<ClientLaunchEvidenceReport> reports)
    {
        if (reports.Count <= 1)
        {
            return true;
        }

        var first = NormalizeClientLaunchEvidenceStringPresence(reports[0]);
        return reports.Skip(1)
            .All(report => string.Equals(first, NormalizeClientLaunchEvidenceStringPresence(report), StringComparison.Ordinal));
    }

    private static string NormalizeClientLaunchEvidenceStringPresence(ClientLaunchEvidenceReport report)
    {
        return string.Join(
            "|",
            report.StringHits.Select(hit =>
                hit.Term + "\t" + hit.EncodingName + "\t" + FormatClientLaunchEvidenceBool(hit.Present)));
    }

    private static string NormalizeClientLaunchEvidenceSet(IReadOnlyList<string> values)
    {
        return string.Join(
            "|",
            values
                .Select(value => value.ToLowerInvariant())
                .Order(StringComparer.Ordinal));
    }

    private static string FormatClientLaunchEvidenceList(
        IReadOnlyList<string> values,
        int maxValues,
        bool truncated)
    {
        if (values.Count == 0 || maxValues == 0)
        {
            return values.Count == 0 ? "<none>" : $"<hidden> (+{values.Count.ToString(CultureInfo.InvariantCulture)} more)";
        }

        var shown = values.Take(maxValues).ToArray();
        var text = string.Join(",", shown);
        return truncated
            ? text + " (+more)"
            : text;
    }

    private static string FormatClientLaunchEvidenceOffsets(IReadOnlyList<long> offsets, bool truncated)
    {
        if (offsets.Count == 0)
        {
            return truncated ? "<hidden> (+more)" : "<none>";
        }

        var formatted = string.Join(",", offsets.Select(FormatScanHex));
        return truncated ? formatted + " (+more)" : formatted;
    }

    private static string FormatClientLaunchEvidenceOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<missing>" : value.Trim();
    }

    private static string FormatClientLaunchEvidenceBool(bool value)
    {
        return value ? "True" : "False";
    }

}

internal sealed record ClientLaunchEvidenceOptions(
    IReadOnlyList<ClientLaunchEvidenceInput> Inputs,
    IReadOnlyList<string> Strings,
    int MaxStringOffsets,
    int MaxImportDlls);

internal sealed record ClientLaunchEvidenceInput(string Label, string Path);

internal sealed record ClientLaunchEvidenceReport(
    string Label,
    string Path,
    long SizeBytes,
    string Sha256,
    string Attributes,
    ClientLaunchEvidenceVersionInfo Version,
    ClientLaunchEvidencePeInfo Pe,
    IReadOnlyList<ClientLaunchEvidenceStringHit> StringHits);

internal sealed record ClientLaunchEvidenceVersionInfo(
    string FileVersion,
    string ProductVersion,
    string ProductName,
    string OriginalFilename);

internal sealed record ClientLaunchEvidencePeInfo(
    bool IsPe,
    string Reason,
    string Machine,
    string Subsystem,
    IReadOnlyList<string> ImportDllNames,
    bool ImportDllNamesTruncated,
    string ImportReadError)
{
    public static ClientLaunchEvidencePeInfo NotPe(string reason)
    {
        return new ClientLaunchEvidencePeInfo(
            IsPe: false,
            Reason: reason,
            Machine: "<not-pe>",
            Subsystem: "<not-pe>",
            ImportDllNames: [],
            ImportDllNamesTruncated: false,
            ImportReadError: string.Empty);
    }
}

internal sealed record ClientLaunchEvidenceStringHit(
    string Term,
    string EncodingName,
    int Count,
    IReadOnlyList<long> Offsets,
    bool Truncated)
{
    public bool Present => Count > 0;
}

internal sealed record ClientLaunchEvidenceBoundedOffsets(
    int TotalCount,
    IReadOnlyList<long> Offsets,
    bool Truncated);

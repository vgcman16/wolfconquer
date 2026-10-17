using System.Text;

internal static partial class Program
{
    private const string ClientLauncherSafetyAuditUsage =
        "Usage: WolfConquer.DataTool client-launcher-safety-audit <clientRoot> [--target-only] [--target-path path] [--evidence-path path] [--live-evidence-path path] [--quarantined-evidence-path path]";

    private static readonly string[] ClientLauncherSafetyAuditRelativeFiles =
    [
        "AutoP.exe",
        "._cache_AutoP.exe",
        "._cache_Conquer.exe",
        "Env_DX9/Conquer.exe",
        "Env_DX9/._cache_Conquer.exe",
        "Env_DX8/Conquer.exe",
        "Env_DX8/._cache_Conquer.exe",
        "drvup.dat",
        "drvup.exe",
    ];

    private const int ClientLauncherSafetyAuditMaxEvidenceDirectoryFiles = 256;

    private static readonly string[] ClientLauncherSafetyAuditEvidenceOptions =
    [
        "--evidence",
        "--evidence-path",
        "--target-path",
        "--live-evidence-path",
        "--quarantined-evidence-path",
    ];

    private static readonly ClientLauncherSafetyAuditIndicator[] ClientLauncherSafetyAuditIndicators =
    [
        new("Synaptics.exe", ClientLauncherSafetyAuditIndicatorSeverity.HighRisk),
        new("xred.mooo.com", ClientLauncherSafetyAuditIndicatorSeverity.HighRisk),
        new("xredline", ClientLauncherSafetyAuditIndicatorSeverity.HighRisk),
        new("AUTORUNINJ", ClientLauncherSafetyAuditIndicatorSeverity.HighRisk),
        new("autorun.inf", ClientLauncherSafetyAuditIndicatorSeverity.HighRisk),
        new("SSLLibrary.dll", ClientLauncherSafetyAuditIndicatorSeverity.HighRisk),
        new(Environment.GetEnvironmentVariable("WOLFCONQUER_CLIENT_RISK_INDICATOR_URL") ?? "https://example.invalid/dynamic-route", ClientLauncherSafetyAuditIndicatorSeverity.HighRisk),
        new("tqhook", ClientLauncherSafetyAuditIndicatorSeverity.KnownNetDragonDriver),
    ];

    static int ClientLauncherSafetyAudit(string[] args)
    {
        if (!TryParseClientLauncherSafetyAuditOptions(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(ClientLauncherSafetyAuditUsage);
            return 1;
        }

        ClientLauncherSafetyAuditReport report;
        try
        {
            report = CreateClientLauncherSafetyAuditReport(options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not audit client launcher safety: {ex.Message}");
            return 1;
        }

        PrintClientLauncherSafetyAuditReport(report);
        return report.HasHighRiskIndicators ? 2 : 0;
    }

    private static bool TryParseClientLauncherSafetyAuditOptions(
        string[] args,
        out ClientLauncherSafetyAuditOptions options,
        out string error)
    {
        options = new ClientLauncherSafetyAuditOptions("", [], false);
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

        var evidencePaths = new List<ClientLauncherSafetyAuditEvidencePath>();
        var targetOnly = false;
        for (var index = 2; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--target-only", StringComparison.OrdinalIgnoreCase))
            {
                targetOnly = true;
                continue;
            }

            if (TryParseClientLauncherSafetyAuditEvidenceOption(argument, out var optionName, out var inlineValue))
            {
                var value = inlineValue;
                if (value is null)
                {
                    if (index + 1 >= args.Length)
                    {
                        error = $"Missing value for {optionName}.";
                        return false;
                    }

                    value = args[++index];
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    error = $"{optionName} requires a non-empty path.";
                    return false;
                }

                if (!TryCreateClientLauncherSafetyAuditEvidencePath(optionName, value, out var evidencePath, out error))
                {
                    return false;
                }

                evidencePaths.Add(evidencePath);
                continue;
            }

            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unknown client-launcher-safety-audit option: {argument}";
                return false;
            }

            if (!TryCreateClientLauncherSafetyAuditEvidencePath("--evidence-path", argument, out var positionalEvidencePath, out error))
            {
                return false;
            }

            evidencePaths.Add(positionalEvidencePath);
        }

        if (targetOnly && evidencePaths.Count == 0)
        {
            error = "--target-only requires at least one --target-path or evidence path.";
            return false;
        }

        options = new ClientLauncherSafetyAuditOptions(clientRoot, evidencePaths, targetOnly);
        return true;
    }

    private static bool TryParseClientLauncherSafetyAuditEvidenceOption(
        string argument,
        out string optionName,
        out string? inlineValue)
    {
        optionName = "";
        inlineValue = null;

        foreach (var candidate in ClientLauncherSafetyAuditEvidenceOptions)
        {
            if (string.Equals(argument, candidate, StringComparison.OrdinalIgnoreCase))
            {
                optionName = candidate;
                return true;
            }

            var prefix = candidate + "=";
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                optionName = candidate;
                inlineValue = argument[prefix.Length..];
                return true;
            }
        }

        return false;
    }

    private static bool TryCreateClientLauncherSafetyAuditEvidencePath(
        string optionName,
        string value,
        out ClientLauncherSafetyAuditEvidencePath evidencePath,
        out string error)
    {
        evidencePath = new ClientLauncherSafetyAuditEvidencePath("", "");
        error = "";

        try
        {
            evidencePath = new ClientLauncherSafetyAuditEvidencePath(
                GetClientLauncherSafetyAuditEvidenceLabel(optionName),
                Path.GetFullPath(value));
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"Invalid evidence path: {ex.Message}";
            return false;
        }
    }

    private static string GetClientLauncherSafetyAuditEvidenceLabel(string optionName)
    {
        return optionName.ToLowerInvariant() switch
        {
            "--target-path" => "target",
            "--live-evidence-path" => "live-evidence",
            "--quarantined-evidence-path" => "quarantined-evidence",
            _ => "evidence",
        };
    }

    private static ClientLauncherSafetyAuditReport CreateClientLauncherSafetyAuditReport(
        ClientLauncherSafetyAuditOptions options)
    {
        var targets = CreateClientLauncherSafetyAuditTargets(options);
        var fileReports = targets
            .Select(ScanClientLauncherSafetyAuditTarget)
            .ToArray();

        return new ClientLauncherSafetyAuditReport(options.ClientRoot, options.EvidencePaths, options.TargetOnly, fileReports);
    }

    private static IReadOnlyList<ClientLauncherSafetyAuditScanTarget> CreateClientLauncherSafetyAuditTargets(
        ClientLauncherSafetyAuditOptions options)
    {
        var targets = new List<ClientLauncherSafetyAuditScanTarget>();
        if (!options.TargetOnly)
        {
            foreach (var relativePath in ClientLauncherSafetyAuditRelativeFiles)
            {
                targets.Add(new ClientLauncherSafetyAuditScanTarget(
                    NormalizeClientLauncherSafetyAuditPath(relativePath),
                    Path.Combine(options.ClientRoot, relativePath.Replace('/', Path.DirectorySeparatorChar))));
            }
        }

        foreach (var evidencePath in options.EvidencePaths)
        {
            if (Directory.Exists(evidencePath.Path))
            {
                foreach (var filePath in EnumerateClientLauncherSafetyAuditEvidenceFiles(evidencePath.Path))
                {
                    var relativePath = Path.GetRelativePath(evidencePath.Path, filePath);
                    targets.Add(new ClientLauncherSafetyAuditScanTarget(
                        $"{evidencePath.Label}:{NormalizeClientLauncherSafetyAuditPath(relativePath)}",
                        filePath));
                }

                continue;
            }

            var fileName = Path.GetFileName(evidencePath.Path);
            targets.Add(new ClientLauncherSafetyAuditScanTarget(
                $"{evidencePath.Label}:{(fileName.Length == 0 ? evidencePath.Path : fileName)}",
                evidencePath.Path));
        }

        return targets;
    }

    private static IEnumerable<string> EnumerateClientLauncherSafetyAuditEvidenceFiles(string directoryPath)
    {
        return Directory
            .EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Take(ClientLauncherSafetyAuditMaxEvidenceDirectoryFiles);
    }

    private static ClientLauncherSafetyAuditFileReport ScanClientLauncherSafetyAuditTarget(
        ClientLauncherSafetyAuditScanTarget target)
    {
        if (!File.Exists(target.Path))
        {
            return new ClientLauncherSafetyAuditFileReport(target.Label, target.Path, false, 0, []);
        }

        var bytes = File.ReadAllBytes(target.Path);
        var lowerAsciiBytes = CreateClientLauncherSafetyAuditLowerAsciiBytes(bytes);
        var hits = ClientLauncherSafetyAuditIndicators
            .Select(indicator => new ClientLauncherSafetyAuditIndicatorHit(
                indicator.Name,
                indicator.Severity,
                CountClientLauncherSafetyAuditIndicator(lowerAsciiBytes, indicator.Name)))
            .Where(hit => hit.Count > 0)
            .ToArray();

        return new ClientLauncherSafetyAuditFileReport(
            target.Label,
            target.Path,
            true,
            bytes.LongLength,
            hits);
    }

    private static int CountClientLauncherSafetyAuditIndicator(byte[] lowerAsciiBytes, string indicator)
    {
        return CountClientLauncherSafetyAuditByteSequence(lowerAsciiBytes, Encoding.ASCII.GetBytes(indicator)) +
            CountClientLauncherSafetyAuditByteSequence(lowerAsciiBytes, Encoding.Unicode.GetBytes(indicator));
    }

    private static int CountClientLauncherSafetyAuditByteSequence(byte[] lowerAsciiBytes, byte[] sequence)
    {
        if (sequence.Length == 0 || lowerAsciiBytes.Length < sequence.Length)
        {
            return 0;
        }

        var lowerAsciiSequence = CreateClientLauncherSafetyAuditLowerAsciiBytes(sequence);
        var remaining = lowerAsciiBytes.AsSpan();
        var count = 0;
        while (true)
        {
            var index = remaining.IndexOf(lowerAsciiSequence);
            if (index < 0)
            {
                return count;
            }

            count++;
            remaining = remaining[(index + 1)..];
        }
    }

    private static byte[] CreateClientLauncherSafetyAuditLowerAsciiBytes(byte[] bytes)
    {
        var lowerAsciiBytes = new byte[bytes.Length];
        for (var index = 0; index < bytes.Length; index++)
        {
            lowerAsciiBytes[index] = ToClientLauncherSafetyAuditAsciiLower(bytes[index]);
        }

        return lowerAsciiBytes;
    }

    private static byte ToClientLauncherSafetyAuditAsciiLower(byte value)
    {
        return value >= 'A' && value <= 'Z'
            ? (byte)(value + 32)
            : value;
    }

    private static void PrintClientLauncherSafetyAuditReport(ClientLauncherSafetyAuditReport report)
    {
        Console.WriteLine("Client launcher safety audit:");
        Console.WriteLine($"Client root: {report.ClientRoot}");
        Console.WriteLine(report.TargetOnly
            ? "Mode: target-only read-only byte/string scan; no client executables launched."
            : "Mode: read-only byte/string scan; no client executables launched.");
        Console.WriteLine($"Evidence paths supplied: {report.EvidencePaths.Count}");
        foreach (var evidencePath in report.EvidencePaths)
        {
            Console.WriteLine($"  {evidencePath.Label}: {evidencePath.Path}");
        }

        Console.WriteLine($"Scan targets: {report.Files.Count}");
        Console.WriteLine($"High-risk hits: {report.HighRiskHitCount}");
        Console.WriteLine($"Known NetDragon driver marker hits: {report.KnownNetDragonDriverHitCount}");
        Console.WriteLine("Files:");
        foreach (var file in report.Files)
        {
            Console.WriteLine(
                $"  {file.Label}: exists={FormatClientLauncherSafetyAuditBool(file.Exists)} " +
                $"bytes={file.Length:N0} hitCount={file.HitCount:N0} " +
                $"highRiskHitCount={file.HighRiskHitCount:N0} " +
                $"indicators={FormatClientLauncherSafetyAuditIndicators(file.Hits)} " +
                $"path={file.Path}");
        }

        Console.WriteLine(report.HasHighRiskIndicators
            ? "Result: high-risk indicators found"
            : "Result: no high-risk indicators found");
    }

    private static string FormatClientLauncherSafetyAuditIndicators(
        IReadOnlyList<ClientLauncherSafetyAuditIndicatorHit> hits)
    {
        return hits.Count == 0
            ? "none"
            : string.Join(",", hits.Select(hit => $"{hit.Name}={hit.Count:N0}"));
    }

    private static string FormatClientLauncherSafetyAuditBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string NormalizeClientLauncherSafetyAuditPath(string path)
    {
        return path.Replace('\\', '/');
    }

    private sealed record ClientLauncherSafetyAuditOptions(
        string ClientRoot,
        IReadOnlyList<ClientLauncherSafetyAuditEvidencePath> EvidencePaths,
        bool TargetOnly);

    private sealed record ClientLauncherSafetyAuditEvidencePath(string Label, string Path);

    private sealed record ClientLauncherSafetyAuditScanTarget(string Label, string Path);

    private sealed record ClientLauncherSafetyAuditReport(
        string ClientRoot,
        IReadOnlyList<ClientLauncherSafetyAuditEvidencePath> EvidencePaths,
        bool TargetOnly,
        IReadOnlyList<ClientLauncherSafetyAuditFileReport> Files)
    {
        public int HighRiskHitCount => Files.Sum(file => file.HighRiskHitCount);

        public int KnownNetDragonDriverHitCount => Files.Sum(file => file.KnownNetDragonDriverHitCount);

        public bool HasHighRiskIndicators => HighRiskHitCount > 0;
    }

    private sealed record ClientLauncherSafetyAuditFileReport(
        string Label,
        string Path,
        bool Exists,
        long Length,
        IReadOnlyList<ClientLauncherSafetyAuditIndicatorHit> Hits)
    {
        public int HitCount => Hits.Sum(hit => hit.Count);

        public int HighRiskHitCount => Hits
            .Where(hit => hit.Severity == ClientLauncherSafetyAuditIndicatorSeverity.HighRisk)
            .Sum(hit => hit.Count);

        public int KnownNetDragonDriverHitCount => Hits
            .Where(hit => hit.Severity == ClientLauncherSafetyAuditIndicatorSeverity.KnownNetDragonDriver)
            .Sum(hit => hit.Count);
    }

    private sealed record ClientLauncherSafetyAuditIndicatorHit(
        string Name,
        ClientLauncherSafetyAuditIndicatorSeverity Severity,
        int Count);

    private sealed record ClientLauncherSafetyAuditIndicator(
        string Name,
        ClientLauncherSafetyAuditIndicatorSeverity Severity);

    private enum ClientLauncherSafetyAuditIndicatorSeverity
    {
        HighRisk,
        KnownNetDragonDriver,
    }
}

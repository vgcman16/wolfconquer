using System.Text.Json;

internal static partial class Program
{
    private const string DefaultCachedClientBranchFrontierModuleName = "DataThread.dll";
    private const string DefaultCachedClientBranchFrontierLicenseModuleName = "license.dll";
    private const int CachedClientBranchFrontierCandidateLimit = 8;
    private const int CachedClientBranchFrontierReportSchemaVersion = 1;
    private const int CachedClientBranchFrontierReportArtifactLimit = 64;
    private const int CachedClientBranchFrontierReportEvidenceLimit = 64;
    private const int CachedClientBranchFrontierReportAggregateTargetLimit = 64;
    private const int CachedClientBranchFrontierReportMaxBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions CachedClientBranchFrontierReportJsonOptions = new()
    {
        WriteIndented = true,
    };

    static int CachedClientBranchFrontierSummary(string[] args)
    {
        CachedClientBranchFrontierSummaryOptions options;
        try
        {
            options = ResolveCachedClientBranchFrontierSummaryOptions(args);
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            IOException or
            NotSupportedException or
            UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var summaries = options.JsonPaths
                .Select(ReadCachedClientBranchFrontierArtifact)
                .ToArray();
            var aggregate = BuildCachedClientBranchFrontierAggregate(summaries);

            if (options.OutputJsonPath is not null)
            {
                var report = BuildCachedClientBranchFrontierReport(summaries, aggregate);
                WriteCachedClientBranchFrontierReportAtomic(options.OutputJsonPath, report);
            }

            Console.WriteLine("Cached client branch frontier summary:");
            foreach (var summary in summaries)
            {
                Console.WriteLine($"Path: {summary.Path}");
                Console.WriteLine(
                    $"  Ready={summary.Ready} Accepted={summary.Accepted} RelayStatus={summary.RelayStatus} " +
                    $"RelayRequestSentBytes={summary.RelayRequestSentBytes} RelayResponseBytes={summary.RelayResponseBytes} " +
                    $"ResponseSentBytes={summary.ResponseSentBytes} ProcessRespondingAfterObserve={summary.ProcessRespondingAfterObserve} " +
                    $"WindowCount={summary.WindowCount} ModuleProbeCount={summary.ModuleProbeCount} " +
                    $"HotInstructionRva={summary.HotInstructionRva}");
                Console.WriteLine($"  BranchTargetRvas={FormatEvidenceMapValues(summary.BranchTargetRvas)}");
            }

            Console.WriteLine("Aggregate:");
            Console.WriteLine($"  RecurrentBranchTargetRvas={FormatCachedClientBranchTargetCounts(aggregate.RecurrentBranchTargetRvas)}");
            Console.WriteLine($"  NextProbeCandidates={FormatCachedClientBranchNextProbeCandidates(aggregate.NextProbeCandidates)}");
            Console.WriteLine($"  RecurrentTargetsExhausted={aggregate.RecurrentTargetsExhausted}");
            Console.WriteLine($"  ExhaustedRecurrentTargets={FormatCachedClientBranchTargetCounts(aggregate.ExhaustedRecurrentTargets)}");
            Console.WriteLine($"  FallbackDisplayTargets={FormatCachedClientBranchNextProbeCandidates(aggregate.FallbackDisplayTargets)}");
            if (options.OutputJsonPath is not null)
            {
                Console.WriteLine($"Output JSON: {options.OutputJsonPath}");
            }

            return 0;
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            IOException or
            JsonException or
            InvalidDataException or
            FormatException or
            NotSupportedException or
            UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Cached client branch frontier summary failed: {ex.Message}");
            return 1;
        }
    }

    static CachedClientBranchFrontierSummaryOptions ResolveCachedClientBranchFrontierSummaryOptions(string[] args)
    {
        var jsonPaths = new List<string>();
        string? outputJsonPath = null;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--output-json", StringComparison.OrdinalIgnoreCase))
            {
                if (outputJsonPath is not null)
                {
                    throw new ArgumentException("--output-json may only be specified once.");
                }

                index++;
                if (index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Missing value for --output-json.");
                }

                outputJsonPath = ResolveCachedClientBranchFrontierPath(args[index], "Output JSON");
                continue;
            }

            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Unknown cached-client-branch-frontier-summary option: {argument}");
            }

            jsonPaths.Add(ResolveCachedClientBranchFrontierPath(argument, "Patch result JSON"));
        }

        if (jsonPaths.Count == 0)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool cached-client-branch-frontier-summary " +
                "<patchResultJsonPath> [patchResultJsonPath ...] [--output-json path]");
        }

        if (jsonPaths.Count > CachedClientBranchFrontierReportArtifactLimit)
        {
            throw new ArgumentException(
                $"At most {CachedClientBranchFrontierReportArtifactLimit} patch result JSON paths may be summarized.");
        }

        foreach (var jsonPath in jsonPaths)
        {
            if (!File.Exists(jsonPath))
            {
                throw new ArgumentException($"Patch result JSON not found: {jsonPath}");
            }
        }

        if (outputJsonPath is not null)
        {
            if (Directory.Exists(outputJsonPath))
            {
                throw new ArgumentException($"Output JSON path is a directory: {outputJsonPath}");
            }

            var outputDirectory = Path.GetDirectoryName(outputJsonPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException($"Output JSON path has no parent directory: {outputJsonPath}");
            }

            if (File.Exists(outputDirectory))
            {
                throw new ArgumentException($"Output JSON parent is not a directory: {outputDirectory}");
            }

            if (jsonPaths.Contains(outputJsonPath, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Output JSON path must not overwrite an input patch result JSON.");
            }
        }

        return new CachedClientBranchFrontierSummaryOptions(jsonPaths, outputJsonPath);
    }

    static string ResolveCachedClientBranchFrontierPath(string value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{description} path must not be empty.");
        }

        var fullPath = Path.GetFullPath(value);
        if (string.IsNullOrWhiteSpace(Path.GetFileName(fullPath)))
        {
            throw new ArgumentException($"{description} path must name a file: {fullPath}");
        }

        return fullPath;
    }

    static CachedClientBranchFrontierArtifactSummary ReadCachedClientBranchFrontierArtifact(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var payload = document.RootElement.TryGetProperty("Result", out var result)
            ? result
            : document.RootElement;

        var modules = ReadCachedClientThreadModules(payload);
        var probes = ReadCachedClientModuleMemoryProbes(payload);
        var threadEvidence = TryBuildCachedClientBranchThreadEvidence(path, payload, modules, probes);
        var branchTargetRvas = ReadCachedClientBranchTargetRvas(
            modules,
            probes,
            DefaultCachedClientBranchFrontierModuleName);
        var probedRvas = ReadCachedClientProbedModuleRvas(
            probes,
            DefaultCachedClientBranchFrontierModuleName);

        return new CachedClientBranchFrontierArtifactSummary(
            path,
            threadEvidence.Ready,
            ReadCachedClientBranchOptionalBool(payload, "Accepted"),
            ReadCachedClientBranchOptionalScalar(payload, "RelayStatus"),
            ReadCachedClientBranchOptionalScalar(payload, "RelayRequestSentBytes"),
            ReadCachedClientBranchOptionalScalar(payload, "RelayResponseBytes"),
            ReadCachedClientBranchOptionalScalar(payload, "ResponseSentBytes"),
            ReadCachedClientBranchOptionalBool(payload, "ProcessRespondingAfterObserve"),
            CountArrayProperty(payload, "WindowEvidenceAfterObserve"),
            probes.Count,
            threadEvidence.HotInstructionRva,
            branchTargetRvas,
            probedRvas);
    }

    static CachedClientBranchThreadEvidenceSummary TryBuildCachedClientBranchThreadEvidence(
        string path,
        JsonElement payload,
        IReadOnlyList<CachedClientThreadModule> modules,
        IReadOnlyList<CachedClientModuleMemoryProbeRow> probes)
    {
        if (!payload.TryGetProperty("ThreadEvidenceAfterObserve", out var threadsElement) ||
            threadsElement.ValueKind != JsonValueKind.Array)
        {
            return new CachedClientBranchThreadEvidenceSummary(false, "<missing>");
        }

        var selectedThread = ReadCachedClientThreadRows(payload)
            .Where(thread =>
                !string.IsNullOrWhiteSpace(thread.InstructionBytes) &&
                string.Equals(
                    thread.InstructionModuleName,
                    DefaultCachedClientBranchFrontierModuleName,
                    StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(thread => thread.TotalProcessorTimeMs)
            .FirstOrDefault();
        if (selectedThread is null)
        {
            return new CachedClientBranchThreadEvidenceSummary(false, "<missing>");
        }

        var evidence = new CachedClientThreadEvidence(
            path,
            ReadOptionalInt(payload, "ProcessId") ?? selectedThread.ProcessId,
            modules,
            selectedThread,
            modules,
            probes);
        var map = BuildCachedClientThreadEvidenceMap(
            evidence,
            DefaultCachedClientBranchFrontierLicenseModuleName);

        return new CachedClientBranchThreadEvidenceSummary(
            map.Ready,
            FormatOptionalText(selectedThread.InstructionRva));
    }

    static IReadOnlyList<string> ReadCachedClientBranchTargetRvas(
        IReadOnlyList<CachedClientThreadModule> modules,
        IReadOnlyList<CachedClientModuleMemoryProbeRow> probes,
        string moduleName)
    {
        return probes
            .Where(probe =>
                probe.Read &&
                !string.IsNullOrWhiteSpace(probe.Hex) &&
                string.Equals(probe.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(probe => ReadCachedClientModuleProbeBranchTargetRvas(probe, modules))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    static IReadOnlyList<string> ReadCachedClientModuleProbeBranchTargetRvas(
        CachedClientModuleMemoryProbeRow probe,
        IReadOnlyList<CachedClientThreadModule> modules)
    {
        var map = BuildCachedClientModuleMemoryProbeMap(probe, modules);
        var startAddress = ParseHexInt64(map.Address, "ProbeAddress");
        return map.Instructions
            .Where(instruction =>
                instruction.Kind == CachedClientThreadInstructionKind.Jump &&
                ParseHexInt64(instruction.Address, "InstructionAddress") - startAddress <= 0x20)
            .Select(FormatNormalizedCachedClientModuleProbeTarget)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    static IReadOnlyList<string> ReadCachedClientProbedModuleRvas(
        IReadOnlyList<CachedClientModuleMemoryProbeRow> probes,
        string moduleName)
    {
        return probes
            .Where(probe =>
                !string.IsNullOrWhiteSpace(probe.Rva) &&
                string.Equals(probe.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
            .Select(probe => TryFormatCachedClientModuleRva(probe.ModuleName, probe.Rva))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    static string TryFormatCachedClientModuleRva(string moduleName, string rva)
    {
        try
        {
            return $"{moduleName}+{FormatHexAddress(ParseHexInt64(rva, "Rva"))}";
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    static CachedClientBranchFrontierAggregate BuildCachedClientBranchFrontierAggregate(
        IReadOnlyList<CachedClientBranchFrontierArtifactSummary> summaries)
    {
        var targetCounts = summaries
            .SelectMany(summary => summary.BranchTargetRvas
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(target => new { Target = target }))
            .GroupBy(item => item.Target, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CachedClientBranchTargetCount(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Target, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var recurrent = targetCounts
            .Where(item => item.Count > 1)
            .ToArray();
        var probedRvas = summaries
            .SelectMany(summary => summary.ProbedRvas)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unprobedRecurrentTargets = recurrent
            .Where(item => !probedRvas.Contains(item.Target))
            .ToArray();
        var exhaustedRecurrentTargets = recurrent
            .Where(item => probedRvas.Contains(item.Target))
            .ToArray();
        var nextProbeCandidates = unprobedRecurrentTargets
            .Take(CachedClientBranchFrontierCandidateLimit)
            .ToArray();
        IReadOnlyList<CachedClientBranchTargetCount> fallbackDisplayTargets = [];
        if (nextProbeCandidates.Length == 0 && recurrent.Length > 0)
        {
            fallbackDisplayTargets = recurrent
                .Take(CachedClientBranchFrontierCandidateLimit)
                .ToArray();
        }
        else if (nextProbeCandidates.Length == 0)
        {
            fallbackDisplayTargets = targetCounts
                .Where(item => !probedRvas.Contains(item.Target))
                .Take(CachedClientBranchFrontierCandidateLimit)
                .ToArray();
        }

        if (nextProbeCandidates.Length == 0 && fallbackDisplayTargets.Count == 0)
        {
            fallbackDisplayTargets = targetCounts
                .Take(CachedClientBranchFrontierCandidateLimit)
                .ToArray();
        }

        return new CachedClientBranchFrontierAggregate(
            targetCounts,
            recurrent,
            unprobedRecurrentTargets,
            exhaustedRecurrentTargets,
            nextProbeCandidates,
            fallbackDisplayTargets);
    }

    static CachedClientBranchFrontierReport BuildCachedClientBranchFrontierReport(
        IReadOnlyList<CachedClientBranchFrontierArtifactSummary> summaries,
        CachedClientBranchFrontierAggregate aggregate)
    {
        return new CachedClientBranchFrontierReport(
            CachedClientBranchFrontierReportSchemaVersion,
            new CachedClientBranchFrontierReportLimits(
                CachedClientBranchFrontierReportArtifactLimit,
                CachedClientBranchFrontierReportEvidenceLimit,
                CachedClientBranchFrontierReportAggregateTargetLimit,
                CachedClientBranchFrontierCandidateLimit,
                CachedClientBranchFrontierReportMaxBytes),
            summaries.Count,
            summaries
                .Select(summary => new CachedClientBranchFrontierArtifactEvidence(
                    summary.Path,
                    summary.Ready,
                    summary.Accepted,
                    summary.RelayStatus,
                    summary.RelayRequestSentBytes,
                    summary.RelayResponseBytes,
                    summary.ResponseSentBytes,
                    summary.ProcessRespondingAfterObserve,
                    summary.WindowCount,
                    summary.ModuleProbeCount,
                    summary.HotInstructionRva,
                    summary.BranchTargetRvas.Count,
                    summary.BranchTargetRvas
                        .Take(CachedClientBranchFrontierReportEvidenceLimit)
                        .ToArray(),
                    summary.BranchTargetRvas.Count > CachedClientBranchFrontierReportEvidenceLimit,
                    summary.ProbedRvas.Count,
                    summary.ProbedRvas
                        .Take(CachedClientBranchFrontierReportEvidenceLimit)
                        .ToArray(),
                    summary.ProbedRvas.Count > CachedClientBranchFrontierReportEvidenceLimit))
                .ToArray(),
            new CachedClientBranchFrontierAggregateEvidence(
                aggregate.BranchTargetRvas.Count,
                aggregate.RecurrentBranchTargetRvas.Count,
                aggregate.RecurrentBranchTargetRvas
                    .Take(CachedClientBranchFrontierReportAggregateTargetLimit)
                    .ToArray(),
                aggregate.RecurrentBranchTargetRvas.Count > CachedClientBranchFrontierReportAggregateTargetLimit,
                aggregate.UnprobedRecurrentTargets.Count,
                aggregate.NextProbeCandidates,
                aggregate.UnprobedRecurrentTargets.Count > aggregate.NextProbeCandidates.Count,
                aggregate.ExhaustedRecurrentTargets.Count,
                aggregate.ExhaustedRecurrentTargets
                    .Take(CachedClientBranchFrontierReportAggregateTargetLimit)
                    .ToArray(),
                aggregate.ExhaustedRecurrentTargets.Count > CachedClientBranchFrontierReportAggregateTargetLimit,
                aggregate.RecurrentTargetsExhausted,
                ResolveCachedClientBranchCandidateStatus(aggregate),
                aggregate.FallbackDisplayTargets));
    }

    static string ResolveCachedClientBranchCandidateStatus(CachedClientBranchFrontierAggregate aggregate)
    {
        if (aggregate.NextProbeCandidates.Count > 0)
        {
            return "unprobed-recurrent-candidates";
        }

        if (aggregate.RecurrentTargetsExhausted)
        {
            return "recurrent-targets-exhausted";
        }

        return aggregate.BranchTargetRvas.Count == 0
            ? "no-branch-targets"
            : "no-recurrent-targets";
    }

    static void WriteCachedClientBranchFrontierReportAtomic(
        string path,
        CachedClientBranchFrontierReport report)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            report,
            CachedClientBranchFrontierReportJsonOptions);
        if (bytes.Length + 1 > CachedClientBranchFrontierReportMaxBytes)
        {
            throw new InvalidDataException(
                $"Cached client branch frontier report exceeds the {CachedClientBranchFrontierReportMaxBytes}-byte limit.");
        }

        var directory = Path.GetDirectoryName(path)
            ?? throw new ArgumentException($"Output JSON path has no parent directory: {path}");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $".cached-client-branch-frontier-{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.WriteByte((byte)'\n');
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    static string ReadCachedClientBranchOptionalBool(JsonElement payload, string name)
    {
        if (payload.TryGetProperty(name, out var value))
        {
            return FormatOptionalBool(ReadOptionalBool(payload, name));
        }

        if (TryReadCachedClientAcceptedConnectionScalar(payload, name, out var nestedValue))
        {
            return nestedValue.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? nestedValue.GetBoolean().ToString()
                : FormatOptionalText(nestedValue.ToString());
        }

        return "<missing>";
    }

    static string ReadCachedClientBranchOptionalScalar(JsonElement payload, string name)
    {
        var value = ReadOptionalString(payload, name);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return TryReadCachedClientAcceptedConnectionScalar(payload, name, out var nestedValue)
            ? FormatOptionalText(nestedValue.ValueKind == JsonValueKind.String ? nestedValue.GetString() ?? string.Empty : nestedValue.ToString())
            : "<missing>";
    }

    static bool TryReadCachedClientAcceptedConnectionScalar(
        JsonElement payload,
        string name,
        out JsonElement value)
    {
        value = default;
        if (!payload.TryGetProperty("AcceptedConnections", out var acceptedConnections) ||
            acceptedConnections.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var connection in acceptedConnections.EnumerateArray())
        {
            if (connection.ValueKind == JsonValueKind.Object &&
                connection.TryGetProperty(name, out value))
            {
                return true;
            }
        }

        return false;
    }

    static string FormatCachedClientBranchTargetCounts(IReadOnlyList<CachedClientBranchTargetCount> counts)
    {
        return counts.Count == 0
            ? "<none>"
            : string.Join("; ", counts.Select(count => $"{count.Target} count={count.Count}"));
    }

    static string FormatCachedClientBranchNextProbeCandidates(IReadOnlyList<CachedClientBranchTargetCount> candidates)
    {
        return candidates.Count == 0
            ? "<none>"
            : string.Join(", ", candidates.Select(candidate => $"{candidate.Target} count={candidate.Count}"));
    }
}

internal sealed record CachedClientBranchFrontierSummaryOptions(
    IReadOnlyList<string> JsonPaths,
    string? OutputJsonPath);

internal sealed record CachedClientBranchThreadEvidenceSummary(
    bool Ready,
    string HotInstructionRva);

internal sealed record CachedClientBranchFrontierArtifactSummary(
    string Path,
    bool Ready,
    string Accepted,
    string RelayStatus,
    string RelayRequestSentBytes,
    string RelayResponseBytes,
    string ResponseSentBytes,
    string ProcessRespondingAfterObserve,
    int WindowCount,
    int ModuleProbeCount,
    string HotInstructionRva,
    IReadOnlyList<string> BranchTargetRvas,
    IReadOnlyList<string> ProbedRvas);

internal sealed record CachedClientBranchFrontierAggregate(
    IReadOnlyList<CachedClientBranchTargetCount> BranchTargetRvas,
    IReadOnlyList<CachedClientBranchTargetCount> RecurrentBranchTargetRvas,
    IReadOnlyList<CachedClientBranchTargetCount> UnprobedRecurrentTargets,
    IReadOnlyList<CachedClientBranchTargetCount> ExhaustedRecurrentTargets,
    IReadOnlyList<CachedClientBranchTargetCount> NextProbeCandidates,
    IReadOnlyList<CachedClientBranchTargetCount> FallbackDisplayTargets)
{
    public bool RecurrentTargetsExhausted =>
        RecurrentBranchTargetRvas.Count > 0 && UnprobedRecurrentTargets.Count == 0;
}

internal sealed record CachedClientBranchTargetCount(
    string Target,
    int Count);

internal sealed record CachedClientBranchFrontierReport(
    int SchemaVersion,
    CachedClientBranchFrontierReportLimits Limits,
    int ArtifactCount,
    IReadOnlyList<CachedClientBranchFrontierArtifactEvidence> Artifacts,
    CachedClientBranchFrontierAggregateEvidence Aggregate);

internal sealed record CachedClientBranchFrontierReportLimits(
    int MaximumArtifacts,
    int MaximumEvidenceValuesPerArtifact,
    int MaximumAggregateTargets,
    int MaximumNextProbeCandidates,
    int MaximumSerializedBytes);

internal sealed record CachedClientBranchFrontierArtifactEvidence(
    string Path,
    bool Ready,
    string Accepted,
    string RelayStatus,
    string RelayRequestSentBytes,
    string RelayResponseBytes,
    string ResponseSentBytes,
    string ProcessRespondingAfterObserve,
    int WindowCount,
    int ModuleProbeCount,
    string HotInstructionRva,
    int BranchTargetRvaCount,
    IReadOnlyList<string> BranchTargetRvas,
    bool BranchTargetRvasTruncated,
    int ProbedRvaCount,
    IReadOnlyList<string> ProbedRvas,
    bool ProbedRvasTruncated);

internal sealed record CachedClientBranchFrontierAggregateEvidence(
    int BranchTargetRvaCount,
    int RecurrentBranchTargetRvaCount,
    IReadOnlyList<CachedClientBranchTargetCount> RecurrentBranchTargetRvas,
    bool RecurrentBranchTargetRvasTruncated,
    int UnprobedRecurrentTargetCount,
    IReadOnlyList<CachedClientBranchTargetCount> UnprobedRecurrentCandidates,
    bool UnprobedRecurrentCandidatesTruncated,
    int ExhaustedRecurrentTargetCount,
    IReadOnlyList<CachedClientBranchTargetCount> ExhaustedRecurrentTargets,
    bool ExhaustedRecurrentTargetsTruncated,
    bool RecurrentTargetsExhausted,
    string CandidateStatus,
    IReadOnlyList<CachedClientBranchTargetCount> FallbackDisplayTargets);

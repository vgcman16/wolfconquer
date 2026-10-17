using System.Globalization;
using System.Text.Json;

internal static partial class Program
{
    private const string CachedClientModuleProbesAfterObserveField = "ModuleMemoryProbesAfterObserve";
    private const string CachedClientModuleProbesBeforeRelayResponseField = "ModuleMemoryProbesBeforeRelayResponse";
    private const int CachedClientModuleProbeCompareMaxArtifactBytes = 64 * 1024 * 1024;
    private const int CachedClientModuleProbeCompareMaxProbeCount = 4096;
    private const int CachedClientModuleProbeCompareMaxProbeBytes = 4096;

    static int CachedClientModuleProbeCompare(string[] args)
    {
        CachedClientModuleProbeCompareOptions options;
        try
        {
            options = ResolveCachedClientModuleProbeCompareOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var left = ReadCachedClientModuleProbeCompareEvidence(options.LeftJsonPath, options.LeftProbeFieldName);
            var right = ReadCachedClientModuleProbeCompareEvidence(options.RightJsonPath, options.RightProbeFieldName);
            var result = CompareCachedClientModuleProbes(left, right);

            Console.WriteLine("Cached client module probe compare:");
            Console.WriteLine($"Left: {left.Path}");
            Console.WriteLine($"Left field: {left.ProbeFieldName}");
            Console.WriteLine($"Right: {right.Path}");
            Console.WriteLine($"Right field: {right.ProbeFieldName}");
            Console.WriteLine($"Common probes: {result.CommonProbeCount}");
            Console.WriteLine($"Only left: {result.LeftOnlyProbeCount}");
            Console.WriteLine($"Only right: {result.RightOnlyProbeCount}");
            foreach (var row in result.Rows)
            {
                Console.WriteLine(
                    $"Probe: {row.ModuleName}+{row.Rva} " +
                    $"leftName={FormatNullableText(row.LeftName)} rightName={FormatNullableText(row.RightName)} " +
                    $"SameHex={row.SameHex} " +
                    $"FirstDifferingByteOffset={FormatCachedClientModuleProbeDifferenceOffset(row.FirstDifferingByteOffset)} " +
                    $"SameLeadingBranchTargets={row.SameLeadingBranchTargets} " +
                    $"LeftTargets={FormatEvidenceMapValues(row.LeftLeadingBranchTargets)} " +
                    $"RightTargets={FormatEvidenceMapValues(row.RightLeadingBranchTargets)}");
            }

            return 0;
        }
        catch (Exception ex) when (
            ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException or
                FormatException or OverflowException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Cached client module probe compare failed: {ex.Message}");
            return 1;
        }
    }

    static CachedClientModuleProbeCompareOptions ResolveCachedClientModuleProbeCompareOptions(string[] args)
    {
        if (args.Length < 3)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool cached-client-module-probe-compare " +
                "<leftPatchResultJsonPath> <rightPatchResultJsonPath> " +
                "[--left-field field] [--right-field field]");
        }

        var leftProbeFieldName = CachedClientModuleProbesAfterObserveField;
        var rightProbeFieldName = CachedClientModuleProbesAfterObserveField;
        var leftFieldSpecified = false;
        var rightFieldSpecified = false;
        for (var index = 3; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--left-field", StringComparison.OrdinalIgnoreCase))
            {
                if (leftFieldSpecified)
                {
                    throw new ArgumentException("Duplicate cached-client-module-probe-compare option: --left-field");
                }

                leftProbeFieldName = ResolveCachedClientModuleProbeFieldName(
                    ReadCachedClientModuleProbeCompareOptionValue(args, ref index));
                leftFieldSpecified = true;
                continue;
            }

            if (string.Equals(args[index], "--right-field", StringComparison.OrdinalIgnoreCase))
            {
                if (rightFieldSpecified)
                {
                    throw new ArgumentException("Duplicate cached-client-module-probe-compare option: --right-field");
                }

                rightProbeFieldName = ResolveCachedClientModuleProbeFieldName(
                    ReadCachedClientModuleProbeCompareOptionValue(args, ref index));
                rightFieldSpecified = true;
                continue;
            }

            throw new ArgumentException($"Unknown cached-client-module-probe-compare option: {args[index]}");
        }

        return new CachedClientModuleProbeCompareOptions(
            Path.GetFullPath(args[1]),
            Path.GetFullPath(args[2]),
            leftProbeFieldName,
            rightProbeFieldName);
    }

    static string ReadCachedClientModuleProbeCompareOptionValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for cached-client-module-probe-compare option: {args[index]}");
        }

        return args[++index];
    }

    static string ResolveCachedClientModuleProbeFieldName(string value)
    {
        if (string.Equals(value, CachedClientModuleProbesAfterObserveField, StringComparison.OrdinalIgnoreCase))
        {
            return CachedClientModuleProbesAfterObserveField;
        }

        if (string.Equals(value, CachedClientModuleProbesBeforeRelayResponseField, StringComparison.OrdinalIgnoreCase))
        {
            return CachedClientModuleProbesBeforeRelayResponseField;
        }

        throw new ArgumentException(
            $"Unknown cached-client-module-probe-compare probe field: {value}. " +
            $"Allowed fields: {CachedClientModuleProbesBeforeRelayResponseField}, {CachedClientModuleProbesAfterObserveField}");
    }

    static CachedClientModuleProbeCompareEvidence ReadCachedClientModuleProbeCompareEvidence(
        string path,
        string probeFieldName)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);
        if (stream.Length > CachedClientModuleProbeCompareMaxArtifactBytes)
        {
            throw new InvalidDataException(
                $"Cached client module probe artifact exceeds the {CachedClientModuleProbeCompareMaxArtifactBytes}-byte limit: {path}");
        }

        using var document = JsonDocument.Parse(
            stream,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"Cached client module probe artifact root is not an object: {path}");
        }

        var payload = document.RootElement;
        if (payload.TryGetProperty("Result", out var result))
        {
            if (result.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"Cached client module probe artifact Result is not an object: {path}");
            }

            payload = result;
        }

        var modules = ReadCachedClientThreadModules(payload);
        var probes = ReadCachedClientModuleMemoryProbesForCompare(payload, probeFieldName)
            .Where(probe =>
                probe.Read &&
                !string.IsNullOrWhiteSpace(probe.ModuleName) &&
                !string.IsNullOrWhiteSpace(probe.Rva) &&
                !string.IsNullOrWhiteSpace(probe.Hex))
            .Select(probe => BuildCachedClientModuleProbeCompareItem(probe, modules))
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return new CachedClientModuleProbeCompareEvidence(path, probeFieldName, probes);
    }

    static IReadOnlyList<CachedClientModuleMemoryProbeRow> ReadCachedClientModuleMemoryProbesForCompare(
        JsonElement payload,
        string probeFieldName)
    {
        if (!payload.TryGetProperty(probeFieldName, out var probesElement) ||
            probesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"{probeFieldName} is missing or not an array.");
        }

        if (probesElement.GetArrayLength() > CachedClientModuleProbeCompareMaxProbeCount)
        {
            throw new InvalidDataException(
                $"{probeFieldName} exceeds the {CachedClientModuleProbeCompareMaxProbeCount}-probe limit.");
        }

        var probes = new List<CachedClientModuleMemoryProbeRow>();
        var index = 0;
        foreach (var probeElement in probesElement.EnumerateArray())
        {
            if (probeElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"{probeFieldName}[{index}] is not an object.");
            }

            ValidateCachedClientModuleProbeCompareStringProperty(probeElement, probeFieldName, index, "Name");
            ValidateCachedClientModuleProbeCompareStringProperty(probeElement, probeFieldName, index, "ModuleName");
            ValidateCachedClientModuleProbeCompareStringProperty(probeElement, probeFieldName, index, "Rva");
            ValidateCachedClientModuleProbeCompareStringProperty(probeElement, probeFieldName, index, "Address");
            ValidateCachedClientModuleProbeCompareStringProperty(probeElement, probeFieldName, index, "Hex");
            ValidateCachedClientModuleProbeCompareStringProperty(probeElement, probeFieldName, index, "FailureReason");

            var length = ReadCachedClientModuleProbeCompareLength(probeElement, probeFieldName, index);
            var read = ReadCachedClientModuleProbeCompareRead(probeElement, probeFieldName, index);
            var hex = ReadOptionalString(probeElement, "Hex");
            if (hex.Count(character => !char.IsWhiteSpace(character)) > CachedClientModuleProbeCompareMaxProbeBytes * 2)
            {
                throw new InvalidDataException(
                    $"{probeFieldName}[{index}].Hex exceeds the {CachedClientModuleProbeCompareMaxProbeBytes}-byte limit.");
            }

            var probe = new CachedClientModuleMemoryProbeRow(
                ReadOptionalString(probeElement, "Name"),
                ReadOptionalString(probeElement, "ModuleName"),
                ReadOptionalString(probeElement, "Rva"),
                ReadOptionalString(probeElement, "Address"),
                length,
                read,
                hex,
                ReadOptionalString(probeElement, "FailureReason"));
            if (probe.Read &&
                (string.IsNullOrWhiteSpace(probe.ModuleName) ||
                    string.IsNullOrWhiteSpace(probe.Rva) ||
                    string.IsNullOrWhiteSpace(probe.Hex)))
            {
                throw new InvalidDataException(
                    $"{probeFieldName}[{index}] is marked Read but lacks ModuleName, Rva, or Hex.");
            }

            probes.Add(probe);
            index++;
        }

        return probes;
    }

    static void ValidateCachedClientModuleProbeCompareStringProperty(
        JsonElement probeElement,
        string probeFieldName,
        int index,
        string propertyName)
    {
        if (probeElement.TryGetProperty(propertyName, out var value) &&
            value.ValueKind is not JsonValueKind.String and not JsonValueKind.Null)
        {
            throw new InvalidDataException($"{probeFieldName}[{index}].{propertyName} is not a string.");
        }
    }

    static int ReadCachedClientModuleProbeCompareLength(
        JsonElement probeElement,
        string probeFieldName,
        int index)
    {
        if (!probeElement.TryGetProperty("Length", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return 0;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var length) || length < 0)
        {
            throw new InvalidDataException($"{probeFieldName}[{index}].Length is not a non-negative integer.");
        }

        return length;
    }

    static bool ReadCachedClientModuleProbeCompareRead(
        JsonElement probeElement,
        string probeFieldName,
        int index)
    {
        if (!probeElement.TryGetProperty("Read", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException($"{probeFieldName}[{index}].Read is not a Boolean."),
        };
    }

    static CachedClientModuleProbeCompareItem BuildCachedClientModuleProbeCompareItem(
        CachedClientModuleMemoryProbeRow probe,
        IReadOnlyList<CachedClientThreadModule> modules)
    {
        var map = BuildCachedClientModuleMemoryProbeMap(probe, modules);
        var startAddress = ParseHexInt64(map.Address, "ProbeAddress");
        var normalizedLeadingTargets = map.Instructions
            .Where(instruction =>
                instruction.Kind == CachedClientThreadInstructionKind.Jump &&
                ParseHexInt64(instruction.Address, "InstructionAddress") - startAddress <= 0x20)
            .Select(FormatNormalizedCachedClientModuleProbeTarget)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var normalizedRva = FormatHexAddress(ParseHexInt64(probe.Rva, "Rva"));
        var hexBytes = ParseHexBytes(probe.Hex);
        if (hexBytes.Count > CachedClientModuleProbeCompareMaxProbeBytes)
        {
            throw new InvalidDataException(
                $"Probe {probe.ModuleName}+{normalizedRva} exceeds the {CachedClientModuleProbeCompareMaxProbeBytes}-byte limit.");
        }

        return new CachedClientModuleProbeCompareItem(
            MakeCachedClientModuleProbeCompareKey(probe.ModuleName, normalizedRva),
            probe.Name,
            probe.ModuleName,
            normalizedRva,
            hexBytes,
            normalizedLeadingTargets);
    }

    static CachedClientModuleProbeCompareResult CompareCachedClientModuleProbes(
        CachedClientModuleProbeCompareEvidence left,
        CachedClientModuleProbeCompareEvidence right)
    {
        var commonKeys = left.Probes.Keys
            .Intersect(right.Probes.Keys, StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rows = commonKeys
            .Select(key =>
            {
                var leftProbe = left.Probes[key];
                var rightProbe = right.Probes[key];
                var firstDifferingByteOffset = FindFirstCachedClientModuleProbeDifferenceOffset(
                    leftProbe.HexBytes,
                    rightProbe.HexBytes);
                return new CachedClientModuleProbeCompareRow(
                    leftProbe.ModuleName,
                    leftProbe.Rva,
                    leftProbe.Name,
                    rightProbe.Name,
                    !firstDifferingByteOffset.HasValue,
                    firstDifferingByteOffset,
                    CachedClientStringListsEqual(leftProbe.NormalizedLeadingBranchTargets, rightProbe.NormalizedLeadingBranchTargets),
                    leftProbe.NormalizedLeadingBranchTargets,
                    rightProbe.NormalizedLeadingBranchTargets);
            })
            .ToArray();

        var leftOnlyProbeCount = left.Probes.Keys
            .Except(right.Probes.Keys, StringComparer.OrdinalIgnoreCase)
            .Count();
        var rightOnlyProbeCount = right.Probes.Keys
            .Except(left.Probes.Keys, StringComparer.OrdinalIgnoreCase)
            .Count();

        return new CachedClientModuleProbeCompareResult(
            rows.Length,
            leftOnlyProbeCount,
            rightOnlyProbeCount,
            rows);
    }

    static bool CachedClientStringListsEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        return left.Count == right.Count &&
            left.Zip(right).All(pair => string.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase));
    }

    static int? FindFirstCachedClientModuleProbeDifferenceOffset(
        IReadOnlyList<byte> left,
        IReadOnlyList<byte> right)
    {
        var commonByteCount = Math.Min(left.Count, right.Count);
        for (var offset = 0; offset < commonByteCount; offset++)
        {
            if (left[offset] != right[offset])
            {
                return offset;
            }
        }

        return left.Count == right.Count ? null : commonByteCount;
    }

    static string FormatCachedClientModuleProbeDifferenceOffset(int? offset)
    {
        return offset?.ToString(CultureInfo.InvariantCulture) ?? "<none>";
    }

    static string MakeCachedClientModuleProbeCompareKey(string moduleName, string rva)
    {
        return $"{moduleName.Trim()}|{rva}";
    }

    static string FormatNormalizedCachedClientModuleProbeTarget(CachedClientThreadInstruction instruction)
    {
        if (!string.IsNullOrWhiteSpace(instruction.TargetModuleName) &&
            !string.IsNullOrWhiteSpace(instruction.TargetModuleRva))
        {
            return $"{instruction.TargetModuleName}+{instruction.TargetModuleRva}";
        }

        return instruction.TargetText;
    }
}

internal sealed record CachedClientModuleProbeCompareOptions(
    string LeftJsonPath,
    string RightJsonPath,
    string LeftProbeFieldName,
    string RightProbeFieldName);

internal sealed record CachedClientModuleProbeCompareEvidence(
    string Path,
    string ProbeFieldName,
    IReadOnlyDictionary<string, CachedClientModuleProbeCompareItem> Probes);

internal sealed record CachedClientModuleProbeCompareItem(
    string Key,
    string Name,
    string ModuleName,
    string Rva,
    IReadOnlyList<byte> HexBytes,
    IReadOnlyList<string> NormalizedLeadingBranchTargets);

internal sealed record CachedClientModuleProbeCompareResult(
    int CommonProbeCount,
    int LeftOnlyProbeCount,
    int RightOnlyProbeCount,
    IReadOnlyList<CachedClientModuleProbeCompareRow> Rows);

internal sealed record CachedClientModuleProbeCompareRow(
    string ModuleName,
    string Rva,
    string LeftName,
    string RightName,
    bool SameHex,
    int? FirstDifferingByteOffset,
    bool SameLeadingBranchTargets,
    IReadOnlyList<string> LeftLeadingBranchTargets,
    IReadOnlyList<string> RightLeadingBranchTargets);

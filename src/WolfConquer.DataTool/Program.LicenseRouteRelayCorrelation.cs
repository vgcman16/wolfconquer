using System.Globalization;
using System.Text.Json;

internal static partial class Program
{
    private const int ExpectedLicenseRouteRequestBytes = 208;
    private const int ExpectedLicenseRouteResponseBytes = 445;
    private const int MaxPrintedLicenseRouteCorrelationPairs = 40;

    static int LicenseRouteRelayCorrelation(string[] args)
    {
        LicenseRouteRelayCorrelationOptions options;
        try
        {
            options = ResolveLicenseRouteRelayCorrelationOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var captures = new List<LicenseRouteRelayCaptureFile>();
            var skippedCaptures = new List<LicenseRouteRelaySkippedCapture>();
            foreach (var path in options.CapturePaths)
            {
                try
                {
                    captures.Add(ReadLicenseRouteRelayCaptureFile(path));
                }
                catch (InvalidDataException ex)
                {
                    skippedCaptures.Add(new LicenseRouteRelaySkippedCapture(path, ex.Message));
                }
            }

            if (captures.Count == 0)
            {
                throw new InvalidDataException("No usable license route relay captures had both clientToServer and serverToClient chunks.");
            }

            var report = AnalyzeLicenseRouteRelayCorrelation(captures);

            Console.WriteLine("License route relay correlation:");
            Console.WriteLine($"Relay captures: {captures.Count}");
            Console.WriteLine($"Skipped incomplete captures: {skippedCaptures.Count}");
            foreach (var skippedCapture in skippedCaptures)
            {
                Console.WriteLine(
                    $"Skipped: path={skippedCapture.Path} reason={skippedCapture.Reason}");
            }

            Console.WriteLine($"Client request lengths: {string.Join(",", captures.Select(capture => capture.ClientToServerBytes.Count))}");
            Console.WriteLine($"Official response lengths: {string.Join(",", captures.Select(capture => capture.ServerToClientBytes.Count))}");
            Console.WriteLine($"Official 208/445 shape: {FormatBool(report.HasExpectedShape)}");
            PrintLicenseRouteStaticResponseReplayEvidence(report.StaticResponseReplayEvidence);
            Console.WriteLine($"Stable request byte ranges: {FormatByteOffsetRanges(report.StableRequestOffsets)}");
            Console.WriteLine(
                "Request bytes present somewhere in same response: " +
                string.Join(", ", report.RequestPresence.Select(FormatRequestPresence)));
            PrintLicenseRouteCorrelationPairs("Same-offset byte equality", report.SameOffsetEqualPairs);
            PrintLicenseRouteCorrelationPairs("Request-byte occurrence stable offset pairs", report.StableEqualPairs);
            PrintLicenseRouteCorrelationPairs("XOR stable single-byte candidates", report.XorCandidates);
            PrintLicenseRouteCorrelationPairs("ADD stable single-byte candidates", report.AddCandidates);
            PrintLicenseRouteCorrelationPairs("SUB stable single-byte candidates", report.SubCandidates);
            Console.WriteLine(
                "Note: candidates are byte-level relationships repeated across the supplied captures; " +
                "this does not prove crypto, direction, or causality.");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"License route relay correlation failed: {ex.Message}");
            return 1;
        }
    }

    static LicenseRouteRelayCorrelationOptions ResolveLicenseRouteRelayCorrelationOptions(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool license-route-relay-correlation <relayCaptureJsonPath> [relayCaptureJsonPath ...]");
        }

        return new LicenseRouteRelayCorrelationOptions(args.Skip(1).Select(Path.GetFullPath).ToArray());
    }

    static LicenseRouteRelayCorrelationReport AnalyzeLicenseRouteRelayCorrelation(
        IReadOnlyList<LicenseRouteRelayCaptureFile> captures)
    {
        if (captures.Count == 0)
        {
            throw new ArgumentException("At least one license route relay capture is required.");
        }

        var stableRequestOffsets = FindStableRequestOffsets(captures);
        var sameOffsetEqualPairs = FindSameOffsetEqualPairs(captures);
        var stableEqualPairs = FindEqualPairs(captures);
        var xorCandidates = FindXorCandidates(captures);
        var addCandidates = FindAddSubCandidates(captures, add: true);
        var subCandidates = FindAddSubCandidates(captures, add: false);
        var requestPresence = captures
            .Select(capture => CreateRequestPresence(capture))
            .ToArray();
        var serverResult = CompareRelayByteSequences(captures
            .Select(capture => capture.ServerToClientBytes)
            .ToArray());

        return new LicenseRouteRelayCorrelationReport(
            captures.All(capture =>
                capture.ClientToServerBytes.Count == ExpectedLicenseRouteRequestBytes &&
                capture.ServerToClientBytes.Count == ExpectedLicenseRouteResponseBytes),
            AnalyzeLicenseRouteStaticResponseReplay(captures, serverResult),
            stableRequestOffsets,
            requestPresence,
            sameOffsetEqualPairs,
            stableEqualPairs,
            xorCandidates,
            addCandidates,
            subCandidates);
    }

    static IReadOnlyList<int> FindStableRequestOffsets(IReadOnlyList<LicenseRouteRelayCaptureFile> captures)
    {
        var comparedByteCount = captures.Max(capture => capture.ClientToServerBytes.Count);
        var stableOffsets = new List<int>();

        for (var offset = 0; offset < comparedByteCount; offset++)
        {
            var presentInAll = captures.All(capture => offset < capture.ClientToServerBytes.Count);
            if (!presentInAll)
            {
                continue;
            }

            var firstValue = captures[0].ClientToServerBytes[offset];
            if (captures.All(capture => capture.ClientToServerBytes[offset] == firstValue))
            {
                stableOffsets.Add(offset);
            }
        }

        return stableOffsets;
    }

    static IReadOnlyList<LicenseRouteCorrelationPair> FindSameOffsetEqualPairs(
        IReadOnlyList<LicenseRouteRelayCaptureFile> captures)
    {
        var comparedByteCount = captures.Min(capture =>
            Math.Min(capture.ClientToServerBytes.Count, capture.ServerToClientBytes.Count));
        var pairs = new List<LicenseRouteCorrelationPair>();

        for (var offset = 0; offset < comparedByteCount; offset++)
        {
            if (captures.All(capture => capture.ClientToServerBytes[offset] == capture.ServerToClientBytes[offset]))
            {
                pairs.Add(new LicenseRouteCorrelationPair(offset, offset, LicenseRouteCorrelationKind.Equal, null));
            }
        }

        return pairs;
    }

    static IReadOnlyList<LicenseRouteCorrelationPair> FindEqualPairs(
        IReadOnlyList<LicenseRouteRelayCaptureFile> captures)
    {
        return FindStablePairCandidates(
            captures,
            LicenseRouteCorrelationKind.Equal,
            (_, _) => 0,
            (requestByte, responseByte) => requestByte == responseByte,
            _ => true);
    }

    static IReadOnlyList<LicenseRouteCorrelationPair> FindXorCandidates(
        IReadOnlyList<LicenseRouteRelayCaptureFile> captures)
    {
        return FindStablePairCandidates(
            captures,
            LicenseRouteCorrelationKind.Xor,
            (requestByte, responseByte) => requestByte ^ responseByte,
            (requestByte, responseByte) => requestByte != responseByte,
            relationValue => relationValue != 0);
    }

    static IReadOnlyList<LicenseRouteCorrelationPair> FindAddSubCandidates(
        IReadOnlyList<LicenseRouteRelayCaptureFile> captures,
        bool add)
    {
        return FindStablePairCandidates(
            captures,
            add ? LicenseRouteCorrelationKind.Add : LicenseRouteCorrelationKind.Sub,
            (requestByte, responseByte) => (responseByte - requestByte) & 0xff,
            (requestByte, responseByte) => requestByte != responseByte,
            relationValue => add
                ? relationValue is > 0 and <= 0x7f
                : relationValue is >= 0x81 and <= 0xff,
            relationValue => add ? relationValue : 0x100 - relationValue);
    }

    static IReadOnlyList<LicenseRouteCorrelationPair> FindStablePairCandidates(
        IReadOnlyList<LicenseRouteRelayCaptureFile> captures,
        LicenseRouteCorrelationKind kind,
        Func<int, int, int> relationSelector,
        Func<int, int, bool> perCaptureFilter,
        Func<int, bool> relationFilter,
        Func<int, int>? reportedValueSelector = null)
    {
        var minRequestLength = captures.Min(capture => capture.ClientToServerBytes.Count);
        var minResponseLength = captures.Min(capture => capture.ServerToClientBytes.Count);
        var pairs = new List<LicenseRouteCorrelationPair>();

        for (var requestOffset = 0; requestOffset < minRequestLength; requestOffset++)
        {
            for (var responseOffset = 0; responseOffset < minResponseLength; responseOffset++)
            {
                var firstRequestByte = captures[0].ClientToServerBytes[requestOffset];
                var firstResponseByte = captures[0].ServerToClientBytes[responseOffset];
                if (!perCaptureFilter(firstRequestByte, firstResponseByte))
                {
                    continue;
                }

                var relationValue = relationSelector(firstRequestByte, firstResponseByte);
                if (!relationFilter(relationValue))
                {
                    continue;
                }

                var stable = captures.All(capture =>
                {
                    var requestByte = capture.ClientToServerBytes[requestOffset];
                    var responseByte = capture.ServerToClientBytes[responseOffset];
                    return perCaptureFilter(requestByte, responseByte) &&
                        relationSelector(requestByte, responseByte) == relationValue;
                });
                if (!stable)
                {
                    continue;
                }

                if (kind != LicenseRouteCorrelationKind.Equal &&
                    (!ValuesVary(captures.Select(capture => capture.ClientToServerBytes[requestOffset])) ||
                     !ValuesVary(captures.Select(capture => capture.ServerToClientBytes[responseOffset]))))
                {
                    continue;
                }

                pairs.Add(new LicenseRouteCorrelationPair(
                    requestOffset,
                    responseOffset,
                    kind,
                    reportedValueSelector is null ? relationValue : reportedValueSelector(relationValue)));
            }
        }

        return pairs;
    }

    static bool ValuesVary(IEnumerable<byte> values)
    {
        byte? first = null;
        foreach (var value in values)
        {
            if (!first.HasValue)
            {
                first = value;
                continue;
            }

            if (value != first.Value)
            {
                return true;
            }
        }

        return false;
    }

    static LicenseRouteRequestPresence CreateRequestPresence(LicenseRouteRelayCaptureFile capture)
    {
        var responseByteValues = capture.ServerToClientBytes.ToHashSet();
        var presentOffsets = capture.ClientToServerBytes
            .Select((value, offset) => new { value, offset })
            .Where(item => responseByteValues.Contains(item.value))
            .Select(item => item.offset)
            .ToArray();

        return new LicenseRouteRequestPresence(
            capture.Path,
            capture.ClientToServerBytes.Count,
            presentOffsets);
    }

    static void PrintLicenseRouteCorrelationPairs(
        string label,
        IReadOnlyList<LicenseRouteCorrelationPair> pairs)
    {
        Console.WriteLine($"{label}: {pairs.Count} pair(s): {FormatLicenseRouteCorrelationPairs(pairs)}");
    }

    static string FormatLicenseRouteCorrelationPairs(IReadOnlyList<LicenseRouteCorrelationPair> pairs)
    {
        if (pairs.Count == 0)
        {
            return "<none>";
        }

        var formattedPairs = pairs
            .Take(MaxPrintedLicenseRouteCorrelationPairs)
            .Select(FormatLicenseRouteCorrelationPair);
        var suffix = pairs.Count > MaxPrintedLicenseRouteCorrelationPairs
            ? $" (showing first {MaxPrintedLicenseRouteCorrelationPairs.ToString(CultureInfo.InvariantCulture)} of {pairs.Count.ToString(CultureInfo.InvariantCulture)})"
            : "";
        return string.Join(", ", formattedPairs) + suffix;
    }

    static string FormatLicenseRouteCorrelationPair(LicenseRouteCorrelationPair pair)
    {
        var offsets = $"r{pair.RequestOffset.ToString(CultureInfo.InvariantCulture)}->s{pair.ResponseOffset.ToString(CultureInfo.InvariantCulture)}";
        return pair.Kind switch
        {
            LicenseRouteCorrelationKind.Xor => $"{offsets} xor=0x{pair.Value!.Value:X2}",
            LicenseRouteCorrelationKind.Add => $"{offsets} +0x{pair.Value!.Value:X2}",
            LicenseRouteCorrelationKind.Sub => $"{offsets} -0x{pair.Value!.Value:X2}",
            _ => offsets,
        };
    }

    static string FormatRequestPresence(LicenseRouteRequestPresence presence)
    {
        return $"{Path.GetFileName(presence.Path)}={presence.PresentRequestOffsets.Count.ToString(CultureInfo.InvariantCulture)}/" +
            $"{presence.RequestByteCount.ToString(CultureInfo.InvariantCulture)}";
    }

    static string FormatBool(bool value)
    {
        return value ? "yes" : "no";
    }
}

internal sealed record LicenseRouteRelayCorrelationOptions(IReadOnlyList<string> CapturePaths);

internal sealed record LicenseRouteRelaySkippedCapture(string Path, string Reason);

internal sealed record LicenseRouteRelayCorrelationReport(
    bool HasExpectedShape,
    LicenseRouteStaticResponseReplayEvidence StaticResponseReplayEvidence,
    IReadOnlyList<int> StableRequestOffsets,
    IReadOnlyList<LicenseRouteRequestPresence> RequestPresence,
    IReadOnlyList<LicenseRouteCorrelationPair> SameOffsetEqualPairs,
    IReadOnlyList<LicenseRouteCorrelationPair> StableEqualPairs,
    IReadOnlyList<LicenseRouteCorrelationPair> XorCandidates,
    IReadOnlyList<LicenseRouteCorrelationPair> AddCandidates,
    IReadOnlyList<LicenseRouteCorrelationPair> SubCandidates);

internal sealed record LicenseRouteRequestPresence(
    string Path,
    int RequestByteCount,
    IReadOnlyList<int> PresentRequestOffsets);

internal sealed record LicenseRouteCorrelationPair(
    int RequestOffset,
    int ResponseOffset,
    LicenseRouteCorrelationKind Kind,
    int? Value);

internal enum LicenseRouteCorrelationKind
{
    Equal,
    Xor,
    Add,
    Sub,
}

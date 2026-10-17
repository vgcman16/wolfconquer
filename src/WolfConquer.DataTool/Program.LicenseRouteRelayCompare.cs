using System.Globalization;
using System.Text.Json;

internal static partial class Program
{
    static int LicenseRouteRelayCompare(string[] args)
    {
        LicenseRouteRelayCompareOptions options;
        try
        {
            options = ResolveLicenseRouteRelayCompareOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var captures = options.CapturePaths
                .Select(ReadLicenseRouteRelayCaptureFile)
                .ToArray();
            var clientResult = CompareRelayByteSequences(captures.Select(capture => capture.ClientToServerBytes).ToArray());
            var serverResult = CompareRelayByteSequences(captures.Select(capture => capture.ServerToClientBytes).ToArray());

            Console.WriteLine("License route relay compare:");
            Console.WriteLine($"Relay captures: {captures.Length}");
            foreach (var capture in captures)
            {
                Console.WriteLine(
                    $"Capture: path={capture.Path} clientToServerBytes={capture.ClientToServerBytes.Count} " +
                    $"serverToClientBytes={capture.ServerToClientBytes.Count} chunks={capture.ChunkCount} " +
                    $"stopReason={FormatNullableText(capture.StopReason)}");
            }

            PrintRelayCompareDirection(
                "Client-to-server",
                "Client",
                captures.Select(capture => capture.ClientToServerBytes.Count),
                clientResult);
            PrintRelayCompareDirection(
                "Server-to-client",
                "Server",
                captures.Select(capture => capture.ServerToClientBytes.Count),
                serverResult);
            PrintLicenseRouteStaticResponseReplayEvidence(
                AnalyzeLicenseRouteStaticResponseReplay(captures, serverResult));

            return 0;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"License route relay compare failed: {ex.Message}");
            return 1;
        }
    }

    static LicenseRouteRelayCompareOptions ResolveLicenseRouteRelayCompareOptions(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool license-route-relay-compare <relayCaptureJsonPath> [relayCaptureJsonPath ...]");
        }

        return new LicenseRouteRelayCompareOptions(args.Skip(1).Select(Path.GetFullPath).ToArray());
    }

    static LicenseRouteRelayCaptureFile ReadLicenseRouteRelayCaptureFile(string path)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var capture = JsonSerializer.Deserialize<LicenseRouteRelayCaptureJson>(File.ReadAllText(path), options);
        if (capture is null)
        {
            throw new InvalidDataException($"Could not read license route relay JSON: {path}");
        }

        var clientChunk = ReadSingleRelayChunk(path, capture.Chunks, "clientToServer");
        var serverChunk = ReadSingleRelayChunk(path, capture.Chunks, "serverToClient");
        return new LicenseRouteRelayCaptureFile(
            path,
            ParseRelayChunkBytes(path, clientChunk),
            ParseRelayChunkBytes(path, serverChunk),
            serverChunk.Length,
            capture.Chunks.Count,
            capture.StopReason);
    }

    static LicenseRouteRelayCaptureChunkJson ReadSingleRelayChunk(
        string path,
        IReadOnlyList<LicenseRouteRelayCaptureChunkJson> chunks,
        string direction)
    {
        var matchingChunks = chunks
            .Where(chunk => string.Equals(chunk.Direction, direction, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matchingChunks.Length == 0)
        {
            throw new InvalidDataException($"License route relay capture has no {direction} chunk: {path}");
        }

        if (matchingChunks.Length > 1)
        {
            throw new InvalidDataException($"License route relay capture has multiple {direction} chunks: {path}");
        }

        return matchingChunks[0];
    }

    static IReadOnlyList<byte> ParseRelayChunkBytes(string path, LicenseRouteRelayCaptureChunkJson chunk)
    {
        if (string.IsNullOrWhiteSpace(chunk.FirstHex))
        {
            throw new InvalidDataException($"License route relay chunk has no firstHex: {path} direction={chunk.Direction}");
        }

        var bytes = ParseHexBytes(chunk.FirstHex);
        if (chunk.SampleLength.HasValue && chunk.SampleLength.Value != bytes.Count)
        {
            throw new InvalidDataException(
                $"License route relay sampleLength mismatch in {path} direction={chunk.Direction}: " +
                $"declared={chunk.SampleLength.Value} parsed={bytes.Count}");
        }

        if (chunk.Length.HasValue && chunk.Length.Value < bytes.Count)
        {
            throw new InvalidDataException(
                $"License route relay length is smaller than parsed firstHex in {path} direction={chunk.Direction}: " +
                $"declared={chunk.Length.Value} parsed={bytes.Count}");
        }

        return bytes;
    }

    static LicenseRouteRelayCompareResult CompareRelayByteSequences(IReadOnlyList<IReadOnlyList<byte>> packets)
    {
        var comparedByteCount = packets.Max(packet => packet.Count);
        var stableByteOffsets = new List<int>();
        var differentByteOffsets = new List<int>();

        for (var offset = 0; offset < comparedByteCount; offset++)
        {
            var firstPacket = packets[0];
            var presentInAll = packets.All(packet => offset < packet.Count);
            var equalInAll = presentInAll &&
                packets.All(packet => packet[offset] == firstPacket[offset]);

            if (equalInAll)
            {
                stableByteOffsets.Add(offset);
            }
            else
            {
                differentByteOffsets.Add(offset);
            }
        }

        return new LicenseRouteRelayCompareResult(
            comparedByteCount,
            stableByteOffsets,
            differentByteOffsets,
            packets.Select(packet => packet.Count).Distinct().Count() > 1);
    }

    static void PrintRelayCompareDirection(
        string label,
        string rangeLabel,
        IEnumerable<int> lengths,
        LicenseRouteRelayCompareResult result)
    {
        Console.WriteLine($"{label} lengths: {string.Join(",", lengths)}");
        Console.WriteLine($"{label} compared byte span: {FormatLicenseRouteComparedSpan(result.ComparedByteCount)}");
        Console.WriteLine($"{rangeLabel} stable byte positions: {result.StableByteOffsets.Count}/{result.ComparedByteCount}");
        Console.WriteLine($"{rangeLabel} different byte positions: {result.DifferentByteOffsets.Count}/{result.ComparedByteCount}");
        Console.WriteLine($"{rangeLabel} stable byte ranges: {FormatByteOffsetRanges(result.StableByteOffsets)}");
        Console.WriteLine($"{rangeLabel} different byte ranges: {FormatByteOffsetRanges(result.DifferentByteOffsets)}");
        Console.WriteLine($"{label} length mismatch: {result.LengthMismatch}");
    }

    static LicenseRouteStaticResponseReplayEvidence AnalyzeLicenseRouteStaticResponseReplay(
        IReadOnlyList<LicenseRouteRelayCaptureFile> captures,
        LicenseRouteRelayCompareResult serverResult)
    {
        var completeResponseSamples = captures.All(HasCompleteServerResponseSample);
        var isReplayCandidate = captures.Count >= 2 &&
            serverResult.ComparedByteCount > 0 &&
            completeResponseSamples &&
            !serverResult.LengthMismatch &&
            serverResult.DifferentByteOffsets.Count == 0;

        return new LicenseRouteStaticResponseReplayEvidence(
            isReplayCandidate,
            serverResult.StableByteOffsets.Count,
            serverResult.ComparedByteCount,
            completeResponseSamples,
            GetStaticResponseReplayReason(captures, serverResult, completeResponseSamples));
    }

    static bool HasCompleteServerResponseSample(LicenseRouteRelayCaptureFile capture)
    {
        return capture.ServerToClientDeclaredLength.HasValue &&
            capture.ServerToClientDeclaredLength.Value == capture.ServerToClientBytes.Count;
    }

    static string GetStaticResponseReplayReason(
        IReadOnlyList<LicenseRouteRelayCaptureFile> captures,
        LicenseRouteRelayCompareResult serverResult,
        bool completeResponseSamples)
    {
        if (captures.Count < 2)
        {
            return "need at least two relay captures";
        }

        if (serverResult.ComparedByteCount == 0)
        {
            return "server response sample is empty";
        }

        if (!completeResponseSamples)
        {
            return "one or more server response chunks were only partially sampled or missing declared length";
        }

        if (serverResult.LengthMismatch)
        {
            return "server response byte counts differ across captures";
        }

        if (serverResult.DifferentByteOffsets.Count > 0)
        {
            return "server response bytes differ across captures";
        }

        return "all fully sampled server response bytes match across captures";
    }

    static void PrintLicenseRouteStaticResponseReplayEvidence(LicenseRouteStaticResponseReplayEvidence evidence)
    {
        Console.WriteLine(
            $"Official 9865 response stable bytes: " +
            $"{evidence.StableResponseByteCount.ToString(CultureInfo.InvariantCulture)}/" +
            $"{evidence.ComparedResponseByteCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Official 9865 full response samples: {FormatBool(evidence.CompleteResponseSamples)}");
        Console.WriteLine($"Static response replay candidate: {FormatBool(evidence.IsReplayCandidate)}");
        Console.WriteLine($"Static response replay reason: {evidence.Reason}");
    }
}

internal sealed record LicenseRouteRelayCompareOptions(IReadOnlyList<string> CapturePaths);

internal sealed record LicenseRouteRelayCaptureFile(
    string Path,
    IReadOnlyList<byte> ClientToServerBytes,
    IReadOnlyList<byte> ServerToClientBytes,
    int? ServerToClientDeclaredLength,
    int ChunkCount,
    string? StopReason);

internal sealed record LicenseRouteRelayCompareResult(
    int ComparedByteCount,
    IReadOnlyList<int> StableByteOffsets,
    IReadOnlyList<int> DifferentByteOffsets,
    bool LengthMismatch);

internal sealed record LicenseRouteStaticResponseReplayEvidence(
    bool IsReplayCandidate,
    int StableResponseByteCount,
    int ComparedResponseByteCount,
    bool CompleteResponseSamples,
    string Reason);

internal sealed class LicenseRouteRelayCaptureJson
{
    public string? StopReason { get; set; }

    public List<LicenseRouteRelayCaptureChunkJson> Chunks { get; set; } = [];
}

internal sealed class LicenseRouteRelayCaptureChunkJson
{
    public string? Direction { get; set; }

    public int? Length { get; set; }

    public int? SampleLength { get; set; }

    public string? FirstHex { get; set; }
}

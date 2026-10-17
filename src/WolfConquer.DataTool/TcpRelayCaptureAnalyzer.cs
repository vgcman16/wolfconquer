using System.Security.Cryptography;
using System.Text.Json;

internal static class TcpRelayCaptureAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static TcpRelayCaptureSummary Analyze(string path, int firstChunkCount)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Capture path is required.", nameof(path));
        }

        if (firstChunkCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(firstChunkCount), firstChunkCount, "Chunk count cannot be negative.");
        }

        var fullPath = Path.GetFullPath(path);
        var bytes = File.ReadAllBytes(fullPath);
        var capture = ReadCapture(bytes, fullPath);
        var chunks = capture.Chunks ?? Array.Empty<TcpRelayCaptureAnalyzerChunk>();

        var directionTotals = chunks
            .GroupBy(chunk => chunk.Direction, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(chunk => (long)chunk.Length),
                StringComparer.Ordinal);

        var firstChunkDescriptors = chunks
            .Take(firstChunkCount)
            .Select(chunk => $"{chunk.Direction}:{chunk.Length}")
            .ToArray();

        return new TcpRelayCaptureSummary(
            fullPath,
            bytes.LongLength,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            capture.ListenAddress,
            capture.ListenPort,
            capture.TargetAddress,
            capture.TargetPort,
            capture.MaxBytes,
            capture.AcceptSeconds,
            capture.IdleSeconds,
            capture.TotalClientToServerBytes,
            capture.TotalServerToClientBytes,
            chunks.Count,
            capture.StopReason,
            firstChunkDescriptors,
            directionTotals);
    }

    public static TcpRelayCaptureAnalyzerFile ReadCapture(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Capture path is required.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        return ReadCapture(File.ReadAllBytes(fullPath), fullPath);
    }

    private static TcpRelayCaptureAnalyzerFile ReadCapture(byte[] bytes, string fullPath)
    {
        return JsonSerializer.Deserialize<TcpRelayCaptureAnalyzerFile>(StripUtf8Bom(bytes), JsonOptions)
            ?? throw new InvalidDataException($"TCP relay capture JSON is empty: {fullPath}");
    }

    private static ReadOnlySpan<byte> StripUtf8Bom(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF
            ? bytes[3..]
            : bytes;
    }
}

internal sealed record TcpRelayCaptureSummary(
    string Path,
    long FileLength,
    string Sha256,
    string ListenAddress,
    int ListenPort,
    string TargetAddress,
    int TargetPort,
    int MaxBytes,
    int AcceptSeconds,
    int IdleSeconds,
    long TotalClientToServerBytes,
    long TotalServerToClientBytes,
    int ChunkCount,
    string StopReason,
    IReadOnlyList<string> FirstChunkDescriptors,
    IReadOnlyDictionary<string, long> DirectionTotalsByChunks);

internal sealed record TcpRelayCaptureAnalyzerFile(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string ListenAddress,
    int ListenPort,
    string TargetAddress,
    int TargetPort,
    int MaxBytes,
    int AcceptSeconds,
    int IdleSeconds,
    long TotalClientToServerBytes,
    long TotalServerToClientBytes,
    string StopReason,
    IReadOnlyList<TcpRelayCaptureAnalyzerChunk>? Chunks);

internal sealed record TcpRelayCaptureAnalyzerChunk(
    DateTimeOffset TimestampUtc,
    string Direction,
    int Length,
    int SampleLength,
    string FirstHex,
    string FirstBase64);

using System.Net;
using System.Net.Sockets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Networking;

public sealed record EndpointProbeCapture(
    DateTimeOffset CapturedAtUtc,
    int TimeoutMs,
    int MaxBytes,
    int Samples,
    int SampleDelayMs,
    IReadOnlyList<EndpointProbeSample> Results);

public sealed record EndpointProbeSample(
    int Sample,
    string Role,
    string Address,
    int Port,
    bool Connected,
    bool TimedOut,
    bool RemoteClosed,
    int BytesRead,
    string PayloadBase64,
    string PayloadSha256,
    double ElapsedMs,
    string? Error);

public sealed record EndpointProbeAnalysis(
    string Role,
    string Address,
    int Port,
    int Samples,
    bool AllConnected,
    bool AnyTimedOut,
    bool AnyRemoteClosed,
    IReadOnlyList<int> ByteCounts,
    int UniqueNonEmptyPayloads,
    int StablePrefixBytes,
    string StablePrefixHex,
    EndpointRouteScope RouteScope,
    bool UnexpectedExternalClientRoute);

public sealed record EndpointProbeComparison(
    string Role,
    string Address,
    int Port,
    int Captures,
    int Samples,
    bool AllConnected,
    bool AnyTimedOut,
    bool AnyRemoteClosed,
    IReadOnlyList<int> ByteCounts,
    int UniqueNonEmptyPayloads,
    int StablePrefixBytes,
    string StablePrefixHex,
    EndpointRouteScope RouteScope,
    bool UnexpectedExternalClientRoute);

public enum EndpointRouteScope
{
    Unknown,
    Local,
    External,
}

public static class EndpointRouteClassifier
{
    public static EndpointRouteScope Classify(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return EndpointRouteScope.Unknown;
        }

        if (string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase) ||
            address.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return EndpointRouteScope.Local;
        }

        if (!IPAddress.TryParse(address, out var parsedAddress))
        {
            return EndpointRouteScope.Unknown;
        }

        if (string.Equals(address, ConquerRuntimeDefaults.ServerAddress, StringComparison.OrdinalIgnoreCase))
        {
            return EndpointRouteScope.Local;
        }

        var normalizedAddress = parsedAddress.IsIPv4MappedToIPv6
            ? parsedAddress.MapToIPv4()
            : parsedAddress;

        return IsLocalAddress(normalizedAddress)
            ? EndpointRouteScope.Local
            : EndpointRouteScope.External;
    }

    public static bool IsUnexpectedExternalClientRoute(string address, int port)
    {
        return IsUnexpectedExternalClientRoute(address, port, Classify(address));
    }

    internal static bool IsUnexpectedExternalClientRoute(
        string address,
        int port,
        EndpointRouteScope routeScope)
    {
        return routeScope == EndpointRouteScope.External && port is 80 or 9865;
    }

    private static bool IsLocalAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.None) ||
            address.Equals(IPAddress.IPv6None))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 10 ||
                bytes[0] == 127 ||
                (bytes[0] == 169 && bytes[1] == 254) ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 100 && bytes[1] is >= 64 and <= 127);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || (bytes[0] & 0xfe) == 0xfc;
        }

        return false;
    }
}

public static class EndpointProbeAnalyzer
{
    public static IReadOnlyList<EndpointProbeAnalysis> Analyze(EndpointProbeCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);

        return capture.Results
            .GroupBy(sample => new EndpointKey(sample.Role, sample.Address, sample.Port))
            .OrderBy(group => group.Key.Role, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Port)
            .ThenBy(group => group.Key.Address, StringComparer.Ordinal)
            .Select(CreateAnalysis)
            .ToArray();
    }

    public static IReadOnlyList<EndpointProbeComparison> Compare(IReadOnlyList<EndpointProbeCapture> captures)
    {
        ArgumentNullException.ThrowIfNull(captures);

        return captures
            .SelectMany((capture, captureIndex) =>
                capture.Results.Select(sample => new CapturedEndpointSample(captureIndex, sample)))
            .GroupBy(entry => new EndpointKey(entry.Sample.Role, entry.Sample.Address, entry.Sample.Port))
            .OrderBy(group => group.Key.Role, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Port)
            .ThenBy(group => group.Key.Address, StringComparer.Ordinal)
            .Select(CreateComparison)
            .ToArray();
    }

    private static EndpointProbeAnalysis CreateAnalysis(IGrouping<EndpointKey, EndpointProbeSample> group)
    {
        var samples = group.ToArray();
        var routeScope = EndpointRouteClassifier.Classify(group.Key.Address);
        var payloads = samples
            .Select(sample => TryDecodeNonEmptyPayload(sample.PayloadBase64))
            .Where(payload => payload is not null)
            .Select(payload => payload!)
            .ToArray();

        var stablePrefix = GetStablePrefix(payloads);

        return new EndpointProbeAnalysis(
            group.Key.Role,
            group.Key.Address,
            group.Key.Port,
            samples.Length,
            samples.All(sample => sample.Connected),
            samples.Any(sample => sample.TimedOut),
            samples.Any(sample => sample.RemoteClosed),
            samples.Select(sample => sample.BytesRead).Distinct().Order().ToArray(),
            CountUniqueNonEmptyPayloads(samples),
            stablePrefix.Length,
            Convert.ToHexString(stablePrefix).ToLowerInvariant(),
            routeScope,
            EndpointRouteClassifier.IsUnexpectedExternalClientRoute(group.Key.Address, group.Key.Port, routeScope));
    }

    private static EndpointProbeComparison CreateComparison(
        IGrouping<EndpointKey, CapturedEndpointSample> group)
    {
        var capturedSamples = group.ToArray();
        var samples = capturedSamples.Select(captured => captured.Sample).ToArray();
        var routeScope = EndpointRouteClassifier.Classify(group.Key.Address);
        var payloads = samples
            .Select(sample => TryDecodeNonEmptyPayload(sample.PayloadBase64))
            .Where(payload => payload is not null)
            .Select(payload => payload!)
            .ToArray();

        var stablePrefix = GetStablePrefix(payloads);

        return new EndpointProbeComparison(
            group.Key.Role,
            group.Key.Address,
            group.Key.Port,
            capturedSamples.Select(captured => captured.CaptureIndex).Distinct().Count(),
            samples.Length,
            samples.All(sample => sample.Connected),
            samples.Any(sample => sample.TimedOut),
            samples.Any(sample => sample.RemoteClosed),
            samples.Select(sample => sample.BytesRead).Distinct().Order().ToArray(),
            CountUniqueNonEmptyPayloads(samples),
            stablePrefix.Length,
            Convert.ToHexString(stablePrefix).ToLowerInvariant(),
            routeScope,
            EndpointRouteClassifier.IsUnexpectedExternalClientRoute(group.Key.Address, group.Key.Port, routeScope));
    }

    private static int CountUniqueNonEmptyPayloads(IEnumerable<EndpointProbeSample> samples)
    {
        var materialized = samples.ToArray();
        var hashes = materialized
            .Where(sample => !string.IsNullOrEmpty(sample.PayloadBase64))
            .Select(sample => sample.PayloadSha256)
            .Where(hash => !string.IsNullOrEmpty(hash))
            .Distinct(StringComparer.Ordinal)
            .Count();

        if (hashes > 0)
        {
            return hashes;
        }

        return materialized
            .Select(sample => sample.PayloadBase64)
            .Where(payload => !string.IsNullOrEmpty(payload))
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private static byte[] GetStablePrefix(IReadOnlyList<byte[]> payloads)
    {
        if (payloads.Count == 0)
        {
            return [];
        }

        var prefixLength = payloads[0].Length;
        for (var payloadIndex = 1; payloadIndex < payloads.Count; payloadIndex++)
        {
            var payload = payloads[payloadIndex];
            prefixLength = Math.Min(prefixLength, payload.Length);

            for (var byteIndex = 0; byteIndex < prefixLength; byteIndex++)
            {
                if (payloads[0][byteIndex] != payload[byteIndex])
                {
                    prefixLength = byteIndex;
                    break;
                }
            }

            if (prefixLength == 0)
            {
                break;
            }
        }

        return payloads[0][..prefixLength];
    }

    private static byte[]? TryDecodeNonEmptyPayload(string payloadBase64)
    {
        if (string.IsNullOrEmpty(payloadBase64))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(payloadBase64);
            return bytes.Length == 0 ? null : bytes;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private sealed record EndpointKey(string Role, string Address, int Port);

    private sealed record CapturedEndpointSample(int CaptureIndex, EndpointProbeSample Sample);
}

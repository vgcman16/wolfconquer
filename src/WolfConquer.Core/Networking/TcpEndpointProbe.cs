using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Networking;

public static class TcpEndpointProbe
{
    public static async Task<TcpEndpointProbeResult> ProbeAsync(
        ConquerEndpoint endpoint,
        TimeSpan timeout,
        int maxBytes = 256,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
        }

        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "Maximum bytes must be positive.");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(endpoint.Address, endpoint.Port, cancellationToken)
                .AsTask()
                .WaitAsync(timeout, cancellationToken)
                .ConfigureAwait(false);

            var stream = client.GetStream();
            var buffer = new byte[maxBytes];
            using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readTimeout.CancelAfter(timeout);

            int bytesRead;
            try
            {
                bytesRead = await stream.ReadAsync(buffer, readTimeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return CreateResult(endpoint, stopwatch.Elapsed, connected: true, timedOut: true);
            }

            if (bytesRead == 0)
            {
                return CreateResult(endpoint, stopwatch.Elapsed, connected: true, remoteClosed: true);
            }

            var payload = Convert.ToBase64String(buffer, 0, bytesRead);
            var payloadSha256 = Convert.ToHexString(SHA256.HashData(buffer.AsSpan(0, bytesRead))).ToLowerInvariant();
            return CreateResult(
                endpoint,
                stopwatch.Elapsed,
                connected: true,
                bytesRead: bytesRead,
                payloadBase64: payload,
                payloadSha256: payloadSha256);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            return CreateResult(endpoint, stopwatch.Elapsed, connected: false, error: exception.Message);
        }
        catch (Exception exception) when (exception is SocketException or IOException or ObjectDisposedException)
        {
            return CreateResult(endpoint, stopwatch.Elapsed, connected: false, error: exception.Message);
        }
    }

    public static async Task<IReadOnlyList<TcpEndpointProbeResult>> ProbeManyAsync(
        ConquerEndpoint endpoint,
        int samples,
        TimeSpan timeout,
        TimeSpan sampleDelay,
        int maxBytes = 256,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (samples <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(samples), "Samples must be positive.");
        }

        if (sampleDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleDelay), "Sample delay must be non-negative.");
        }

        var results = new List<TcpEndpointProbeResult>(samples);
        for (var sample = 0; sample < samples; sample++)
        {
            results.Add(await ProbeAsync(endpoint, timeout, maxBytes, cancellationToken).ConfigureAwait(false));

            if (sampleDelay > TimeSpan.Zero && sample < samples - 1)
            {
                await Task.Delay(sampleDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return results;
    }

    private static TcpEndpointProbeResult CreateResult(
        ConquerEndpoint endpoint,
        TimeSpan elapsed,
        bool connected,
        bool timedOut = false,
        bool remoteClosed = false,
        string? error = null,
        int bytesRead = 0,
        string payloadBase64 = "",
        string payloadSha256 = "")
    {
        return new TcpEndpointProbeResult(
            endpoint.Role,
            endpoint.Address,
            endpoint.Port,
            connected,
            timedOut,
            remoteClosed,
            error,
            bytesRead,
            payloadBase64,
            payloadSha256,
            elapsed);
    }
}

public sealed record TcpEndpointProbeResult(
    string Role,
    string Address,
    int Port,
    bool Connected,
    bool TimedOut,
    bool RemoteClosed,
    string? Error,
    int BytesRead,
    string PayloadBase64,
    string PayloadSha256,
    TimeSpan Elapsed);

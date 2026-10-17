using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WolfConquer.Core.Web;

public sealed class AutoPatchSocketSinkServer : IAsyncDisposable
{
    public const string ResponsePayloadEnvironmentVariable = "WOLFCONQUER_LOGINWEB_AUTOPATCH_SOCKET_RESPONSE";

    private const int MaxPayloadPreviewBytes = 512;

    private readonly TcpListener[] _listeners;
    private readonly CancellationTokenSource _stop = new();
    private readonly TextWriter _log;
    private readonly TimeSpan _readTimeout;
    private readonly byte[] _responsePayload;
    private readonly Task[] _acceptLoops;

    private AutoPatchSocketSinkServer(
        TcpListener[] listeners,
        TextWriter? log,
        TimeSpan readTimeout,
        byte[] responsePayload)
    {
        _listeners = listeners;
        _log = log ?? TextWriter.Null;
        _readTimeout = readTimeout;
        _responsePayload = responsePayload;
        _acceptLoops = listeners.Select(AcceptLoopAsync).ToArray();
    }

    public IReadOnlyList<IPEndPoint> BoundEndPoints =>
        _listeners.Select(listener => (IPEndPoint)listener.LocalEndpoint).ToArray();

    public static AutoPatchSocketSinkServer Start(
        IPAddress address,
        IReadOnlyCollection<int> ports,
        TextWriter? log = null,
        TimeSpan? readTimeout = null,
        byte[]? responsePayload = null)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(ports);
        if (ports.Count == 0)
        {
            throw new ArgumentException("At least one AutoPatch socket port is required.", nameof(ports));
        }

        var listeners = new List<TcpListener>();
        try
        {
            foreach (var port in ports.Distinct())
            {
                ArgumentOutOfRangeException.ThrowIfNegative(port);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(port, ushort.MaxValue);

                var listener = new TcpListener(address, port);
                listener.Start();
                listeners.Add(listener);
            }
        }
        catch
        {
            foreach (var listener in listeners)
            {
                listener.Stop();
            }

            throw;
        }

        return new AutoPatchSocketSinkServer(
            listeners.ToArray(),
            log,
            readTimeout ?? TimeSpan.FromSeconds(1),
            responsePayload ?? GetConfiguredResponsePayload());
    }

    private static byte[] GetConfiguredResponsePayload()
    {
        var configuredResponse = Environment.GetEnvironmentVariable(ResponsePayloadEnvironmentVariable);
        return configuredResponse is null
            ? "READY"u8.ToArray()
            : Encoding.ASCII.GetBytes(configuredResponse);
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        foreach (var listener in _listeners)
        {
            listener.Stop();
        }

        try
        {
            await Task.WhenAll(_acceptLoops).ConfigureAwait(false);
        }
        catch (SocketException) when (_stop.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (_stop.IsCancellationRequested)
        {
        }

        _stop.Dispose();
    }

    private async Task AcceptLoopAsync(TcpListener listener)
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException) when (_stop.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (_stop.IsCancellationRequested)
            {
                break;
            }

            _ = HandleClientAsync(client, (IPEndPoint)listener.LocalEndpoint, _stop.Token);
        }
    }

    private async Task HandleClientAsync(
        TcpClient client,
        IPEndPoint localEndPoint,
        CancellationToken cancellationToken)
    {
        using (client)
        {
            var remoteEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "<unknown>";
            _log.WriteLine($"AutoPatch socket accepted client {remoteEndPoint} on {localEndPoint}");

            var buffer = new byte[MaxPayloadPreviewBytes];
            var read = 0;
            using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readTimeout.CancelAfter(_readTimeout);
            try
            {
                read = await client.GetStream().ReadAsync(buffer, readTimeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!_stop.IsCancellationRequested)
            {
            }
            catch (IOException ex) when (ex.InnerException is SocketException)
            {
                _log.WriteLine($"AutoPatch socket read failed from {remoteEndPoint}: {ex.Message}");
            }
            catch (SocketException ex)
            {
                _log.WriteLine($"AutoPatch socket read failed from {remoteEndPoint}: {ex.Message}");
            }

            var payloadHex = read == 0
                ? "<none>"
                : Convert.ToHexString(buffer.AsSpan(0, read)).ToLowerInvariant();
            var responseHex = "<none>";
            if (read > 0 && _responsePayload.Length > 0)
            {
                try
                {
                    await client.GetStream().WriteAsync(_responsePayload, cancellationToken).ConfigureAwait(false);
                    responseHex = Convert.ToHexString(_responsePayload).ToLowerInvariant();
                }
                catch (IOException ex) when (ex.InnerException is SocketException)
                {
                    _log.WriteLine($"AutoPatch socket write failed to {remoteEndPoint}: {ex.Message}");
                }
                catch (SocketException ex)
                {
                    _log.WriteLine($"AutoPatch socket write failed to {remoteEndPoint}: {ex.Message}");
                }
            }

            _log.WriteLine(
                $"AutoPatch socket closed client {remoteEndPoint} bytes={read} payloadHex={payloadHex} responseHex={responseHex}");
        }
    }
}

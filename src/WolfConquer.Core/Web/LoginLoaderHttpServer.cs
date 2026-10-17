using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WolfConquer.Core.Web;

public sealed class LoginLoaderHttpServer : IAsyncDisposable
{
    private const int MaxHeaderBytes = 8192;

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly TextWriter _log;
    private readonly Task _acceptLoop;

    private LoginLoaderHttpServer(TcpListener listener, TextWriter? log)
    {
        _listener = listener;
        _log = log ?? TextWriter.Null;
        _acceptLoop = AcceptLoopAsync();
    }

    public IPEndPoint BoundEndPoint => (IPEndPoint)_listener.LocalEndpoint;

    public static LoginLoaderHttpServer Start(IPAddress address, int port, TextWriter? log = null)
    {
        ArgumentNullException.ThrowIfNull(address);

        var listener = new TcpListener(address, port);
        listener.Start();
        return new LoginLoaderHttpServer(listener, log);
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Stop();

        try
        {
            await _acceptLoop.ConfigureAwait(false);
        }
        catch (SocketException) when (_stop.IsCancellationRequested)
        {
        }

        _stop.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
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

            await HandleClientAsync(client, _stop.Token).ConfigureAwait(false);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            var stream = client.GetStream();
            var header = await ReadHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
            var response = CreateResponse(header);

            await WriteResponseAsync(stream, response, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<string?> ReadHeaderAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxHeaderBytes];
        var used = 0;

        while (used < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(used, buffer.Length - used), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            used += read;
            if (IndexOfHeaderTerminator(buffer.AsSpan(0, used)) >= 0)
            {
                return Encoding.ASCII.GetString(buffer, 0, used);
            }
        }

        return used == 0 ? null : Encoding.ASCII.GetString(buffer, 0, used);
    }

    private LoginLoaderHttpResponse CreateResponse(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return new LoginLoaderHttpResponse(400, "Bad Request", "text/plain; charset=us-ascii", []);
        }

        var firstLineEnd = header.IndexOf("\r\n", StringComparison.Ordinal);
        var firstLine = firstLineEnd < 0 ? header : header[..firstLineEnd];
        var parts = firstLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return new LoginLoaderHttpResponse(400, "Bad Request", "text/plain; charset=us-ascii", []);
        }

        var response = LoginLoaderHttpResponder.Get(parts[0], parts[1]);
        _log.WriteLine($"{parts[0]} {parts[1]} -> {response.StatusCode}");
        return response;
    }

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        LoginLoaderHttpResponse response,
        CancellationToken cancellationToken)
    {
        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 " + response.StatusCode + " " + response.ReasonPhrase + "\r\n" +
            "Server: WolfConquer.LoginWeb\r\n" +
            "Cache-Control: no-cache\r\n" +
            "Content-Type: " + response.ContentType + "\r\n" +
            "Content-Length: " + response.Body.Length + "\r\n" +
            "Connection: close\r\n" +
            "\r\n");

        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(response.Body, cancellationToken).ConfigureAwait(false);
    }

    private static int IndexOfHeaderTerminator(ReadOnlySpan<byte> buffer)
    {
        for (var index = 0; index <= buffer.Length - 4; index++)
        {
            if (buffer[index] == '\r' &&
                buffer[index + 1] == '\n' &&
                buffer[index + 2] == '\r' &&
                buffer[index + 3] == '\n')
            {
                return index;
            }
        }

        return -1;
    }
}

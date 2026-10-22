using System.Buffers;
using System.Net;
using System.Net.Sockets;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class ConquerTcpServerTests
{
    [Fact]
    public async Task TcpServerReceivesPacketFrameFromClient()
    {
        var received = new TaskCompletionSource<(ushort Opcode, byte[] Payload, ConquerTcpConnection Connection)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = CreateLoopbackServer((connection, frame, _) =>
        {
            received.TrySetResult((frame.Header.Opcode, frame.Payload.ToArray(), connection));
            return ValueTask.CompletedTask;
        });

        server.Start();
        using var client = await ConnectAsync(server);
        var packet = new PacketWriter(1010, payloadLength: 2);
        packet.WriteUInt16(0, 0x1234);

        await client.GetStream().WriteAsync(packet.ToArray());

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1010, result.Opcode);
        Assert.Equal([0x34, 0x12], result.Payload);
        Assert.NotNull(result.Connection.LocalEndPoint);
        Assert.NotNull(result.Connection.RemoteEndPoint);
    }

    [Fact]
    public async Task TcpServerReassemblesFrameAcrossClientWrites()
    {
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = CreateLoopbackServer((_, frame, _) =>
        {
            received.TrySetResult(frame.Payload.ToArray());
            return ValueTask.CompletedTask;
        });

        server.Start();
        using var client = await ConnectAsync(server);
        var packet = new PacketWriter(1001, payloadLength: 4);
        packet.WriteUInt16(0, 0x1234);
        packet.WriteUInt16(2, 0xBEEF);
        var bytes = packet.ToArray();
        var stream = client.GetStream();

        await stream.WriteAsync(bytes[..3]);
        await Task.Delay(25);
        await stream.WriteAsync(bytes[3..6]);
        await Task.Delay(25);
        await stream.WriteAsync(bytes[6..]);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([0x34, 0x12, 0xEF, 0xBE], payload);
    }

    [Fact]
    public async Task TcpConnectionCanSendPacketFrameToClient()
    {
        await using var server = CreateLoopbackServer((connection, frame, token) =>
        {
            return connection.SendAsync(frame.Frame.ToArray(), token);
        });

        server.Start();
        using var client = await ConnectAsync(server);
        var packet = new PacketWriter(1001, payloadLength: 2);
        packet.WriteUInt16(0, 0xBEEF);

        await client.GetStream().WriteAsync(packet.ToArray());

        var response = new byte[packet.Header.Length];
        await client.GetStream().ReadExactlyAsync(response);
        Assert.Equal(packet.ToArray(), response);
    }

    [Fact]
    public async Task TcpServerSendsServerFirstPayloadOnConnect()
    {
        await using var server = new ConquerTcpServer(
            new ConquerTcpServerOptions(
                ConquerRuntimeDefaults.AccountServer,
                IPAddress.Loopback,
                ListenPort: 0,
                ServerFirstPayloadFactory: () => new byte[] { 0xC5, 0x48, 0x69, 0x12 }),
            (_, _, _) => ValueTask.CompletedTask);

        server.Start();
        using var client = await ConnectAsync(server);

        var payload = new byte[4];
        await client.GetStream().ReadExactlyAsync(payload);
        Assert.Equal([0xC5, 0x48, 0x69, 0x12], payload);
    }

    [Fact]
    public async Task TcpServerLogsHandlerExceptionAsStructuredRuntimeGap()
    {
        using var output = new ThreadSafeStringWriter();
        using var error = new ThreadSafeStringWriter();
        await using var server = new ConquerTcpServer(
            new ConquerTcpServerOptions(
                ConquerRuntimeDefaults.WorldServer,
                IPAddress.Loopback,
                ListenPort: 0,
                Log: TextWriter.Synchronized(output),
                ErrorLog: TextWriter.Synchronized(error)),
            (_, _, _) => throw new InvalidOperationException("handler exploded"));

        server.Start();
        using var client = await ConnectAsync(server);
        await client.GetStream().WriteAsync(new PacketWriter(1010, payloadLength: 0).ToArray());

        // The error writer is the final publication in LogClientException. Seeing
        // the earlier summary writer does not mean this independent writer ran yet.
        var errorLog = await WaitForLogAsync(
            error,
            static text => text.Contains("InvalidOperationException: handler exploded", StringComparison.Ordinal));
        var log = await WaitForLogAsync(
            output,
            static text => text.Contains("PARITY-GAP category=runtime severity=error gap=tcp-client-exception", StringComparison.Ordinal));
        Assert.Contains("dedupe=runtime:tcp-client-exception:exceptionType=InvalidOperationException", log);
        Assert.Contains("nextSeam=inspect-tcp-client-pipeline", log);
        Assert.Contains("role=WorldServer", log);
        Assert.Contains("sourceSeam=ConquerTcpServer.HandleClientAsync.modern-packet-pipeline", log);
        Assert.Contains("stage=modern-packet-pipeline", log);
        Assert.Contains("wireMode=Modern", log);
        Assert.Contains("PARITY-BUG tcp client exception", log);
        Assert.Contains("exception=InvalidOperationException: handler exploded", log);
        Assert.Contains("InvalidOperationException: handler exploded", errorLog);
    }

    [Fact]
    public async Task TcpServerTreatsPeerDisconnectIOExceptionAsNormalClose()
    {
        using var output = new ThreadSafeStringWriter();
        using var error = new ThreadSafeStringWriter();
        await using var server = new ConquerTcpServer(
            new ConquerTcpServerOptions(
                ConquerRuntimeDefaults.WorldServer,
                IPAddress.Loopback,
                ListenPort: 0,
                Log: TextWriter.Synchronized(output),
                ErrorLog: TextWriter.Synchronized(error)),
            (_, _, _) => throw CreatePeerDisconnectIOException());

        server.Start();
        using var client = await ConnectAsync(server);
        await client.GetStream().WriteAsync(new PacketWriter(1010, payloadLength: 0).ToArray());

        var log = await WaitForLogAsync(
            output,
            static text => text.Contains("WorldServer closed client", StringComparison.Ordinal));
        Assert.DoesNotContain("PARITY-GAP", log);
        Assert.DoesNotContain("PARITY-BUG", log);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task TcpConnectionSendAfterClientCloseCompletesWithoutRuntimeGap()
    {
        using var output = new ThreadSafeStringWriter();
        using var error = new ThreadSafeStringWriter();
        var capturedConnection = new TaskCompletionSource<ConquerTcpConnection>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new ConquerTcpServer(
            new ConquerTcpServerOptions(
                ConquerRuntimeDefaults.WorldServer,
                IPAddress.Loopback,
                ListenPort: 0,
                Log: TextWriter.Synchronized(output),
                ErrorLog: TextWriter.Synchronized(error)),
            (connection, _, _) =>
            {
                capturedConnection.TrySetResult(connection);
                return ValueTask.CompletedTask;
            });

        server.Start();
        using (var client = await ConnectAsync(server))
        {
            await client.GetStream().WriteAsync(new PacketWriter(1010, payloadLength: 0).ToArray());
            _ = await capturedConnection.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        var logAfterClose = await WaitForLogAsync(
            output,
            static text => text.Contains("WorldServer closed client", StringComparison.Ordinal));
        var connection = await capturedConnection.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await connection.SendAsync(new PacketWriter(1011, payloadLength: 0).ToArray());

        Assert.DoesNotContain("PARITY-GAP", logAfterClose);
        Assert.DoesNotContain("PARITY-BUG", logAfterClose);
        Assert.DoesNotContain("PARITY-GAP", output.ToString());
        Assert.DoesNotContain("PARITY-BUG", output.ToString());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task TcpServerLogsIncompleteModernFrameWithBufferedByteCount()
    {
        using var output = new ThreadSafeStringWriter();
        using var error = new ThreadSafeStringWriter();
        await using var server = new ConquerTcpServer(
            new ConquerTcpServerOptions(
                ConquerRuntimeDefaults.WorldServer,
                IPAddress.Loopback,
                ListenPort: 0,
                Log: TextWriter.Synchronized(output),
                ErrorLog: TextWriter.Synchronized(error)),
            (_, _, _) => ValueTask.CompletedTask);

        server.Start();
        using (var client = await ConnectAsync(server))
        {
            await client.GetStream().WriteAsync(new byte[] { 0x04, 0x00, 0xF2 });
        }

        var log = await WaitForLogAsync(
            output,
            static text => text.Contains("remainingBytes=3", StringComparison.Ordinal));
        var errorLog = await WaitForLogAsync(
            error,
            static text => text.Contains("remainingBytes=3", StringComparison.Ordinal));
        Assert.Contains("PARITY-GAP category=runtime severity=error gap=tcp-client-exception", log);
        Assert.Contains("exceptionType=EndOfStreamException", log);
        Assert.Contains("sourceSeam=ConquerTcpServer.HandleClientAsync.modern-packet-pipeline", log);
        Assert.Contains("remainingBytes=3", log);
        Assert.Contains("maxFrameSize=6844", log);
        Assert.Contains("remainingBytes=3", errorLog);
    }

    private static ConquerTcpServer CreateLoopbackServer(
        Func<ConquerTcpConnection, PacketFrame, CancellationToken, ValueTask> handleFrame)
    {
        return new ConquerTcpServer(
            new ConquerTcpServerOptions(ConquerRuntimeDefaults.WorldServer, IPAddress.Loopback, ListenPort: 0),
            handleFrame);
    }

    private static async Task<TcpClient> ConnectAsync(ConquerTcpServer server)
    {
        var endpoint = server.BoundEndPoint;
        var client = new TcpClient();
        await client.ConnectAsync(endpoint.Address, endpoint.Port);
        return client;
    }

    private static IOException CreatePeerDisconnectIOException()
    {
        return new IOException(
            "Unable to read data from the transport connection.",
            new SocketException((int)SocketError.ConnectionReset));
    }

    private sealed class ThreadSafeStringWriter : StringWriter
    {
        private readonly object _gate = new();

        public override void Write(char value)
        {
            lock (_gate)
            {
                base.Write(value);
            }
        }

        public override void Write(string? value)
        {
            lock (_gate)
            {
                base.Write(value);
            }
        }

        public override void WriteLine(string? value)
        {
            lock (_gate)
            {
                base.WriteLine(value);
            }
        }

        public override Task WriteAsync(string? value)
        {
            lock (_gate)
            {
                base.Write(value);
            }

            return Task.CompletedTask;
        }

        public override Task WriteLineAsync(string? value)
        {
            lock (_gate)
            {
                base.WriteLine(value);
            }

            return Task.CompletedTask;
        }

        public override string ToString()
        {
            lock (_gate)
            {
                return base.ToString();
            }
        }
    }

    private static async Task<string> WaitForLogAsync(StringWriter writer, Func<string, bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var text = writer.ToString();
            if (predicate(text))
            {
                return text;
            }

            await Task.Delay(25).ConfigureAwait(false);
        }

        return writer.ToString();
    }
}

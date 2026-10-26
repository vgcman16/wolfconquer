using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Channels;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

public sealed class LegacyNativePlayerNetworkBirthTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyGameDispatchHasPhysicalOwnerBeforeAnyLogin(bool coalesced)
    {
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            _ = await fixture.ConnectGameAsync(coalesced);
            var seen = await fixture.NextAsync();
            Assert.Equal(NativePlayerLoopback.ProbeOpcode, seen.Opcode);
            var owner = Assert.IsType<LegacyNativeGameClientRuntime>(seen.Connection.NativeGameClient);
            var player = Assert.IsType<LegacyNativePlayerRuntime>(owner.Player);
            Assert.Same(owner, player.Owner);
            Assert.NotNull(player.MyRandom);
            Assert.NotNull(player.BitVector);
        });
    }

    [Fact]
    public async Task SeparateGameSocketsProduceDistinctPhysicalOwnersAndPlayers()
    {
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            _ = await fixture.ConnectGameAsync(false);
            var first = await fixture.NextAsync();
            _ = await fixture.ConnectGameAsync(false);
            var second = await fixture.NextAsync();
            Assert.Equal(NativePlayerLoopback.ProbeOpcode, first.Opcode);
            Assert.Equal(NativePlayerLoopback.ProbeOpcode, second.Opcode);
            var a = Assert.IsType<LegacyNativeGameClientRuntime>(first.Connection.NativeGameClient);
            var b = Assert.IsType<LegacyNativeGameClientRuntime>(second.Connection.NativeGameClient);
            Assert.NotSame(first.Connection, second.Connection);
            Assert.NotSame(a, b);
            Assert.NotSame(a.Player, b.Player);
            Assert.NotSame(a.Player.MyRandom, b.Player.MyRandom);
            Assert.NotSame(a.Player.BitVector, b.Player.BitVector);
            Assert.Same(a, a.Player.Owner);
            Assert.Same(b, b.Player.Owner);
        });
    }

    [Theory]
    [InlineData(ConquerTcpWireMode.Modern)]
    [InlineData(ConquerTcpWireMode.LegacyAccountServer)]
    public async Task NongameSocketTransportsDoNotInventPhysicalPlayerOwners(ConquerTcpWireMode mode)
    {
        await NativePlayerLoopback.RunAsync(mode, async fixture =>
        {
            var client = await fixture.ConnectNongameAsync();
            await client.SendAsync(NativePlayerLoopback.ProbeFrame(), fixture.Token);
            var seen = await fixture.NextAsync();
            Assert.Equal(NativePlayerLoopback.ProbeOpcode, seen.Opcode);
            Assert.Null(seen.Connection.NativeGameClient);
        });
    }
}

// Shared loopback machinery has NO dependency on the optional session-observation API.
// The network-only owner above can be staged independently of the adoption fixture/observer.
internal sealed class NativePlayerLoopback : IAsyncDisposable
{
    internal const ushort ProbeOpcode = 65000;
    private readonly CancellationTokenSource _stop = new(TimeSpan.FromSeconds(15));
    private readonly Channel<NativePlayerDispatch> _seen = Channel.CreateUnbounded<NativePlayerDispatch>();
    private readonly List<TcpClient> _clients = [];
    private readonly List<Task> _drains = [];
    private readonly List<NativePlayerLoopbackOutputCapture> _outputCaptures = [];
    private readonly ConquerTcpWireMode _mode;
    private readonly ConquerTcpServer _server;
    private readonly NativePlayerDiagnosticLog _transportLog = new();
    private readonly NativePlayerDiagnosticLog _transportErrors = new();

    private NativePlayerLoopback(
        ConquerTcpWireMode mode,
        Func<ConquerTcpConnection, PacketFrame, CancellationToken, ValueTask>? handleFrame,
        Func<ConquerTcpConnection, WorldSession?>? observe)
    {
        _mode = mode;
        var endpoint = mode == ConquerTcpWireMode.LegacyAccountServer
            ? ConquerRuntimeDefaults.AccountServer : ConquerRuntimeDefaults.WorldServer;
        _server = new ConquerTcpServer(
            new ConquerTcpServerOptions(
                endpoint, IPAddress.Loopback, ListenPort: 0, WireMode: mode,
                ServerFirstPayloadFactory: mode == ConquerTcpWireMode.LegacyAccountServer
                    ? () => ConquerServerFirstPayloads.Create(ConquerRuntimeDefaults.AccountServer)
                    : null,
                Log: _transportLog, ErrorLog: _transportErrors),
            async (connection, frame, token) =>
            {
                try
                {
                    var before = observe?.Invoke(connection);
                    if (frame.Header.Opcode != ProbeOpcode && handleFrame is not null)
                        await handleFrame(connection, frame, token);
                    var after = observe?.Invoke(connection);
                    _seen.Writer.TryWrite(new(connection, frame.Header.Opcode, before, after));
                }
                catch (Exception error)
                {
                    // Preserve the original handler failure in the awaited channel and server log.
                    _seen.Writer.TryComplete(error);
                    throw;
                }
            });
    }

    internal CancellationToken Token => _stop.Token;

    internal static async Task RunAsync(
        ConquerTcpWireMode mode,
        Func<NativePlayerLoopback, Task> body,
        Func<ConquerTcpConnection, PacketFrame, CancellationToken, ValueTask>? handleFrame = null,
        Func<ConquerTcpConnection, WorldSession?>? observe = null)
    {
        var fixture = new NativePlayerLoopback(mode, handleFrame, observe);
        Exception? primary = null;
        Exception? cleanup = null;
        try { fixture._server.Start(fixture.Token); await body(fixture); }
        catch (Exception error) { primary = error; }
        try { await fixture.DisposeAsync(); }
        catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null)
            throw new AggregateException("Loopback assertion/operation and cleanup both failed.", primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
    }

    internal async ValueTask<NativePlayerDispatch> NextAsync()
    {
        var seen = await _seen.Reader.ReadAsync(Token);
        AssertNoTransportDiagnostics();
        return seen;
    }

    private void AssertNoTransportDiagnostics()
    {
        var regular = _transportLog.Text;
        var errors = _transportErrors.Text;
        Assert.False(regular.Contains("PARITY-BUG tcp ", StringComparison.Ordinal), regular);
        Assert.True(errors.Length == 0, errors);
        // Silent peer-disconnect catches are NOT disproved by absence of diagnostics.
    }

    private async Task<TcpClient> ConnectAsync()
    {
        var client = new TcpClient();
        _clients.Add(client); // Own it BEFORE the first awaited socket operation.
        await client.ConnectAsync(_server.BoundEndPoint.Address, _server.BoundEndPoint.Port, Token);
        return client;
    }

    internal async Task<NativePlayerWireClient> ConnectGameAsync(bool coalesced, bool captureOutput = false)
    {
        if (_mode != ConquerTcpWireMode.LegacyGameServer)
            throw new InvalidOperationException("Game handshake requested on nongame fixture.");
        var socket = await ConnectAsync();
        var stream = socket.GetStream();
        var plain = await ReadServerHandshakeAsync(stream, Token);
        var offset = 53;
        var prime = ReadBlob(plain, ref offset);
        var generator = ReadBlob(plain, ref offset);
        var serverPublic = ReadBlob(plain, ref offset);
        var dh = new LegacyGameServerDiffieHellman(prime, generator);
        // Real client-side CSPRNG, not a forced exponent or a gameplay Pool seed/result.
        var clientPublic = dh.GenerateRequest();
        dh.HandleResponse(serverPublic);
        var material = new byte[LegacyGameServerCipher.KeyMaterialSize];
        dh.DeriveKeyMaterial(material);
        var outbound = new LegacyGameServerCipher(material);
        var capture = captureOutput ? new NativePlayerLoopbackOutputCapture() : null;
        if (capture is not null) _outputCaptures.Add(capture);
        var client = new NativePlayerWireClient(stream, outbound, null, capture);
        var reply = ClientHandshakeReply(clientPublic);
        new LegacyGameServerCipher(Encoding.ASCII.GetBytes(LegacyGameServerCipher.LoginKey))
            .EncryptInPlace(reply);
        if (coalesced)
        {
            var first = LegacyGameServerFrameCodec.EncodeEncrypted(
                ProbeFrame(), outbound, LegacyGameServerFrameSeal.Client);
            var combined = new byte[reply.Length + first.Length];
            reply.CopyTo(combined, 0);
            first.CopyTo(combined, reply.Length);
            await stream.WriteAsync(combined, Token);
        }
        else
        {
            await stream.WriteAsync(reply, Token);
            await client.SendAsync(ProbeFrame(), Token);
        }
        _drains.Add(DrainOutputAsync(stream, Token, capture));
        return client;
    }

    internal async Task<NativePlayerWireClient> ConnectNongameAsync()
    {
        if (_mode == ConquerTcpWireMode.LegacyGameServer)
            throw new InvalidOperationException("Nongame connection requested on game fixture.");
        var socket = await ConnectAsync();
        var stream = socket.GetStream();
        LegacyAccServerCipher? accountCipher = null;
        if (_mode == ConquerTcpWireMode.LegacyAccountServer)
        {
            var seed = new byte[LegacyAccountPasswordSeedPacket.FrameLength];
            await stream.ReadExactlyAsync(seed, Token);
            new LegacyAccServerCipher().DecryptFromServer(seed);
            Assert.True(PacketHeader.TryRead(seed, out var parsed));
            Assert.Equal(LegacyAccountPasswordSeedPacket.Opcode, parsed.Opcode);
            Assert.Equal(LegacyAccountPasswordSeedPacket.FrameLength, parsed.Length);
            accountCipher = new LegacyAccServerCipher();
        }
        _drains.Add(DrainOutputAsync(stream, Token));
        return new NativePlayerWireClient(stream, null, accountCipher);
    }

    internal static byte[] ProbeFrame() => new PacketWriter(ProbeOpcode, payloadLength: 0).ToArray();

    internal async Task CloseClientTransportAsync(int clientIndex = 0)
    {
        if (clientIndex < 0 || clientIndex >= _clients.Count || clientIndex >= _drains.Count)
            throw new ArgumentOutOfRangeException(nameof(clientIndex));
        var client = _clients[clientIndex];
        // Exercise a genuine peer half-close and observe the already-owned read
        // reaching server EOF. A timed-out/canceled drain is not closure proof.
        client.Client.Shutdown(SocketShutdown.Send);
        await _drains[clientIndex].ConfigureAwait(false);
        Token.ThrowIfCancellationRequested();
        client.Dispose();
        AssertNoTransportDiagnostics();
    }

    private static async Task DrainOutputAsync(NetworkStream stream, CancellationToken token,
        NativePlayerLoopbackOutputCapture? capture = null)
    {
        try
        {
            if (capture is null) { await stream.CopyToAsync(Stream.Null, token); return; }
            var buffer = new byte[4096];
            while (true)
            {
                var count = await stream.ReadAsync(buffer, token);
                if (count == 0) { capture.CompleteEof(); return; }
                capture.Append(buffer.AsSpan(0, count));
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        // IO/decoder/other failures are NOT swallowed as a successful ownership assertion.
    }

    public async ValueTask DisposeAsync()
    {
        var failures = new List<Exception>();
        try { _stop.Cancel(); } catch (Exception error) { failures.Add(error); }
        // All original background reads remain owned and are observed; no abandoned WaitAsync task.
        foreach (var drain in _drains)
            try { await drain; } catch (Exception error) { failures.Add(error); }
        foreach (var capture in _outputCaptures)
            try { capture.Dispose(); } catch (Exception error) { failures.Add(error); }
        foreach (var client in _clients)
            try { client.Dispose(); } catch (Exception error) { failures.Add(error); }
        try { await _server.DisposeAsync(); } catch (Exception error) { failures.Add(error); }
        _seen.Writer.TryComplete();
        try { AssertNoTransportDiagnostics(); } catch (Exception error) { failures.Add(error); }
        _transportLog.Dispose();
        _transportErrors.Dispose();
        _stop.Dispose();
        if (failures.Count != 0)
            throw new AggregateException("Loopback cleanup failed.", failures);
    }

    private static async Task<byte[]> ReadServerHandshakeAsync(NetworkStream stream, CancellationToken token)
    {
        var login = new LegacyGameServerCipher(Encoding.ASCII.GetBytes(LegacyGameServerCipher.LoginKey));
        var header = new byte[LegacyGameServerHandshakePacket.LengthFieldOffset + sizeof(int)];
        await stream.ReadExactlyAsync(header, token);
        login.DecryptInPlace(header);
        var length = checked(BinaryPrimitives.ReadInt32LittleEndian(
            header.AsSpan(LegacyGameServerHandshakePacket.LengthFieldOffset, sizeof(int))) +
            LegacyGameServerHandshakePacket.LengthFieldAdjustment);
        if (length <= LegacyGameServerHandshakePacket.FixedLength || length > PacketHeader.DefaultMaxFrameSize)
            throw new InvalidDataException("Invalid server handshake size.");
        var tail = new byte[length - header.Length];
        await stream.ReadExactlyAsync(tail, token);
        login.DecryptInPlace(tail);
        var plain = new byte[length];
        header.CopyTo(plain, 0);
        tail.CopyTo(plain, header.Length);
        Assert.Equal("TQServer", Encoding.ASCII.GetString(plain.AsSpan(length - 8)));
        Assert.Equal(LegacyGameServerHandshakePacket.HandshakeType,
            BinaryPrimitives.ReadInt32LittleEndian(plain.AsSpan(15, sizeof(int))));
        return plain;
    }

    private static string ReadBlob(byte[] plain, ref int offset)
    {
        var length = BinaryPrimitives.ReadInt32LittleEndian(plain.AsSpan(offset, sizeof(int)));
        offset += sizeof(int);
        var value = Encoding.ASCII.GetString(plain.AsSpan(offset, length));
        offset += length;
        return value;
    }

    private static byte[] ClientHandshakeReply(string publicKey)
    {
        const int segmentLength = 17;
        var key = Encoding.ASCII.GetBytes(publicKey);
        var keyOffset = segmentLength + LegacyGameServerHandshakeReply.KeyLengthFieldAdjustment;
        var bytesOffset = keyOffset + sizeof(int);
        var seal = Encoding.ASCII.GetBytes(LegacyGameServerHandshakeReply.Seal);
        var result = new byte[bytesOffset + key.Length + seal.Length];
        BinaryPrimitives.WriteInt32LittleEndian(
            result.AsSpan(LegacyGameServerHandshakeReply.LengthFieldOffset, sizeof(int)), segmentLength);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(keyOffset, sizeof(int)), key.Length);
        key.CopyTo(result, bytesOffset);
        seal.CopyTo(result, bytesOffset + key.Length);
        return result;
    }
}


internal sealed class NativePlayerDiagnosticLog : TextWriter
{
    private readonly object gate = new();
    private readonly StringBuilder text = new();
    public override Encoding Encoding => System.Text.Encoding.UTF8;
    internal string Text { get { lock (gate) return text.ToString(); } }
    internal Action<string>? ObserveLine { get; set; }
    public override void WriteLine(string? value)
    {
        lock (gate) text.AppendLine(value);
        ObserveLine?.Invoke(value ?? ""); // Observation only, after the actual diagnostic append.
    }
    public override Task WriteLineAsync(string? value)
    {
        WriteLine(value);
        return Task.CompletedTask;
    }

    internal void AssertNoWorldRuntimeDiagnostics()
    {
        var captured = Text;
        Assert.False(captured.Contains("PARITY-BUG world runtime exception", StringComparison.Ordinal), captured);
        Assert.False(captured.Contains("WorldServer legacy startup send peer-disconnect ", StringComparison.Ordinal), captured);
        // Scope: these actual diagnostic paths only, not every silently classified peer disconnect.
    }
}

internal sealed record NativePlayerDispatch(
    ConquerTcpConnection Connection, ushort Opcode, WorldSession? Before, WorldSession? After);

internal sealed class NativePlayerWireClient(
    NetworkStream stream, LegacyGameServerCipher? game, LegacyAccServerCipher? account,
    NativePlayerLoopbackOutputCapture? serverOutput = null)
{
    private byte[][]? decodedOutput;

    internal byte[][] CopyCapturedServerFramesAtEof()
    {
        if (serverOutput is null || game is null)
            throw new InvalidOperationException("Captured native game output is required.");
        if (decodedOutput is null)
        {
            var plain = serverOutput.CopyAtEof();
            game.DecryptInPlace(plain); // The actual negotiated cipher has separate receive state.
            var frames = new List<byte[]>();
            var offset = 0;
            while (offset < plain.Length)
            {
                if (!LegacyGameServerFrameCodec.TryStripSeal(plain.AsSpan(offset), out var frame, out var consumed))
                    throw new InvalidDataException("Captured server output ends inside a frame.");
                frames.Add(frame);
                offset = checked(offset + consumed);
            }
            decodedOutput = frames.ToArray();
        }
        return decodedOutput.Select(frame => frame.ToArray()).ToArray();
    }

    internal async Task SendAsync(byte[] frame, CancellationToken token)
    {
        var wire = game is not null
            ? LegacyGameServerFrameCodec.EncodeEncrypted(frame, game, LegacyGameServerFrameSeal.Client)
            : frame.ToArray();
        account?.EncryptForServer(wire);
        await stream.WriteAsync(wire, token);
    }
}

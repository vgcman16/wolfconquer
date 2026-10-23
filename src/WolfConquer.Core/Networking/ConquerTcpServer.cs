using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Networking;

public sealed class ConquerTcpServer : IAsyncDisposable
{
    private static readonly byte[] LegacyClientSeal = Encoding.ASCII.GetBytes(LegacyGameServerHandshakeReply.Seal);

    private readonly ConquerTcpServerOptions _options;
    private readonly Func<ConquerTcpConnection, PacketFrame, CancellationToken, ValueTask> _handleFrame;
    private readonly TcpListener _listener;
    private readonly TextWriter _log;
    private readonly TextWriter _errorLog;
    private readonly List<Task> _clientTasks = [];
    private readonly object _gate = new();
    private CancellationTokenSource? _stop;
    private Task? _acceptTask;

    public ConquerTcpServer(
        ConquerTcpServerOptions options,
        Func<ConquerTcpConnection, PacketFrame, CancellationToken, ValueTask> handleFrame)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(handleFrame);

        if (options.ListenPort is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Listen port must be between 0 and 65535.");
        }

        _options = options;
        _handleFrame = handleFrame;
        _listener = new TcpListener(options.ListenAddress, options.ListenPort);
        _log = options.Log ?? Console.Out;
        _errorLog = options.ErrorLog ?? Console.Error;
    }

    public ConquerTcpServerOptions Options => _options;

    public IPEndPoint BoundEndPoint => (IPEndPoint)_listener.LocalEndpoint;

    public bool IsRunning => _acceptTask is { IsCompleted: false };

    public void Start(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_acceptTask is not null)
            {
                throw new InvalidOperationException("TCP server is already started.");
            }

            _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (_options.WireMode == ConquerTcpWireMode.LegacyGameServer)
                LegacyNativePoolRandom.InitializeAtServerStartup();
            _listener.Start();
            _acceptTask = AcceptLoopAsync(_stop.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task? acceptTask;
        CancellationTokenSource? stop;

        lock (_gate)
        {
            acceptTask = _acceptTask;
            stop = _stop;
        }

        if (stop is null)
        {
            return;
        }

        await stop.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        await IgnoreShutdownExceptionAsync(acceptTask).ConfigureAwait(false);
        // An accept already in progress may register its client while shutdown
        // is starting. Only snapshot after that producer has fully stopped.
        Task[] clientTasks;
        lock (_gate)
        {
            clientTasks = _clientTasks.ToArray();
        }
        await IgnoreShutdownExceptionAsync(Task.WhenAll(clientTasks)).ConfigureAwait(false);
        stop.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _log.WriteLine(
                    $"{_options.Endpoint.Role} accepted client {client.Client.RemoteEndPoint} on {client.Client.LocalEndPoint}");
                var task = HandleClientAsync(client, cancellationToken);

                lock (_gate)
                {
                    _clientTasks.Add(task);
                }

                _ = task.ContinueWith(
                    completed =>
                    {
                        lock (_gate)
                        {
                            _clientTasks.Remove(completed);
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            LogAcceptLoopException(ex);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var remoteEndPoint = client.Client.RemoteEndPoint;
        var localEndPoint = client.Client.LocalEndPoint;
        using var _ = client;
        using var writeGate = new SemaphoreSlim(1, 1);
        using var clientStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var clientCancellationToken = clientStop.Token;
        var stream = client.GetStream();
        var pipelineStage = "modern-packet-pipeline";
        var connectionClosed = 0;

        bool IsConnectionClosed()
        {
            return Volatile.Read(ref connectionClosed) != 0;
        }

        void MarkConnectionClosed()
        {
            Interlocked.Exchange(ref connectionClosed, 1);
            try
            {
                clientStop.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        async ValueTask WritePlainAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            await stream.WriteAsync(bytes, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            return WriteWithGateAsync(
                bytes,
                writeGate,
                token,
                WritePlainAsync,
                IsConnectionClosed,
                MarkConnectionClosed);
        }

        var connection = new ConquerTcpConnection(
            client.Client.LocalEndPoint,
            client.Client.RemoteEndPoint,
            WriteAsync,
            _ =>
            {
                MarkConnectionClosed();
                client.Close();
                return ValueTask.CompletedTask;
            });
        var reader = PipeReader.Create(stream);

        try
        {
            if (_options.WireMode == ConquerTcpWireMode.LegacyGameServer)
            {
                pipelineStage = "legacy-game-server-pipeline";
                await HandleLegacyGameServerClientAsync(
                        client,
                        stream,
                        reader,
                        writeGate,
                        clientCancellationToken,
                        IsConnectionClosed,
                        MarkConnectionClosed,
                        created => connection = created)
                    .ConfigureAwait(false);
                return;
            }

            if (_options.WireMode == ConquerTcpWireMode.LegacyAccountServer)
            {
                pipelineStage = "legacy-account-server-pipeline";
                await HandleLegacyAccountServerClientAsync(
                        client,
                        stream,
                        reader,
                        writeGate,
                        clientCancellationToken,
                        IsConnectionClosed,
                        MarkConnectionClosed,
                        created => connection = created)
                    .ConfigureAwait(false);
                return;
            }

            if (_options.WireMode == ConquerTcpWireMode.LegacyGuardShield)
            {
                pipelineStage = "guard-shield-text-protocol";
                await HandleLegacyGuardShieldClientAsync(
                        stream,
                        reader,
                        writeGate,
                        clientCancellationToken,
                        IsConnectionClosed,
                        MarkConnectionClosed)
                    .ConfigureAwait(false);
                return;
            }

            if (_options.ServerFirstPayloadFactory?.Invoke() is { Length: > 0 } serverFirstPayload)
            {
                pipelineStage = "server-first-payload";
                await WriteAsync(serverFirstPayload, clientCancellationToken).ConfigureAwait(false);
            }

            pipelineStage = "modern-packet-pipeline";
            await PacketPipeline.ReadFramesAsync(
                reader,
                (frame, token) => _handleFrame(connection, frame, token),
                _options.MaxFrameSize,
                clientCancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (clientCancellationToken.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (clientCancellationToken.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (NetworkExceptionClassifier.IsPeerDisconnect(ex))
        {
        }
        catch (Exception ex)
        {
            LogClientException(remoteEndPoint, localEndPoint, pipelineStage, ex);
        }
        finally
        {
            MarkConnectionClosed();
            await clientStop.CancelAsync().ConfigureAwait(false);
            // Finish world retirement before normal stream disposal publishes EOF.
            // This runs after dispatch unwinds, never inside a send/connection gate.
            await connection.CompleteTransportCloseAsync().ConfigureAwait(false);
            await reader.CompleteAsync().ConfigureAwait(false);
            _log.WriteLine($"{_options.Endpoint.Role} closed client {remoteEndPoint} on {localEndPoint}");
        }
    }

    private static async ValueTask WriteWithGateAsync(
        ReadOnlyMemory<byte> bytes,
        SemaphoreSlim writeGate,
        CancellationToken token,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> writeAsync,
        Func<bool> isConnectionClosed,
        Action markConnectionClosed,
        Action<ReadOnlyMemory<byte>, string>? skippedWrite = null)
    {
        if (isConnectionClosed())
        {
            skippedWrite?.Invoke(bytes, "closed-before-gate");
            return;
        }

        var entered = false;
        try
        {
            await writeGate.WaitAsync(token).ConfigureAwait(false);
            entered = true;

            if (isConnectionClosed())
            {
                skippedWrite?.Invoke(bytes, "closed-after-gate");
                return;
            }

            await writeAsync(bytes, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || isConnectionClosed())
        {
            skippedWrite?.Invoke(bytes, "canceled");
        }
        catch (ObjectDisposedException) when (token.IsCancellationRequested || isConnectionClosed())
        {
            skippedWrite?.Invoke(bytes, "disposed");
        }
        catch (Exception ex) when (NetworkExceptionClassifier.IsPeerDisconnect(ex))
        {
            markConnectionClosed();
            skippedWrite?.Invoke(bytes, "peer-disconnect");
        }
        finally
        {
            if (entered)
            {
                try
                {
                    writeGate.Release();
                }
                catch (ObjectDisposedException) when (isConnectionClosed())
                {
                }
            }
        }
    }

    private async Task HandleLegacyAccountServerClientAsync(
        TcpClient client,
        NetworkStream stream,
        PipeReader reader,
        SemaphoreSlim writeGate,
        CancellationToken cancellationToken,
        Func<bool> isConnectionClosed,
        Action markConnectionClosed,
        Action<ConquerTcpConnection> onConnectionCreated)
    {
        var cipher = new LegacyAccServerCipher();
        var traceLegacyAccountAuth = IsTruthyEnvironment(
            Environment.GetEnvironmentVariable("WOLFCONQUER_TRACE_LEGACY_ACCOUNT_AUTH"));

        async ValueTask WriteEncryptedAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            var plain = bytes.ToArray();
            var encrypted = plain.ToArray();
            var encryptCounterBefore = cipher.EncryptCounter;
            cipher.Encrypt(encrypted);
            if (traceLegacyAccountAuth)
            {
                LogLegacyAccountOutboundTrace(plain, encrypted, encryptCounterBefore, cipher.EncryptCounter);
            }

            await stream.WriteAsync(encrypted, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        ValueTask WriteLegacyAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            return WriteWithGateAsync(
                bytes,
                writeGate,
                token,
                WriteEncryptedAsync,
                isConnectionClosed,
                markConnectionClosed);
        }

        if (_options.ServerFirstPayloadFactory?.Invoke() is { Length: > 0 } serverFirstPayload)
        {
            await WriteLegacyAsync(serverFirstPayload, cancellationToken).ConfigureAwait(false);
        }

        var connection = new ConquerTcpConnection(
            client.Client.LocalEndPoint,
            client.Client.RemoteEndPoint,
            WriteLegacyAsync,
            _ =>
            {
                markConnectionClosed();
                client.Close();
                return ValueTask.CompletedTask;
            });

        onConnectionCreated(connection);

        await LegacyAccServerPacketPipeline.ReadFramesAsync(
            reader,
            cipher,
            (frame, token) => _handleFrame(connection, frame, token),
            _options.MaxFrameSize,
            cancellationToken,
            traceLegacyAccountAuth ? LogLegacyAccountInboundTrace : null).ConfigureAwait(false);
    }

    private void LogLegacyAccountOutboundTrace(
        ReadOnlySpan<byte> plainBytes,
        ReadOnlySpan<byte> encryptedBytes,
        ushort encryptCounterBefore,
        ushort encryptCounterAfter)
    {
        _log.WriteLine(
            "LegacyAccountWireTrace outbound " +
            $"counter={encryptCounterBefore}->{encryptCounterAfter} " +
            $"{DescribeLegacyAccountFrame(plainBytes)} " +
            $"plainHex={HexForLog(plainBytes)} encryptedHex={HexForLog(encryptedBytes)}");
    }

    private void LogLegacyAccountInboundTrace(LegacyAccServerPacketChunkTrace trace)
    {
        _log.WriteLine(
            "LegacyAccountWireTrace inbound " +
            $"counter={trace.DecryptCounterBefore}->{trace.DecryptCounterAfter} " +
            $"encryptedLength={trace.EncryptedBytes.Length} decryptedLength={trace.DecryptedBytes.Length} " +
            $"bufferedBefore={trace.BufferedBytesBeforeAppend} " +
            $"{DescribeLegacyAccountFrame(trace.DecryptedBytes)} " +
            $"encryptedHex={HexForLog(trace.EncryptedBytes)} decryptedHex={HexForLog(trace.DecryptedBytes)}");
    }

    private void LogLegacyGameWireTrace(string message)
    {
        _log.WriteLine($"LegacyGameWireTrace {message}");
    }

    private async Task HandleLegacyGuardShieldClientAsync(
        NetworkStream stream,
        PipeReader reader,
        SemaphoreSlim writeGate,
        CancellationToken cancellationToken,
        Func<bool> isConnectionClosed,
        Action markConnectionClosed)
    {
        async ValueTask WritePlainAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            await stream.WriteAsync(bytes, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        ValueTask WriteRawAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            return WriteWithGateAsync(
                bytes,
                writeGate,
                token,
                WritePlainAsync,
                isConnectionClosed,
                markConnectionClosed);
        }

        await LegacyGuardShieldTextProtocol.ReadCommandsAsync(
            reader,
            WriteRawAsync,
            _options.GuardShieldTextProtocol,
            _options.MaxFrameSize,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleLegacyGameServerClientAsync(
        TcpClient client,
        NetworkStream stream,
        PipeReader reader,
        SemaphoreSlim writeGate,
        CancellationToken cancellationToken,
        Func<bool> isConnectionClosed,
        Action markConnectionClosed,
        Action<ConquerTcpConnection> onConnectionCreated)
    {
        var traceLegacyGameWire = IsTruthyEnvironment(
            Environment.GetEnvironmentVariable("WOLFCONQUER_TRACE_LEGACY_GAME_WIRE"));

        async ValueTask WritePlainAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            await stream.WriteAsync(bytes, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        ValueTask WriteRawAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            return WriteWithGateAsync(
                bytes,
                writeGate,
                token,
                WritePlainAsync,
                isConnectionClosed,
                markConnectionClosed);
        }

        // Native GameClient constructs its physical Player BEFORE the first DH send.
        // This is intentionally only the legacy game path, not account/generic transport creation.
        // Allocate Player after DH object construction but BEFORE GenerateRequest/server-key send.
        LegacyNativeGameClientRuntime? nativeGameClient = null;
        var (inboundCipher, outboundCipher) = await CompleteLegacyGameServerHandshakeAsync(
            reader,
            WriteRawAsync,
            cancellationToken,
            traceLegacyGameWire ? LogLegacyGameWireTrace : null,
            () => nativeGameClient = new LegacyNativeGameClientRuntime()).ConfigureAwait(false);

        async ValueTask WriteEncryptedAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            var plain = bytes.ToArray();
            var encrypted = LegacyGameServerFrameCodec.EncodeEncrypted(
                plain,
                outboundCipher,
                _options.MaxFrameSize);
            if (traceLegacyGameWire)
            {
                LogLegacyGameWireTrace(
                    "outbound-frame " +
                    $"{DescribeLegacyGameFrame(plain)} " +
                    $"plainHex={HexForLog(plain)} encryptedHex={HexForLog(encrypted)}");
            }

            await stream.WriteAsync(encrypted, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        void LogSkippedLegacyGameWrite(ReadOnlyMemory<byte> bytes, string reason)
        {
            if (!traceLegacyGameWire)
            {
                return;
            }

            var plain = bytes.ToArray();
            LogLegacyGameWireTrace(
                "outbound-frame-skipped " +
                $"reason={reason} " +
                $"{DescribeLegacyGameFrame(plain)} " +
                $"plainHex={HexForLog(plain)}");
        }

        ValueTask WriteLegacyAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            return WriteWithGateAsync(
                bytes,
                writeGate,
                token,
                WriteEncryptedAsync,
                isConnectionClosed,
                markConnectionClosed,
                LogSkippedLegacyGameWrite);
        }

        var connection = new ConquerTcpConnection(
            client.Client.LocalEndPoint,
            client.Client.RemoteEndPoint,
            WriteLegacyAsync,
            _ =>
            {
                markConnectionClosed();
                client.Close();
                return ValueTask.CompletedTask;
            },
            nativeGameClient ?? throw new InvalidOperationException("Native Player birth was not reached."));

        onConnectionCreated(connection);

        await LegacyGameServerPacketPipeline.ReadFramesAsync(
            reader,
            inboundCipher,
            (frame, token) => _handleFrame(connection, frame, token),
            _options.MaxFrameSize,
            cancellationToken,
            LegacyGameServerFrameSeal.Client,
            traceLegacyGameWire ? LogLegacyGameWireTrace : null).ConfigureAwait(false);
    }

    private static async ValueTask<(LegacyGameServerCipher InboundCipher, LegacyGameServerCipher OutboundCipher)>
        CompleteLegacyGameServerHandshakeAsync(
            PipeReader reader,
            Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> writeRawAsync,
            CancellationToken cancellationToken,
            Action<string>? trace = null,
            Action? beforeServerKeyExchange = null)
    {
        var diffieHellman = new LegacyGameServerDiffieHellman();
        // Native GameClient: DH object/crypto setup -> new Player -> CreateServerKeyPacket.
        beforeServerKeyExchange?.Invoke();
        var serverPublicKey = diffieHellman.GenerateRequest();
        var serverHandshake = LegacyGameServerHandshakePacket.BuildWithLegacyParameters(serverPublicKey);
        var plainServerHandshake = serverHandshake.ToArray();
        var loginEncryptCipher = CreateLegacyLoginCipher();
        loginEncryptCipher.EncryptInPlace(serverHandshake);
        trace?.Invoke(
            "handshake-outbound " +
            $"plainLength={plainServerHandshake.Length} encryptedLength={serverHandshake.Length} " +
            $"plainHex={HexForLog(plainServerHandshake)} encryptedHex={HexForLog(serverHandshake)}");
        await writeRawAsync(serverHandshake, cancellationToken).ConfigureAwait(false);

        var loginDecryptCipher = CreateLegacyLoginCipher();
        var decryptedHandshake = new List<byte>();
        long decryptedLength = 0;

        while (true)
        {
            var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var sequence = result.Buffer;
            var advanced = false;

            try
            {
                if (sequence.Length > decryptedLength)
                {
                    var newlyEncrypted = sequence.Slice(decryptedLength).ToArray();
                    var encryptedSample = newlyEncrypted.ToArray();
                    loginDecryptCipher.DecryptInPlace(newlyEncrypted);
                    decryptedHandshake.AddRange(newlyEncrypted);
                    decryptedLength = sequence.Length;
                    trace?.Invoke(
                        "handshake-inbound-chunk " +
                        $"encryptedLength={encryptedSample.Length} decryptedLength={newlyEncrypted.Length} " +
                        $"decryptedTotal={decryptedHandshake.Count} " +
                        $"encryptedHex={HexForLog(encryptedSample)} decryptedHex={HexForLog(newlyEncrypted)}");
                }

                var handshakeLength = FindLegacyClientHandshakeLength(decryptedHandshake);
                if (handshakeLength >= 0)
                {
                    var handshakePayload = decryptedHandshake.Take(handshakeLength).ToArray();
                    if (!LegacyGameServerHandshakeReply.TryReadPublicKey(handshakePayload, out var clientPublicKey))
                    {
                        throw new InvalidDataException("Legacy GameServer DH reply did not contain a public key.");
                    }

                    trace?.Invoke(
                        "handshake-inbound-complete " +
                        $"length={handshakeLength} clientPublicKeyLength={clientPublicKey.Length} " +
                        $"bufferedPlainBytes={decryptedHandshake.Count}");
                    diffieHellman.HandleResponse(clientPublicKey);
                    var keyMaterial = new byte[LegacyGameServerCipher.KeyMaterialSize];
                    diffieHellman.DeriveKeyMaterial(keyMaterial);

                    var handshakeEnd = sequence.GetPosition(handshakeLength);
                    // A client may coalesce its DH reply and first game frame. Leave any
                    // post-handshake bytes unexamined so the game-frame pipeline sees them now.
                    reader.AdvanceTo(handshakeEnd, handshakeEnd);
                    advanced = true;

                    return (
                        new LegacyGameServerCipher(keyMaterial),
                        new LegacyGameServerCipher(keyMaterial));
                }

                if (result.IsCompleted)
                {
                    trace?.Invoke(
                        "handshake-inbound-ended " +
                        $"decryptedBytes={decryptedHandshake.Count} expectedSeal={LegacyGameServerHandshakeReply.Seal}");
                    throw new EndOfStreamException("The legacy GameServer DH reply ended before TQClient was received.");
                }
            }
            finally
            {
                if (!advanced)
                {
                    reader.AdvanceTo(sequence.Start, sequence.End);
                }
            }
        }
    }

    private static int FindLegacyClientHandshakeLength(IReadOnlyList<byte> decryptedHandshake)
    {
        if (decryptedHandshake.Count < LegacyClientSeal.Length)
        {
            return -1;
        }

        for (var index = 0; index <= decryptedHandshake.Count - LegacyClientSeal.Length; index++)
        {
            var matches = true;
            for (var sealIndex = 0; sealIndex < LegacyClientSeal.Length; sealIndex++)
            {
                if (decryptedHandshake[index + sealIndex] != LegacyClientSeal[sealIndex])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return index + LegacyClientSeal.Length;
            }
        }

        return -1;
    }

    private static LegacyGameServerCipher CreateLegacyLoginCipher()
    {
        return new LegacyGameServerCipher(Encoding.ASCII.GetBytes(LegacyGameServerCipher.LoginKey));
    }

    private static string DescribeLegacyAccountFrame(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < PacketHeader.Size)
        {
            return $"frame=too-short length={bytes.Length}";
        }

        var frameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        var opcode = BinaryPrimitives.ReadUInt16LittleEndian(bytes[sizeof(ushort)..]);
        var description = $"frameLength={frameLength} opcode={opcode}";
        if (opcode == AccountPacketOpcodes.LegacyPasswordCryptographySeed &&
            bytes.Length >= LegacyAccountPasswordSeedPacket.FrameLength)
        {
            description += $" seed={BinaryPrimitives.ReadInt32LittleEndian(bytes[PacketHeader.Size..])}";
        }

        if (opcode == AccountPacketOpcodes.LegacyAuthentication)
        {
            description += $" payloadLength={Math.Max(0, bytes.Length - PacketHeader.Size)}";
        }

        return description;
    }

    private static string DescribeLegacyGameFrame(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < PacketHeader.Size)
        {
            return $"frame=too-short length={bytes.Length}";
        }

        var frameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        var opcode = BinaryPrimitives.ReadUInt16LittleEndian(bytes[sizeof(ushort)..]);
        return $"frameLength={frameLength} opcode={opcode} payloadLength={Math.Max(0, bytes.Length - PacketHeader.Size)}";
    }

    private static string HexForLog(ReadOnlySpan<byte> bytes)
    {
        const int MaxBytes = 1024;
        if (bytes.Length <= MaxBytes)
        {
            return Convert.ToHexString(bytes);
        }

        return Convert.ToHexString(bytes[..MaxBytes]) + $"...(total {bytes.Length} bytes)";
    }

    private static bool IsTruthyEnvironment(string? value)
    {
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private void LogClientException(
        EndPoint? remoteEndPoint,
        EndPoint? localEndPoint,
        string pipelineStage,
        Exception exception)
    {
        var role = NormalizeLogToken(_options.Endpoint.Role);
        var wireMode = NormalizeLogToken(_options.WireMode.ToString());
        var stage = NormalizeLogToken(pipelineStage);
        var exceptionType = NormalizeLogToken(exception.GetType().Name);
        var sourceSeam = $"ConquerTcpServer.HandleClientAsync.{stage}";
        _log.WriteLine(
            LegacyParityGapDiagnostics.Create(
                LegacyParityGapCategory.Runtime,
                "tcp-client-exception",
                "TCP client pipeline exception was caught without stopping the listener; inspect the error log, packet capture, and matching decompiled network dispatch path.",
                LegacyParityGapSeverity.Error,
                facts:
                [
                    LegacyParityGapDiagnostics.Fact("exceptionType", exceptionType),
                    LegacyParityGapDiagnostics.Fact("nextSeam", "inspect-tcp-client-pipeline"),
                    LegacyParityGapDiagnostics.Fact("role", role),
                    LegacyParityGapDiagnostics.Fact("sourceSeam", sourceSeam),
                    LegacyParityGapDiagnostics.Fact("stage", stage),
                    LegacyParityGapDiagnostics.Fact("wireMode", wireMode),
                ],
                dedupeFacts:
                [
                    LegacyParityGapDiagnostics.Fact("exceptionType", exceptionType),
                    LegacyParityGapDiagnostics.Fact("role", role),
                    LegacyParityGapDiagnostics.Fact("sourceSeam", sourceSeam),
                    LegacyParityGapDiagnostics.Fact("stage", stage),
                    LegacyParityGapDiagnostics.Fact("wireMode", wireMode),
                ]).FormatLogMessage());
        _log.WriteLine(
            "PARITY-BUG tcp client exception " +
            $"role={_options.Endpoint.Role} wireMode={_options.WireMode} " +
            $"stage={stage} sourceSeam={sourceSeam} remote={remoteEndPoint} local={localEndPoint} " +
            $"exception={exception.GetType().Name}: {exception.Message}");
        _errorLog.WriteLine(
            "PARITY-BUG tcp client exception " +
            $"{DateTimeOffset.UtcNow:O} role={_options.Endpoint.Role} wireMode={_options.WireMode} " +
            $"stage={stage} sourceSeam={sourceSeam} remote={remoteEndPoint} local={localEndPoint}" +
            $"{Environment.NewLine}{exception}");
    }

    private void LogAcceptLoopException(Exception exception)
    {
        var role = NormalizeLogToken(_options.Endpoint.Role);
        var wireMode = NormalizeLogToken(_options.WireMode.ToString());
        var exceptionType = NormalizeLogToken(exception.GetType().Name);
        const string sourceSeam = "ConquerTcpServer.AcceptLoopAsync";
        _log.WriteLine(
            LegacyParityGapDiagnostics.Create(
                LegacyParityGapCategory.Runtime,
                "tcp-accept-loop-exception",
                "TCP accept loop exception stopped listener acceptance; inspect the error log and restart the runtime if needed.",
                LegacyParityGapSeverity.Error,
                facts:
                [
                    LegacyParityGapDiagnostics.Fact("exceptionType", exceptionType),
                    LegacyParityGapDiagnostics.Fact("nextSeam", "inspect-tcp-accept-loop"),
                    LegacyParityGapDiagnostics.Fact("role", role),
                    LegacyParityGapDiagnostics.Fact("sourceSeam", sourceSeam),
                    LegacyParityGapDiagnostics.Fact("wireMode", wireMode),
                ],
                dedupeFacts:
                [
                    LegacyParityGapDiagnostics.Fact("exceptionType", exceptionType),
                    LegacyParityGapDiagnostics.Fact("role", role),
                    LegacyParityGapDiagnostics.Fact("sourceSeam", sourceSeam),
                    LegacyParityGapDiagnostics.Fact("wireMode", wireMode),
                ]).FormatLogMessage());
        _log.WriteLine(
            "PARITY-BUG tcp accept loop exception " +
            $"role={_options.Endpoint.Role} wireMode={_options.WireMode} sourceSeam={sourceSeam} " +
            $"exception={exception.GetType().Name}: {exception.Message}");
        _errorLog.WriteLine(
            "PARITY-BUG tcp accept loop exception " +
            $"{DateTimeOffset.UtcNow:O} role={_options.Endpoint.Role} wireMode={_options.WireMode} sourceSeam={sourceSeam}" +
            $"{Environment.NewLine}{exception}");
    }

    private static string NormalizeLogToken(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasDash = false;
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character) || character is '_' or '.' or ':')
            {
                builder.Append(character);
                previousWasDash = false;
            }
            else if (!previousWasDash)
            {
                builder.Append('-');
                previousWasDash = true;
            }
        }

        var token = builder.ToString().Trim('-');
        return token.Length == 0 ? "unknown" : token;
    }

    private static async ValueTask IgnoreShutdownExceptionAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException)
        {
        }
    }
}

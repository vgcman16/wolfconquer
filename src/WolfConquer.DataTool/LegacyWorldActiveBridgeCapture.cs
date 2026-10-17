using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

internal static partial class LegacyWorldActiveBridgeCaptureRunner
{
    private const string ClientToServerDirection = "clientToServer";
    private const string ServerToClientDirection = "serverToClient";
    private const int HandshakeDhBlobOffset = 53;
    private const int ClientHandshakeReplySegmentLength = 17;
    private const int PrivateKeyBytes = 32;

    public static async Task<LegacyWorldActiveBridgeCaptureResult> RunAsync(
        LegacyWorldActiveBridgeCaptureOptions options,
        CancellationToken cancellationToken = default,
        Action<string>? progress = null)
    {
        ValidateOptions(options);

        var startedAt = DateTimeOffset.UtcNow;
        var listenAddress = options.ListenAddress.ToString();
        var listenPort = options.ListenPort;
        var stopReason = "completed";
        string? acceptedClientEndpoint = null;
        string? targetEndpoint = null;
        var targetServerHandshakeLength = 0;
        var targetClientHandshakeReplyLength = 0;
        var clientServerHandshakeLength = 0;
        var clientHandshakeReplyLength = 0;

        TcpListener? listener = null;
        TcpClient? client = null;
        TcpClient? target = null;

        try
        {
            listener = new TcpListener(options.ListenAddress, options.ListenPort);
            listener.Start(1);
            if (listener.LocalEndpoint is IPEndPoint localEndpoint)
            {
                listenPort = localEndpoint.Port;
            }

            progress?.Invoke($"listening {listenAddress}:{listenPort} -> {options.TargetAddress}:{options.TargetPort}");

            stopReason = "accept-timeout";
            using (var acceptTimeout = CreateTimeout(options.AcceptTimeout, cancellationToken))
            {
                client = await listener.AcceptTcpClientAsync(acceptTimeout.Token).ConfigureAwait(false);
            }

            acceptedClientEndpoint = client.Client.RemoteEndPoint?.ToString();
            progress?.Invoke($"accepted client {acceptedClientEndpoint ?? "<unknown>"}");

            stopReason = "target-connect-timeout";
            target = new TcpClient();
            using (var connectTimeout = CreateTimeout(options.IdleTimeout, cancellationToken))
            {
                await target.ConnectAsync(options.TargetAddress, options.TargetPort, connectTimeout.Token)
                    .ConfigureAwait(false);
            }

            targetEndpoint = target.Client.RemoteEndPoint?.ToString();
            progress?.Invoke($"connected target {targetEndpoint ?? "<unknown>"}");

            var clientStream = client.GetStream();
            var targetStream = target.GetStream();

            progress?.Invoke("reading target server handshake");
            stopReason = "target-server-handshake-timeout";
            LoginHandshakeRead targetServerHandshake;
            using (var handshakeTimeout = CreateTimeout(options.IdleTimeout, cancellationToken))
            {
                targetServerHandshake = await ReadLoginKeyEncryptedHandshakeAsync(
                        targetStream,
                        TryGetCompleteServerHandshakeLength,
                        handshakeTimeout.Token)
                    .ConfigureAwait(false);
            }

            targetServerHandshakeLength = targetServerHandshake.PlainHandshake.Length;
            var targetDhValues = ReadServerHandshakeDhValues(targetServerHandshake.PlainHandshake);
            progress?.Invoke($"target server handshake {targetServerHandshakeLength} bytes");

            var targetDh = new LegacyGameServerDiffieHellman(
                targetDhValues.PrimeHex,
                targetDhValues.GeneratorHex);
            var targetClientPublicKey = targetDh.GenerateResponse(
                targetDhValues.ServerPublicHex,
                CreateLegacyPrivateExponent());
            var targetClientReply = CreateClientHandshakeReply(targetClientPublicKey);
            targetClientHandshakeReplyLength = targetClientReply.Length;

            stopReason = "target-client-handshake-timeout";
            using (var writeTimeout = CreateTimeout(options.IdleTimeout, cancellationToken))
            {
                await WriteLoginKeyEncryptedAsync(targetStream, targetClientReply, writeTimeout.Token)
                    .ConfigureAwait(false);
            }
            progress?.Invoke($"sent target client handshake {targetClientHandshakeReplyLength} bytes");

            var targetKeyMaterial = new byte[LegacyGameServerCipher.KeyMaterialSize];
            targetDh.DeriveKeyMaterial(targetKeyMaterial);

            var clientDh = new LegacyGameServerDiffieHellman(
                targetDhValues.PrimeHex,
                targetDhValues.GeneratorHex);
            var bridgeServerPublicKey = clientDh.GenerateRequest();
            var clientServerHandshake = LegacyGameServerHandshakePacket.Build(
                Encoding.ASCII.GetBytes(targetDhValues.PrimeHex),
                Encoding.ASCII.GetBytes(targetDhValues.GeneratorHex),
                Encoding.ASCII.GetBytes(bridgeServerPublicKey));
            clientServerHandshakeLength = clientServerHandshake.Length;

            stopReason = "client-server-handshake-timeout";
            using (var writeTimeout = CreateTimeout(options.IdleTimeout, cancellationToken))
            {
                await WriteLoginKeyEncryptedAsync(clientStream, clientServerHandshake, writeTimeout.Token)
                    .ConfigureAwait(false);
            }
            progress?.Invoke($"sent bridge server handshake {clientServerHandshakeLength} bytes");

            stopReason = "client-handshake-timeout";
            LoginHandshakeRead clientReply;
            using (var handshakeTimeout = CreateTimeout(options.IdleTimeout, cancellationToken))
            {
                clientReply = await ReadLoginKeyEncryptedHandshakeAsync(
                        clientStream,
                        TryGetCompleteClientHandshakeReplyLength,
                        handshakeTimeout.Token)
                    .ConfigureAwait(false);
            }

            clientHandshakeReplyLength = clientReply.PlainHandshake.Length;
            if (!LegacyGameServerHandshakeReply.TryReadPublicKey(clientReply.PlainHandshake, out var clientPublicKey))
            {
                throw new InvalidDataException("Legacy GameServer client handshake did not expose a public key.");
            }
            progress?.Invoke($"client handshake {clientHandshakeReplyLength} bytes");

            clientDh.HandleResponse(clientPublicKey);
            var clientKeyMaterial = new byte[LegacyGameServerCipher.KeyMaterialSize];
            clientDh.DeriveKeyMaterial(clientKeyMaterial);

            using var captureState = new BridgeCaptureState(options, cancellationToken);
            var serverToClientDecoder = new DirectionFrameDecoder(LegacyGameServerFrameSeal.Server);
            var clientToServerDecoder = new DirectionFrameDecoder(LegacyGameServerFrameSeal.Client);

            var targetInboundCipher = new LegacyGameServerCipher(targetKeyMaterial);
            var targetOutboundCipher = new LegacyGameServerCipher(targetKeyMaterial);
            var clientInboundCipher = new LegacyGameServerCipher(clientKeyMaterial);
            var clientOutboundCipher = new LegacyGameServerCipher(clientKeyMaterial);

            if (targetServerHandshake.EncryptedRemainder.Length > 0)
            {
                await ProcessRelayChunkSafelyAsync(
                        ServerToClientDirection,
                        targetServerHandshake.EncryptedRemainder,
                        targetInboundCipher,
                        clientOutboundCipher,
                        clientStream,
                        serverToClientDecoder,
                        captureState)
                    .ConfigureAwait(false);
            }

            if (clientReply.EncryptedRemainder.Length > 0)
            {
                await ProcessRelayChunkSafelyAsync(
                        ClientToServerDirection,
                        clientReply.EncryptedRemainder,
                        clientInboundCipher,
                        targetOutboundCipher,
                        targetStream,
                        clientToServerDecoder,
                        captureState)
                    .ConfigureAwait(false);
            }

            if (!captureState.IsStopRequested)
            {
                progress?.Invoke("relay started");
                stopReason = "relay";
                var clientToServer = RelayDirectionAsync(
                    ClientToServerDirection,
                    clientStream,
                    targetStream,
                    clientInboundCipher,
                    targetOutboundCipher,
                    clientToServerDecoder,
                    captureState);
                var serverToClient = RelayDirectionAsync(
                    ServerToClientDirection,
                    targetStream,
                    clientStream,
                    targetInboundCipher,
                    clientOutboundCipher,
                    serverToClientDecoder,
                    captureState);

                await Task.WhenAll(clientToServer, serverToClient).ConfigureAwait(false);
            }

            stopReason = captureState.StopReason ?? "completed";
            return captureState.CreateResult(
                startedAt,
                listenAddress,
                listenPort,
                options.TargetAddress,
                options.TargetPort,
                acceptedClientEndpoint,
                targetEndpoint,
                stopReason,
                targetServerHandshakeLength,
                targetClientHandshakeReplyLength,
                clientServerHandshakeLength,
                clientHandshakeReplyLength);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopReason = "cancelled";
        }
        catch (OperationCanceledException)
        {
        }
        catch (EndOfStreamException ex)
        {
            stopReason = $"closed-during-handshake:{ex.Message}";
        }
        catch (IOException ex)
        {
            stopReason = $"io-error:{ex.Message}";
        }
        catch (SocketException ex)
        {
            stopReason = $"socket-error:{ex.Message}";
        }
        finally
        {
            listener?.Stop();
            client?.Dispose();
            target?.Dispose();
        }

        return CreateEmptyResult(
            options,
            startedAt,
            listenAddress,
            listenPort,
            acceptedClientEndpoint,
            targetEndpoint,
            stopReason,
            targetServerHandshakeLength,
            targetClientHandshakeReplyLength,
            clientServerHandshakeLength,
            clientHandshakeReplyLength);
    }

    private static async Task<LoginHandshakeRead> ReadLoginKeyEncryptedHandshakeAsync(
        NetworkStream stream,
        TryGetHandshakeLength tryGetHandshakeLength,
        CancellationToken cancellationToken)
    {
        var cipher = new LegacyGameServerCipher(Encoding.ASCII.GetBytes(LegacyGameServerCipher.LoginKey));
        var plain = new List<byte>();
        var readBuffer = new byte[512];

        while (true)
        {
            var read = await stream.ReadAsync(readBuffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Connection closed before the legacy GameServer handshake was complete.");
            }

            var encryptedChunk = readBuffer.AsSpan(0, read).ToArray();
            var decryptedChunk = encryptedChunk.ToArray();
            cipher.DecryptInPlace(decryptedChunk);
            plain.AddRange(decryptedChunk);

            var plainBytes = plain.ToArray();
            if (!tryGetHandshakeLength(plainBytes, out var handshakeLength))
            {
                continue;
            }

            var extraLength = plainBytes.Length - handshakeLength;
            if (extraLength < 0 || extraLength > encryptedChunk.Length)
            {
                throw new InvalidDataException("Legacy GameServer handshake boundary fell outside the latest read.");
            }

            var encryptedRemainder = extraLength == 0
                ? Array.Empty<byte>()
                : encryptedChunk.AsSpan(encryptedChunk.Length - extraLength, extraLength).ToArray();
            return new LoginHandshakeRead(
                plainBytes.AsSpan(0, handshakeLength).ToArray(),
                encryptedRemainder);
        }
    }

    private static bool TryGetCompleteServerHandshakeLength(byte[] plain, out int length)
    {
        length = 0;
        if (plain.Length < LegacyGameServerHandshakePacket.LengthFieldOffset + sizeof(int))
        {
            return false;
        }

        var adjustedLength = ReadInt32(plain, LegacyGameServerHandshakePacket.LengthFieldOffset);
        length = checked(adjustedLength + LegacyGameServerHandshakePacket.LengthFieldAdjustment);
        if (length <= LegacyGameServerHandshakePacket.FixedLength || length > PacketHeader.DefaultMaxFrameSize)
        {
            throw new InvalidDataException($"Legacy GameServer handshake length {length} is outside the expected range.");
        }

        if (plain.Length < length)
        {
            return false;
        }

        if (!plain.AsSpan(length - LegacyGameServerHandshakePacket.Seal.Length, LegacyGameServerHandshakePacket.Seal.Length)
                .SequenceEqual(Encoding.ASCII.GetBytes(LegacyGameServerHandshakePacket.Seal)))
        {
            throw new InvalidDataException("Decrypted legacy GameServer handshake did not end with TQServer.");
        }

        return true;
    }

    private static bool TryGetCompleteClientHandshakeReplyLength(byte[] plain, out int length)
    {
        length = 0;
        var seal = Encoding.ASCII.GetBytes(LegacyGameServerHandshakeReply.Seal);
        var sealIndex = plain.AsSpan().IndexOf(seal);
        if (sealIndex < 0)
        {
            return false;
        }

        length = sealIndex + seal.Length;
        return true;
    }

    private static HandshakeDhValues ReadServerHandshakeDhValues(byte[] packet)
    {
        var offset = HandshakeDhBlobOffset;
        return new HandshakeDhValues(
            Encoding.ASCII.GetString(ReadLengthPrefixedBlob(packet, ref offset)),
            Encoding.ASCII.GetString(ReadLengthPrefixedBlob(packet, ref offset)),
            Encoding.ASCII.GetString(ReadLengthPrefixedBlob(packet, ref offset)));
    }

    private static byte[] ReadLengthPrefixedBlob(byte[] packet, ref int offset)
    {
        if (offset + sizeof(int) > packet.Length)
        {
            throw new InvalidDataException("Legacy GameServer handshake blob length is truncated.");
        }

        var length = ReadInt32(packet, offset);
        offset += sizeof(int);
        if (length <= 0 || offset + length > packet.Length)
        {
            throw new InvalidDataException("Legacy GameServer handshake blob is outside the packet bounds.");
        }

        var blob = packet.AsSpan(offset, length).ToArray();
        offset += length;
        return blob;
    }

    private static byte[] CreateClientHandshakeReply(string publicKey)
    {
        var publicKeyBytes = Encoding.ASCII.GetBytes(publicKey);
        var keyLengthOffset = ClientHandshakeReplySegmentLength +
            LegacyGameServerHandshakeReply.KeyLengthFieldAdjustment;
        var keyBytesOffset = keyLengthOffset + sizeof(int);
        var sealBytes = Encoding.ASCII.GetBytes(LegacyGameServerHandshakeReply.Seal);
        var reply = new byte[keyBytesOffset + publicKeyBytes.Length + sealBytes.Length];

        BinaryPrimitives.WriteInt32LittleEndian(
            reply.AsSpan(LegacyGameServerHandshakeReply.LengthFieldOffset, sizeof(int)),
            ClientHandshakeReplySegmentLength);
        BinaryPrimitives.WriteInt32LittleEndian(reply.AsSpan(keyLengthOffset, sizeof(int)), publicKeyBytes.Length);
        publicKeyBytes.CopyTo(reply.AsSpan(keyBytesOffset));
        sealBytes.CopyTo(reply.AsSpan(keyBytesOffset + publicKeyBytes.Length));
        return reply;
    }

    private static async Task WriteLoginKeyEncryptedAsync(
        NetworkStream stream,
        byte[] plain,
        CancellationToken cancellationToken)
    {
        var encrypted = plain.ToArray();
        new LegacyGameServerCipher(Encoding.ASCII.GetBytes(LegacyGameServerCipher.LoginKey))
            .EncryptInPlace(encrypted);
        await stream.WriteAsync(encrypted, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static BigInteger CreateLegacyPrivateExponent()
    {
        Span<byte> privateBytes = stackalloc byte[PrivateKeyBytes];
        BigInteger privateExponent;
        do
        {
            RandomNumberGenerator.Fill(privateBytes);
            privateBytes[0] &= 0x7F;
            privateExponent = new BigInteger(privateBytes, isUnsigned: true, isBigEndian: true);
        }
        while (privateExponent <= BigInteger.Zero);

        return privateExponent;
    }

    private static CancellationTokenSource CreateTimeout(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        return timeoutSource;
    }

    private static int ReadInt32(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)));
    }

    private static LegacyWorldActiveBridgeCaptureResult CreateEmptyResult(
        LegacyWorldActiveBridgeCaptureOptions options,
        DateTimeOffset startedAt,
        string listenAddress,
        int listenPort,
        string? acceptedClientEndpoint,
        string? targetEndpoint,
        string stopReason,
        int targetServerHandshakeLength,
        int targetClientHandshakeReplyLength,
        int clientServerHandshakeLength,
        int clientHandshakeReplyLength)
    {
        return new LegacyWorldActiveBridgeCaptureResult(
            startedAt,
            DateTimeOffset.UtcNow,
            listenAddress,
            listenPort,
            options.TargetAddress,
            options.TargetPort,
            acceptedClientEndpoint,
            targetEndpoint,
            options.MaxBytes,
            options.SampleBytes,
            options.MaxFrames,
            0,
            0,
            stopReason,
            targetServerHandshakeLength,
            targetClientHandshakeReplyLength,
            clientServerHandshakeLength,
            clientHandshakeReplyLength,
            Array.Empty<LegacyWorldActiveBridgeCaptureFrame>(),
            Array.Empty<LegacyWorldActiveBridgeCaptureChunk>(),
            Array.Empty<string>());
    }

    private static void ValidateOptions(LegacyWorldActiveBridgeCaptureOptions options)
    {
        if (options.ListenPort is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(options.ListenPort), "Listen port must be between 0 and 65535.");
        }

        if (options.TargetPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(options.TargetPort), "Target port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(options.TargetAddress))
        {
            throw new ArgumentException("Target address is required.", nameof(options.TargetAddress));
        }

        if (options.MaxBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxBytes), "Maximum bytes cannot be negative.");
        }

        if (options.SampleBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.SampleBytes), "Sample bytes cannot be negative.");
        }

        if (options.MaxFrames <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxFrames), "Maximum frames must be positive.");
        }

        if (options.AcceptTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options.AcceptTimeout), "Accept timeout must be positive.");
        }

        if (options.IdleTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options.IdleTimeout), "Idle timeout must be positive.");
        }
    }

    private delegate bool TryGetHandshakeLength(byte[] plain, out int length);

    private sealed record LoginHandshakeRead(byte[] PlainHandshake, byte[] EncryptedRemainder);

    private sealed record HandshakeDhValues(string PrimeHex, string GeneratorHex, string ServerPublicHex);

    private sealed class BridgeCaptureState : IDisposable
    {
        private readonly object _gate = new();
        private readonly CancellationTokenSource _stopSource;
        private readonly CancellationTokenSource _idleSource;
        private readonly CancellationTokenSource _combinedSource;
        private readonly List<LegacyWorldActiveBridgeCaptureFrame> _frames = new();
        private readonly List<LegacyWorldActiveBridgeCaptureChunk> _chunks = new();
        private readonly List<string> _warnings = new();
        private long _totalClientToServerBytes;
        private long _totalServerToClientBytes;
        private bool _frameLimitWarningRecorded;
        private string? _stopReason;

        public BridgeCaptureState(LegacyWorldActiveBridgeCaptureOptions options, CancellationToken cancellationToken)
        {
            Options = options;
            _stopSource = new CancellationTokenSource();
            _idleSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _combinedSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _stopSource.Token,
                _idleSource.Token);
            ResetIdle();
        }

        public LegacyWorldActiveBridgeCaptureOptions Options { get; }

        public CancellationToken Token => _combinedSource.Token;

        public bool IdleTimedOut => _idleSource.IsCancellationRequested && !_stopSource.IsCancellationRequested;

        public bool IsStopRequested => _stopSource.IsCancellationRequested;

        public string? StopReason
        {
            get
            {
                lock (_gate)
                {
                    return _stopReason;
                }
            }
        }

        public int FrameCount
        {
            get
            {
                lock (_gate)
                {
                    return _frames.Count;
                }
            }
        }

        public void ResetIdle()
        {
            lock (_gate)
            {
                if (!_idleSource.IsCancellationRequested)
                {
                    _idleSource.CancelAfter(Options.IdleTimeout);
                }
            }
        }

        public int GetReadLimit(int bufferLength)
        {
            if (Options.MaxBytes == 0)
            {
                return bufferLength;
            }

            lock (_gate)
            {
                var remaining = Options.MaxBytes - (_totalClientToServerBytes + _totalServerToClientBytes);
                if (remaining <= 0)
                {
                    return 0;
                }

                return (int)Math.Min(bufferLength, remaining);
            }
        }

        public bool RecordBytes(string direction, int length)
        {
            if (length <= 0)
            {
                return false;
            }

            lock (_gate)
            {
                if (string.Equals(direction, ClientToServerDirection, StringComparison.Ordinal))
                {
                    _totalClientToServerBytes += length;
                }
                else
                {
                    _totalServerToClientBytes += length;
                }

                return Options.MaxBytes > 0 &&
                    _totalClientToServerBytes + _totalServerToClientBytes >= Options.MaxBytes;
            }
        }

        public void RecordChunk(string direction, ReadOnlySpan<byte> decrypted)
        {
            var sampleLength = Math.Min(Options.SampleBytes, decrypted.Length);
            var sample = decrypted[..sampleLength].ToArray();
            lock (_gate)
            {
                _chunks.Add(new LegacyWorldActiveBridgeCaptureChunk(
                    DateTimeOffset.UtcNow,
                    direction,
                    decrypted.Length,
                    sampleLength,
                    Convert.ToHexString(sample).ToLowerInvariant(),
                    Convert.ToBase64String(sample)));
            }
        }

        public void RecordFrame(string direction, PacketFrame frame)
        {
            var frameBytes = frame.Frame.ToArray();
            var sampleLength = Math.Min(Options.SampleBytes, frameBytes.Length);
            var sample = frameBytes.AsSpan(0, sampleLength).ToArray();

            lock (_gate)
            {
                if (_frames.Count >= Options.MaxFrames)
                {
                    return;
                }

                _frames.Add(new LegacyWorldActiveBridgeCaptureFrame(
                    DateTimeOffset.UtcNow,
                    _frames.Count,
                    direction,
                    frame.Header.Opcode,
                    (int)frame.Frame.Length,
                    (int)frame.Payload.Length,
                    sampleLength,
                    Convert.ToHexString(sample).ToLowerInvariant(),
                    Convert.ToBase64String(sample)));
            }
        }

        public void RecordWarning(string warning)
        {
            lock (_gate)
            {
                _warnings.Add(warning);
            }
        }

        public void RecordFrameLimitReached(string direction)
        {
            lock (_gate)
            {
                if (_frameLimitWarningRecorded)
                {
                    return;
                }

                _frameLimitWarningRecorded = true;
                _warnings.Add(
                    $"max-frames reached at {Options.MaxFrames} stored frame(s); continuing active bridge relay");
            }
        }

        public void RequestStop(string reason)
        {
            lock (_gate)
            {
                _stopReason ??= reason;
            }

            if (!_stopSource.IsCancellationRequested)
            {
                _stopSource.Cancel();
            }
        }

        public LegacyWorldActiveBridgeCaptureResult CreateResult(
            DateTimeOffset startedAt,
            string listenAddress,
            int listenPort,
            string targetAddress,
            int targetPort,
            string? acceptedClientEndpoint,
            string? targetEndpoint,
            string stopReason,
            int targetServerHandshakeLength,
            int targetClientHandshakeReplyLength,
            int clientServerHandshakeLength,
            int clientHandshakeReplyLength)
        {
            lock (_gate)
            {
                return new LegacyWorldActiveBridgeCaptureResult(
                    startedAt,
                    DateTimeOffset.UtcNow,
                    listenAddress,
                    listenPort,
                    targetAddress,
                    targetPort,
                    acceptedClientEndpoint,
                    targetEndpoint,
                    Options.MaxBytes,
                    Options.SampleBytes,
                    Options.MaxFrames,
                    _totalClientToServerBytes,
                    _totalServerToClientBytes,
                    _stopReason ?? stopReason,
                    targetServerHandshakeLength,
                    targetClientHandshakeReplyLength,
                    clientServerHandshakeLength,
                    clientHandshakeReplyLength,
                    _frames.ToArray(),
                    _chunks.ToArray(),
                    _warnings.ToArray());
            }
        }

        public void Dispose()
        {
            _combinedSource.Dispose();
            _idleSource.Dispose();
            _stopSource.Dispose();
        }
    }
}

internal sealed record LegacyWorldActiveBridgeCaptureOptions(
    IPAddress ListenAddress,
    int ListenPort,
    string TargetAddress,
    int TargetPort,
    long MaxBytes,
    int MaxFrames,
    int SampleBytes,
    TimeSpan AcceptTimeout,
    TimeSpan IdleTimeout,
    bool ContinueAfterMaxFrames);

internal sealed record LegacyWorldActiveBridgeCaptureResult(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string ListenAddress,
    int ListenPort,
    string TargetAddress,
    int TargetPort,
    string? AcceptedClientEndpoint,
    string? TargetEndpoint,
    long MaxBytes,
    int SampleBytes,
    int MaxFrames,
    long TotalClientToServerBytes,
    long TotalServerToClientBytes,
    string StopReason,
    int TargetServerHandshakeLength,
    int TargetClientHandshakeReplyLength,
    int ClientServerHandshakeLength,
    int ClientHandshakeReplyLength,
    IReadOnlyList<LegacyWorldActiveBridgeCaptureFrame> Frames,
    IReadOnlyList<LegacyWorldActiveBridgeCaptureChunk> Chunks,
    IReadOnlyList<string> CaptureWarnings)
{
    public bool HandshakeCompleted =>
        TargetServerHandshakeLength > 0 &&
        TargetClientHandshakeReplyLength > 0 &&
        ClientServerHandshakeLength > 0 &&
        ClientHandshakeReplyLength > 0;

    public int ClientHandshakeLength => ClientHandshakeReplyLength;

    public string HandshakeStatus => HandshakeCompleted ? "completed" : StopReason;
}

internal sealed record LegacyWorldActiveBridgeCaptureFrame(
    DateTimeOffset TimestampUtc,
    int Index,
    string Direction,
    ushort Opcode,
    int Length,
    int PayloadLength,
    int SampleLength,
    string FirstHex,
    string FirstBase64);

internal sealed record LegacyWorldActiveBridgeCaptureChunk(
    DateTimeOffset TimestampUtc,
    string Direction,
    int Length,
    int SampleLength,
    string FirstHex,
    string FirstBase64);

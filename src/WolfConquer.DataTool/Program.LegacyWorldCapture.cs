using System.Buffers;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

internal static partial class Program
{
    static int LegacyWorldSmoke(string[] args)
    {
        LegacyWorldSmokeOptions options;
        try
        {
            options = ResolveLegacyWorldSmokeOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var result = RunLegacyWorldSmokeAsync(options).GetAwaiter().GetResult();
            Console.WriteLine("Legacy world smoke:");
            Console.WriteLine($"endpoint={options.Address}:{options.Port}");
            Console.WriteLine($"accountKey={options.AccountKey}");
            Console.WriteLine($"answerOk={result.AnswerOkSeen} status={result.Status}");
            Console.WriteLine($"observed-opcodes={FormatLegacyWorldSmokeOpcodes(result.ObservedOpcodes)}");
            return result.AnswerOkSeen ? 0 : 2;
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or InvalidDataException)
        {
            Console.Error.WriteLine($"Legacy world smoke failed: {ex.Message}");
            return 2;
        }
    }

    static LegacyWorldSmokeOptions ResolveLegacyWorldSmokeOptions(string[] args)
    {
        if (args.Length < 4)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool legacy-world-smoke <address> <port> <accountKey> [--timeout-ms ms]");
        }

        if (!int.TryParse(args[2], out var port) || port is < 1 or > 65535)
        {
            throw new FormatException($"Invalid port: {args[2]}");
        }

        if (!uint.TryParse(args[3], out var accountKey))
        {
            throw new FormatException($"Invalid account key: {args[3]}");
        }

        var timeoutMs = 5000;
        for (var index = 4; index < args.Length; index++)
        {
            var option = args[index];
            if (string.Equals(option, "--timeout-ms", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out timeoutMs) || timeoutMs <= 0)
                {
                    throw new FormatException("--timeout-ms requires a positive integer.");
                }
            }
            else
            {
                throw new ArgumentException($"Unknown legacy-world-smoke option: {option}");
            }
        }

        return new LegacyWorldSmokeOptions(args[1], port, accountKey, TimeSpan.FromMilliseconds(timeoutMs));
    }

    static async Task<LegacyWorldSmokeResult> RunLegacyWorldSmokeAsync(LegacyWorldSmokeOptions options)
    {
        var capture = await RunLegacyWorldCaptureCoreAsync(
                options.Address,
                options.Port,
                options.AccountKey,
                options.Timeout,
                maxFrames: int.MaxValue,
                sampleBytes: 0,
                stopAfterAnswerOk: true)
            .ConfigureAwait(false);
        return new LegacyWorldSmokeResult(
            capture.AnswerOkSeen,
            capture.Frames.Select(frame => frame.Opcode).ToArray(),
            capture.Status);
    }

    static int LegacyWorldCapture(string[] args)
    {
        LegacyWorldCaptureOptions options;
        try
        {
            options = ResolveLegacyWorldCaptureOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var result = RunLegacyWorldCaptureAsync(options).GetAwaiter().GetResult();
            var outputDirectory = Path.GetDirectoryName(options.OutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            WriteJson(options.OutputPath, result);

            Console.WriteLine("Legacy world capture:");
            Console.WriteLine($"endpoint={options.Address}:{options.Port}");
            Console.WriteLine($"accountKey={options.AccountKey}");
            Console.WriteLine($"output-json={options.OutputPath}");
            Console.WriteLine($"answerOk={result.AnswerOkSeen} status={result.Status}");
            Console.WriteLine($"observed-opcodes={FormatLegacyWorldSmokeOpcodes(result.Frames.Select(frame => frame.Opcode).ToArray())}");
            Console.WriteLine($"frames={result.Frames.Count}");
            return result.AnswerOkSeen ? 0 : 2;
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or InvalidDataException)
        {
            Console.Error.WriteLine($"Legacy world capture failed: {ex.Message}");
            return 2;
        }
    }

    static LegacyWorldCaptureOptions ResolveLegacyWorldCaptureOptions(string[] args)
    {
        if (args.Length < 5)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool legacy-world-capture <address> <port> <accountKey> <outputPath> [--timeout-ms ms] [--max-frames N] [--sample-bytes N]");
        }

        if (!int.TryParse(args[2], out var port) || port is < 1 or > 65535)
        {
            throw new FormatException($"Invalid port: {args[2]}");
        }

        if (!uint.TryParse(args[3], out var accountKey))
        {
            throw new FormatException($"Invalid account key: {args[3]}");
        }

        var timeoutMs = 5000;
        var maxFrames = 64;
        var sampleBytes = 64;
        for (var index = 5; index < args.Length; index++)
        {
            var option = args[index];
            if (string.Equals(option, "--timeout-ms", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out timeoutMs) || timeoutMs <= 0)
                {
                    throw new FormatException("--timeout-ms requires a positive integer.");
                }
            }
            else if (string.Equals(option, "--max-frames", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out maxFrames) || maxFrames <= 0)
                {
                    throw new FormatException("--max-frames requires a positive integer.");
                }
            }
            else if (string.Equals(option, "--sample-bytes", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out sampleBytes) || sampleBytes < 0)
                {
                    throw new FormatException("--sample-bytes requires a non-negative integer.");
                }
            }
            else
            {
                throw new ArgumentException($"Unknown legacy-world-capture option: {option}");
            }
        }

        return new LegacyWorldCaptureOptions(
            args[1],
            port,
            accountKey,
            Path.GetFullPath(args[4]),
            TimeSpan.FromMilliseconds(timeoutMs),
            maxFrames,
            sampleBytes);
    }

    static Task<LegacyWorldCaptureFile> RunLegacyWorldCaptureAsync(LegacyWorldCaptureOptions options)
    {
        return RunLegacyWorldCaptureCoreAsync(
            options.Address,
            options.Port,
            options.AccountKey,
            options.Timeout,
            options.MaxFrames,
            options.SampleBytes,
            stopAfterAnswerOk: false);
    }

    static async Task<LegacyWorldCaptureFile> RunLegacyWorldCaptureCoreAsync(
        string address,
        int port,
        uint accountKey,
        TimeSpan timeoutValue,
        int maxFrames,
        int sampleBytes,
        bool stopAfterAnswerOk)
    {
        var startedAt = DateTimeOffset.UtcNow;
        using var timeout = new CancellationTokenSource(timeoutValue);
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(address, port, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return CreateLegacyWorldCaptureFile(
                startedAt,
                address,
                port,
                accountKey,
                maxFrames,
                sampleBytes,
                false,
                "timeout",
                []);
        }

        var stream = client.GetStream();
        LegacyWorldHandshakeDhValues dhValues;
        try
        {
            var handshake = await ReadEncryptedLegacyWorldHandshakeAsync(stream, timeout.Token).ConfigureAwait(false);
            dhValues = ReadLegacyWorldHandshakeDhValues(handshake);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return CreateLegacyWorldCaptureFile(
                startedAt,
                address,
                port,
                accountKey,
                maxFrames,
                sampleBytes,
                false,
                "timeout",
                []);
        }

        var clientDh = new LegacyGameServerDiffieHellman(dhValues.PrimeHex, dhValues.GeneratorHex);
        var clientPublicKey = clientDh.GenerateResponse(dhValues.ServerPublicHex, new BigInteger(15));
        await stream.WriteAsync(
                EncryptLegacyWorldWithLoginKey(CreateLegacyWorldClientHandshakeReply(clientPublicKey)),
                timeout.Token)
            .ConfigureAwait(false);
        await stream.FlushAsync(timeout.Token).ConfigureAwait(false);

        var keyMaterial = new byte[LegacyGameServerCipher.KeyMaterialSize];
        clientDh.DeriveKeyMaterial(keyMaterial);

        var inboundCipher = new LegacyGameServerCipher(keyMaterial);
        var outboundCipher = new LegacyGameServerCipher(keyMaterial);
        var loginFrame = LegacyGameServerFrameCodec.EncodeEncrypted(
            new LegacyMsgLoginGamePacket(accountKey).ToFrameBytes(),
            outboundCipher,
            LegacyGameServerFrameSeal.Client);
        await stream.WriteAsync(loginFrame, timeout.Token).ConfigureAwait(false);
        await stream.FlushAsync(timeout.Token).ConfigureAwait(false);

        var frames = new List<LegacyWorldCaptureFrame>();
        var decryptedBuffer = new List<byte>();
        var readBuffer = new byte[1024];
        var answerOkSeen = false;

        while (true)
        {
            while (TryTakeLegacyWorldSmokeFrame(decryptedBuffer, out var frame))
            {
                var isAnswerOk = LegacyMsgMessagePacket.TryParse(frame, out var message) &&
                    message.Content.Count > 3 &&
                    string.Equals(message.Content[3], "ANSWER_OK", StringComparison.Ordinal);
                answerOkSeen |= isAnswerOk;
                frames.Add(CreateLegacyWorldCaptureFrame(frames.Count, frame, sampleBytes, isAnswerOk));

                if (stopAfterAnswerOk && isAnswerOk)
                {
                    return CreateLegacyWorldCaptureFile(
                        startedAt,
                        address,
                        port,
                        accountKey,
                        maxFrames,
                        sampleBytes,
                        true,
                        "answer-ok",
                        frames);
                }

                if (frames.Count >= maxFrames)
                {
                    return CreateLegacyWorldCaptureFile(
                        startedAt,
                        address,
                        port,
                        accountKey,
                        maxFrames,
                        sampleBytes,
                        answerOkSeen,
                        "max-frames",
                        frames);
                }
            }

            int read;
            try
            {
                read = await stream.ReadAsync(readBuffer, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                return CreateLegacyWorldCaptureFile(
                    startedAt,
                    address,
                    port,
                    accountKey,
                    maxFrames,
                    sampleBytes,
                    answerOkSeen,
                    "timeout",
                    frames);
            }

            if (read == 0)
            {
                return CreateLegacyWorldCaptureFile(
                    startedAt,
                    address,
                    port,
                    accountKey,
                    maxFrames,
                    sampleBytes,
                    answerOkSeen,
                    "closed",
                    frames);
            }

            var decrypted = readBuffer.AsSpan(0, read).ToArray();
            inboundCipher.DecryptInPlace(decrypted);
            decryptedBuffer.AddRange(decrypted);
        }
    }

    static LegacyWorldCaptureFile CreateLegacyWorldCaptureFile(
        DateTimeOffset startedAt,
        string address,
        int port,
        uint accountKey,
        int maxFrames,
        int sampleBytes,
        bool answerOkSeen,
        string status,
        IReadOnlyList<LegacyWorldCaptureFrame> frames)
    {
        return new LegacyWorldCaptureFile(
            startedAt,
            DateTimeOffset.UtcNow,
            address,
            port,
            accountKey,
            maxFrames,
            sampleBytes,
            answerOkSeen,
            status,
            frames.ToArray());
    }

    static LegacyWorldCaptureFrame CreateLegacyWorldCaptureFrame(
        int index,
        PacketFrame frame,
        int sampleBytes,
        bool isAnswerOk)
    {
        var frameBytes = frame.Frame.ToArray();
        var sampleLength = Math.Min(sampleBytes, frameBytes.Length);
        var sample = frameBytes.AsSpan(0, sampleLength).ToArray();
        return new LegacyWorldCaptureFrame(
            DateTimeOffset.UtcNow,
            index,
            frame.Header.Opcode,
            (int)frame.Frame.Length,
            (int)frame.Payload.Length,
            sampleLength,
            Convert.ToHexString(sample).ToLowerInvariant(),
            Convert.ToBase64String(sample),
            isAnswerOk);
    }

    static async Task<byte[]> ReadEncryptedLegacyWorldHandshakeAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var cipher = new LegacyGameServerCipher(Encoding.ASCII.GetBytes(LegacyGameServerCipher.LoginKey));
        using var plain = new MemoryStream();
        var buffer = new byte[128];

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Connection closed before the legacy GameServer handshake was complete.");
            }

            var decrypted = buffer.AsSpan(0, read).ToArray();
            cipher.DecryptInPlace(decrypted);
            plain.Write(decrypted);

            var plainBytes = plain.ToArray();
            if (TryGetCompleteLegacyWorldHandshakeLength(plainBytes, out var length))
            {
                return plainBytes.AsSpan(0, length).ToArray();
            }
        }
    }

    static bool TryGetCompleteLegacyWorldHandshakeLength(byte[] plain, out int length)
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

    static LegacyWorldHandshakeDhValues ReadLegacyWorldHandshakeDhValues(byte[] packet)
    {
        const int handshakeDhBlobOffset = 53;
        var offset = handshakeDhBlobOffset;
        return new LegacyWorldHandshakeDhValues(
            Encoding.ASCII.GetString(ReadLegacyWorldLengthPrefixedBlob(packet, ref offset)),
            Encoding.ASCII.GetString(ReadLegacyWorldLengthPrefixedBlob(packet, ref offset)),
            Encoding.ASCII.GetString(ReadLegacyWorldLengthPrefixedBlob(packet, ref offset)));
    }

    static byte[] ReadLegacyWorldLengthPrefixedBlob(byte[] packet, ref int offset)
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

    static byte[] CreateLegacyWorldClientHandshakeReply(string publicKey)
    {
        const int clientHandshakeReplySegmentLength = 17;
        var publicKeyBytes = Encoding.ASCII.GetBytes(publicKey);
        var keyLengthOffset = clientHandshakeReplySegmentLength +
            LegacyGameServerHandshakeReply.KeyLengthFieldAdjustment;
        var keyBytesOffset = keyLengthOffset + sizeof(int);
        var sealBytes = Encoding.ASCII.GetBytes(LegacyGameServerHandshakeReply.Seal);
        var reply = new byte[keyBytesOffset + publicKeyBytes.Length + sealBytes.Length];

        BinaryPrimitives.WriteInt32LittleEndian(
            reply.AsSpan(LegacyGameServerHandshakeReply.LengthFieldOffset, sizeof(int)),
            clientHandshakeReplySegmentLength);
        BinaryPrimitives.WriteInt32LittleEndian(reply.AsSpan(keyLengthOffset, sizeof(int)), publicKeyBytes.Length);
        publicKeyBytes.CopyTo(reply.AsSpan(keyBytesOffset));
        sealBytes.CopyTo(reply.AsSpan(keyBytesOffset + publicKeyBytes.Length));
        return reply;
    }

    static byte[] EncryptLegacyWorldWithLoginKey(byte[] plain)
    {
        var encrypted = plain.ToArray();
        new LegacyGameServerCipher(Encoding.ASCII.GetBytes(LegacyGameServerCipher.LoginKey))
            .EncryptInPlace(encrypted);
        return encrypted;
    }

    static bool TryTakeLegacyWorldSmokeFrame(List<byte> decryptedBuffer, out PacketFrame frame)
    {
        frame = default;
        var buffer = decryptedBuffer.ToArray();
        if (!LegacyGameServerFrameCodec.TryStripSeal(buffer, out var modernFrame, out var consumed))
        {
            return false;
        }

        var sequence = new ReadOnlySequence<byte>(modernFrame);
        if (!PacketFrameDecoder.TryRead(sequence, out frame, out var frameConsumed, out _) ||
            sequence.GetOffset(frameConsumed) != modernFrame.Length)
        {
            throw new InvalidDataException(
                "Decoded legacy GameServer sealed frame did not contain one complete modern packet frame.");
        }

        decryptedBuffer.RemoveRange(0, consumed);
        return true;
    }

    static string FormatLegacyWorldSmokeOpcodes(IReadOnlyList<ushort> opcodes)
    {
        if (opcodes.Count == 0)
        {
            return "(none)";
        }

        return string.Join(", ", opcodes.Take(8));
    }

    static int ReadInt32(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)));
    }
}

internal sealed record LegacyWorldSmokeOptions(
    string Address,
    int Port,
    uint AccountKey,
    TimeSpan Timeout);

internal sealed record LegacyWorldSmokeResult(
    bool AnswerOkSeen,
    IReadOnlyList<ushort> ObservedOpcodes,
    string Status);

internal sealed record LegacyWorldCaptureOptions(
    string Address,
    int Port,
    uint AccountKey,
    string OutputPath,
    TimeSpan Timeout,
    int MaxFrames,
    int SampleBytes);

internal sealed record LegacyWorldCaptureFile(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string Address,
    int Port,
    uint AccountKey,
    int MaxFrames,
    int SampleBytes,
    bool AnswerOkSeen,
    string Status,
    IReadOnlyList<LegacyWorldCaptureFrame> Frames);

internal sealed record LegacyWorldCaptureFrame(
    DateTimeOffset TimestampUtc,
    int Index,
    ushort Opcode,
    int Length,
    int PayloadLength,
    int SampleLength,
    string FirstHex,
    string FirstBase64,
    bool IsAnswerOk);

internal sealed record LegacyWorldHandshakeDhValues(
    string PrimeHex,
    string GeneratorHex,
    string ServerPublicHex);

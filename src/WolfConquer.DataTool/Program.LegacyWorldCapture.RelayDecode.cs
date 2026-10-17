using System.Buffers;
using System.Numerics;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

internal static partial class Program
{
    static int LegacyWorldRelayDecode(string[] args)
    {
        LegacyWorldRelayDecodeOptions options;
        try
        {
            options = ResolveLegacyWorldRelayDecodeOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var result = DecodeLegacyWorldRelayCapture(options);
            var outputDirectory = Path.GetDirectoryName(options.OutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            WriteJson(options.OutputPath, result);

            Console.WriteLine("Legacy world relay decode:");
            Console.WriteLine($"source-json={result.SourcePath}");
            Console.WriteLine($"source-sha256={result.SourceSha256}");
            Console.WriteLine($"output-json={options.OutputPath}");
            Console.WriteLine(
                $"serverHandshake={result.ServerHandshakeSeen} length={result.ServerHandshakeLength} " +
                $"clientHandshake={result.ClientHandshakeSeen} length={result.ClientHandshakeLength} " +
                $"fixedDh15={result.FixedPrivateExponent15Matches} status={result.Status}");
            Console.WriteLine($"frames={result.Frames.Count} opcodes={FormatLegacyWorldRelayDecodeOpcodes(result.Frames)}");
            return result.FixedPrivateExponent15Matches && result.Frames.Count > 0 ? 0 : 2;
        }
        catch (Exception ex) when (
            ex is ArgumentException or FormatException or IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Legacy world relay decode failed: {ex.Message}");
            return 1;
        }
    }

    static LegacyWorldRelayDecodeOptions ResolveLegacyWorldRelayDecodeOptions(string[] args)
    {
        if (args.Length < 3)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool legacy-world-relay-decode <relayCaptureJsonPath> <outputJsonPath> [--max-frames N] [--sample-bytes N]");
        }

        var maxFrames = 64;
        var sampleBytes = 64;
        for (var index = 3; index < args.Length; index++)
        {
            var option = args[index];
            if (string.Equals(option, "--max-frames", StringComparison.OrdinalIgnoreCase))
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
                throw new ArgumentException($"Unknown legacy-world-relay-decode option: {option}");
            }
        }

        return new LegacyWorldRelayDecodeOptions(
            Path.GetFullPath(args[1]),
            Path.GetFullPath(args[2]),
            maxFrames,
            sampleBytes);
    }

    static LegacyWorldRelayDecodeFile DecodeLegacyWorldRelayCapture(LegacyWorldRelayDecodeOptions options)
    {
        var summary = TcpRelayCaptureAnalyzer.Analyze(options.CapturePath, firstChunkCount: 0);
        var capture = TcpRelayCaptureAnalyzer.ReadCapture(options.CapturePath);
        var chunks = capture.Chunks ?? Array.Empty<TcpRelayCaptureAnalyzerChunk>();

        var serverLoginCipher = new LegacyGameServerCipher(Encoding.ASCII.GetBytes(LegacyGameServerCipher.LoginKey));
        var clientLoginCipher = new LegacyGameServerCipher(Encoding.ASCII.GetBytes(LegacyGameServerCipher.LoginKey));
        using var serverHandshakePlain = new MemoryStream();
        using var clientHandshakePlain = new MemoryStream();
        var encryptedChunks = new List<LegacyWorldRelayEncryptedChunk>();

        LegacyWorldHandshakeDhValues? dhValues = null;
        var clientPublicKey = string.Empty;
        var serverHandshakeSeen = false;
        var clientHandshakeSeen = false;
        var serverHandshakeLength = 0;
        var clientHandshakeLength = 0;

        foreach (var chunk in chunks)
        {
            var sample = DecodeTcpRelayCaptureSample(chunk);
            if (sample.Length == 0)
            {
                continue;
            }

            var truncatedAfterSample = chunk.SampleLength < chunk.Length;
            if (string.Equals(chunk.Direction, "serverToClient", StringComparison.Ordinal))
            {
                if (!serverHandshakeSeen)
                {
                    var beforeLength = checked((int)serverHandshakePlain.Length);
                    var decrypted = sample.ToArray();
                    serverLoginCipher.DecryptInPlace(decrypted);
                    serverHandshakePlain.Write(decrypted);

                    var plain = serverHandshakePlain.ToArray();
                    if (TryGetCompleteLegacyWorldHandshakeLength(plain, out serverHandshakeLength))
                    {
                        serverHandshakeSeen = true;
                        dhValues = ReadLegacyWorldHandshakeDhValues(plain.AsSpan(0, serverHandshakeLength).ToArray());
                        var consumedFromSample = Math.Clamp(serverHandshakeLength - beforeLength, 0, sample.Length);
                        AddLegacyWorldRelayEncryptedChunk(
                            encryptedChunks,
                            chunk.Direction,
                            sample,
                            consumedFromSample,
                            truncatedAfterSample);
                    }
                }
                else
                {
                    AddLegacyWorldRelayEncryptedChunk(encryptedChunks, chunk.Direction, sample, 0, truncatedAfterSample);
                }
            }
            else if (string.Equals(chunk.Direction, "clientToServer", StringComparison.Ordinal))
            {
                if (!clientHandshakeSeen)
                {
                    var beforeLength = checked((int)clientHandshakePlain.Length);
                    var decrypted = sample.ToArray();
                    clientLoginCipher.DecryptInPlace(decrypted);
                    clientHandshakePlain.Write(decrypted);

                    var plain = clientHandshakePlain.ToArray();
                    if (TryGetCompleteLegacyWorldClientHandshakeLength(plain, out clientHandshakeLength))
                    {
                        clientHandshakeSeen = true;
                        if (!LegacyGameServerHandshakeReply.TryReadPublicKey(
                                plain.AsSpan(0, clientHandshakeLength),
                                out clientPublicKey))
                        {
                            throw new InvalidDataException("Captured legacy GameServer client handshake did not expose a public key.");
                        }

                        var consumedFromSample = Math.Clamp(clientHandshakeLength - beforeLength, 0, sample.Length);
                        AddLegacyWorldRelayEncryptedChunk(
                            encryptedChunks,
                            chunk.Direction,
                            sample,
                            consumedFromSample,
                            truncatedAfterSample);
                    }
                }
                else
                {
                    AddLegacyWorldRelayEncryptedChunk(encryptedChunks, chunk.Direction, sample, 0, truncatedAfterSample);
                }
            }
        }

        var frames = new List<LegacyWorldRelayDecodeFrame>();
        var fixedPrivateExponent15Matches = false;
        var status = "handshake-missing";
        if (!serverHandshakeSeen)
        {
            status = "server-handshake-missing";
        }
        else if (!clientHandshakeSeen)
        {
            status = "client-handshake-missing";
        }
        else if (dhValues is null || clientPublicKey.Length == 0)
        {
            status = "handshake-key-missing";
        }
        else
        {
            var clientDh = new LegacyGameServerDiffieHellman(
                dhValues.PrimeHex,
                dhValues.GeneratorHex);
            var expectedClientPublicKey = clientDh.GenerateResponse(
                dhValues.ServerPublicHex,
                new BigInteger(15));
            fixedPrivateExponent15Matches = LegacyWorldRelayDhPublicKeysMatch(
                expectedClientPublicKey,
                clientPublicKey);

            if (!fixedPrivateExponent15Matches)
            {
                status = "client-dh-not-fixed-exponent-15";
            }
            else
            {
                var keyMaterial = new byte[LegacyGameServerCipher.KeyMaterialSize];
                clientDh.DeriveKeyMaterial(keyMaterial);
                status = DecodeLegacyWorldRelayFrames(
                    encryptedChunks,
                    new LegacyGameServerCipher(keyMaterial),
                    new LegacyGameServerCipher(keyMaterial),
                    options.MaxFrames,
                    options.SampleBytes,
                    frames);
            }
        }

        return new LegacyWorldRelayDecodeFile(
            DateTimeOffset.UtcNow,
            summary.Path,
            summary.Sha256,
            summary.FileLength,
            summary.ListenAddress,
            summary.ListenPort,
            summary.TargetAddress,
            summary.TargetPort,
            options.MaxFrames,
            options.SampleBytes,
            serverHandshakeSeen,
            clientHandshakeSeen,
            fixedPrivateExponent15Matches,
            serverHandshakeLength,
            clientHandshakeLength,
            status,
            frames.ToArray());
    }

    static byte[] DecodeTcpRelayCaptureSample(TcpRelayCaptureAnalyzerChunk chunk)
    {
        if (chunk.SampleLength <= 0 || string.IsNullOrEmpty(chunk.FirstBase64))
        {
            return [];
        }

        var sample = Convert.FromBase64String(chunk.FirstBase64);
        if (sample.Length < chunk.SampleLength)
        {
            throw new InvalidDataException(
                $"Relay sample for {chunk.Direction} declares {chunk.SampleLength} bytes but only contains {sample.Length} bytes.");
        }

        return sample.Length == chunk.SampleLength
            ? sample
            : sample.AsSpan(0, chunk.SampleLength).ToArray();
    }

    static void AddLegacyWorldRelayEncryptedChunk(
        List<LegacyWorldRelayEncryptedChunk> chunks,
        string direction,
        byte[] sample,
        int offset,
        bool truncatedAfterSample)
    {
        if (offset < 0 || offset > sample.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Relay sample offset is outside the sample bounds.");
        }

        if (offset == sample.Length && !truncatedAfterSample)
        {
            return;
        }

        chunks.Add(new LegacyWorldRelayEncryptedChunk(
            direction,
            sample.AsSpan(offset).ToArray(),
            truncatedAfterSample));
    }

    static bool TryGetCompleteLegacyWorldClientHandshakeLength(byte[] plain, out int length)
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

    static bool LegacyWorldRelayDhPublicKeysMatch(string expected, string actual)
    {
        return string.Equals(
            NormalizeLegacyWorldRelayDhHex(expected),
            NormalizeLegacyWorldRelayDhHex(actual),
            StringComparison.OrdinalIgnoreCase);
    }

    static string NormalizeLegacyWorldRelayDhHex(string value)
    {
        var normalized = value.Trim().TrimStart('0');
        return normalized.Length == 0 ? "0" : normalized;
    }

    static string DecodeLegacyWorldRelayFrames(
        IReadOnlyList<LegacyWorldRelayEncryptedChunk> encryptedChunks,
        LegacyGameServerCipher serverToClientCipher,
        LegacyGameServerCipher clientToServerCipher,
        int maxFrames,
        int sampleBytes,
        List<LegacyWorldRelayDecodeFrame> frames)
    {
        var serverToClientBuffer = new List<byte>();
        var clientToServerBuffer = new List<byte>();
        var disabledDirections = new HashSet<string>(StringComparer.Ordinal);
        string? truncateStatus = null;

        foreach (var chunk in encryptedChunks)
        {
            if (frames.Count >= maxFrames)
            {
                return "max-frames";
            }

            if (disabledDirections.Contains(chunk.Direction))
            {
                continue;
            }

            var buffer = string.Equals(chunk.Direction, "serverToClient", StringComparison.Ordinal)
                ? serverToClientBuffer
                : clientToServerBuffer;
            var cipher = string.Equals(chunk.Direction, "serverToClient", StringComparison.Ordinal)
                ? serverToClientCipher
                : clientToServerCipher;

            if (chunk.Bytes.Length > 0)
            {
                var decrypted = chunk.Bytes.ToArray();
                cipher.DecryptInPlace(decrypted);
                buffer.AddRange(decrypted);

                try
                {
                    while (frames.Count < maxFrames && TryTakeLegacyWorldSmokeFrame(buffer, out var frame))
                    {
                        frames.Add(CreateLegacyWorldRelayDecodeFrame(
                            frames.Count,
                            chunk.Direction,
                            frame,
                            sampleBytes));
                    }
                }
                catch (InvalidDataException ex)
                {
                    return $"frame-decode-failed:{ex.Message}";
                }
            }

            if (chunk.TruncatedAfterSample)
            {
                disabledDirections.Add(chunk.Direction);
                truncateStatus ??= $"{chunk.Direction}-sample-truncated";
            }
        }

        if (frames.Count >= maxFrames)
        {
            return "max-frames";
        }

        return truncateStatus ?? (frames.Count == 0 ? "no-frames" : "decoded");
    }

    static LegacyWorldRelayDecodeFrame CreateLegacyWorldRelayDecodeFrame(
        int index,
        string direction,
        PacketFrame frame,
        int sampleBytes)
    {
        var frameBytes = frame.Frame.ToArray();
        var sampleLength = Math.Min(sampleBytes, frameBytes.Length);
        var sample = frameBytes.AsSpan(0, sampleLength).ToArray();
        var isAnswerOk = LegacyMsgMessagePacket.TryParse(frame, out var message) &&
            message.Content.Count > 3 &&
            string.Equals(message.Content[3], "ANSWER_OK", StringComparison.Ordinal);

        return new LegacyWorldRelayDecodeFrame(
            index,
            direction,
            frame.Header.Opcode,
            (int)frame.Frame.Length,
            (int)frame.Payload.Length,
            sampleLength,
            Convert.ToHexString(sample).ToLowerInvariant(),
            Convert.ToBase64String(sample),
            isAnswerOk);
    }

    static string FormatLegacyWorldRelayDecodeOpcodes(IReadOnlyList<LegacyWorldRelayDecodeFrame> frames)
    {
        return frames.Count == 0
            ? "(none)"
            : string.Join(", ", frames.Take(12).Select(frame => $"{frame.Direction}:{frame.Opcode}"));
    }
}

internal sealed record LegacyWorldRelayDecodeOptions(
    string CapturePath,
    string OutputPath,
    int MaxFrames,
    int SampleBytes);

internal sealed record LegacyWorldRelayEncryptedChunk(
    string Direction,
    byte[] Bytes,
    bool TruncatedAfterSample);

internal sealed record LegacyWorldRelayDecodeFile(
    DateTimeOffset DecodedAtUtc,
    string SourcePath,
    string SourceSha256,
    long SourceLength,
    string ListenAddress,
    int ListenPort,
    string TargetAddress,
    int TargetPort,
    int MaxFrames,
    int SampleBytes,
    bool ServerHandshakeSeen,
    bool ClientHandshakeSeen,
    bool FixedPrivateExponent15Matches,
    int ServerHandshakeLength,
    int ClientHandshakeLength,
    string Status,
    IReadOnlyList<LegacyWorldRelayDecodeFrame> Frames);

internal sealed record LegacyWorldRelayDecodeFrame(
    int Index,
    string Direction,
    ushort Opcode,
    int Length,
    int PayloadLength,
    int SampleLength,
    string FirstHex,
    string FirstBase64,
    bool IsAnswerOk);

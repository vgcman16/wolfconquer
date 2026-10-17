using System.Buffers;
using System.Net.Sockets;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

internal static partial class LegacyWorldActiveBridgeCaptureRunner
{
    private const int RelayBufferSize = 4096;

    private static async Task RelayDirectionAsync(
        string direction,
        NetworkStream source,
        NetworkStream destination,
        LegacyGameServerCipher sourceDecryptCipher,
        LegacyGameServerCipher destinationEncryptCipher,
        DirectionFrameDecoder decoder,
        BridgeCaptureState captureState)
    {
        var buffer = new byte[RelayBufferSize];

        while (!captureState.IsStopRequested)
        {
            var readLimit = captureState.GetReadLimit(buffer.Length);
            if (readLimit <= 0)
            {
                captureState.RequestStop("max-bytes");
                return;
            }

            int read;
            try
            {
                read = await source.ReadAsync(buffer.AsMemory(0, readLimit), captureState.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                captureState.RequestStop(captureState.IdleTimedOut ? "idle-timeout" : "cancelled");
                return;
            }
            catch (IOException ex)
            {
                captureState.RequestStop($"{direction}-read-error:{ex.Message}");
                return;
            }
            catch (SocketException ex)
            {
                captureState.RequestStop($"{direction}-socket-error:{ex.Message}");
                return;
            }

            if (read == 0)
            {
                captureState.RequestStop(
                    string.Equals(direction, ClientToServerDirection, StringComparison.Ordinal)
                        ? "client-closed"
                        : "target-closed");
                return;
            }

            var encrypted = buffer.AsSpan(0, read).ToArray();
            await ProcessRelayChunkSafelyAsync(
                    direction,
                    encrypted,
                    sourceDecryptCipher,
                    destinationEncryptCipher,
                    destination,
                    decoder,
                    captureState)
                .ConfigureAwait(false);
        }
    }

    private static async Task ProcessRelayChunkSafelyAsync(
        string direction,
        byte[] encrypted,
        LegacyGameServerCipher sourceDecryptCipher,
        LegacyGameServerCipher destinationEncryptCipher,
        NetworkStream destination,
        DirectionFrameDecoder decoder,
        BridgeCaptureState captureState)
    {
        try
        {
            await ProcessRelayChunkAsync(
                    direction,
                    encrypted,
                    sourceDecryptCipher,
                    destinationEncryptCipher,
                    destination,
                    decoder,
                    captureState)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            captureState.RequestStop(captureState.IdleTimedOut ? "idle-timeout" : "cancelled");
        }
        catch (IOException ex)
        {
            captureState.RequestStop($"{direction}-write-error:{ex.Message}");
        }
        catch (SocketException ex)
        {
            captureState.RequestStop($"{direction}-socket-error:{ex.Message}");
        }
    }

    private static async Task ProcessRelayChunkAsync(
        string direction,
        byte[] encrypted,
        LegacyGameServerCipher sourceDecryptCipher,
        LegacyGameServerCipher destinationEncryptCipher,
        NetworkStream destination,
        DirectionFrameDecoder decoder,
        BridgeCaptureState captureState)
    {
        if (encrypted.Length == 0)
        {
            return;
        }

        captureState.ResetIdle();

        var decrypted = encrypted.ToArray();
        sourceDecryptCipher.DecryptInPlace(decrypted);

        var maxBytesReached = captureState.RecordBytes(direction, encrypted.Length);
        captureState.RecordChunk(direction, decrypted);
        var maxFramesReached = CaptureFrames(direction, decrypted, decoder, captureState);

        var outgoing = decrypted.ToArray();
        destinationEncryptCipher.EncryptInPlace(outgoing);
        await destination.WriteAsync(outgoing, captureState.Token).ConfigureAwait(false);
        await destination.FlushAsync(captureState.Token).ConfigureAwait(false);

        if (maxFramesReached && !captureState.Options.ContinueAfterMaxFrames)
        {
            captureState.RequestStop("max-frames");
        }
        else if (maxBytesReached)
        {
            captureState.RequestStop("max-bytes");
        }
    }

    private static bool CaptureFrames(
        string direction,
        byte[] decrypted,
        DirectionFrameDecoder decoder,
        BridgeCaptureState captureState)
    {
        if (decoder.Disabled || captureState.FrameCount >= captureState.Options.MaxFrames)
        {
            captureState.RecordFrameLimitReached(direction);
            return captureState.FrameCount >= captureState.Options.MaxFrames;
        }

        decoder.Buffer.AddRange(decrypted);

        try
        {
            while (captureState.FrameCount < captureState.Options.MaxFrames &&
                TryTakeLegacyFrame(decoder, out var frame))
            {
                captureState.RecordFrame(direction, frame);
            }
        }
        catch (InvalidDataException ex)
        {
            decoder.Disabled = true;
            captureState.RecordWarning($"{direction}:frame-decode-disabled:{ex.Message}");
        }

        if (captureState.FrameCount >= captureState.Options.MaxFrames)
        {
            captureState.RecordFrameLimitReached(direction);
            return true;
        }

        return false;
    }

    private static bool TryTakeLegacyFrame(DirectionFrameDecoder decoder, out PacketFrame frame)
    {
        frame = default;
        var decryptedBuffer = decoder.Buffer;
        var buffer = decryptedBuffer.ToArray();
        if (!LegacyGameServerFrameCodec.TryStripSeal(
                buffer,
                decoder.ExpectedSeal,
                out var modernFrame,
                out var consumed))
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

    private sealed class DirectionFrameDecoder
    {
        public DirectionFrameDecoder(LegacyGameServerFrameSeal expectedSeal)
        {
            ExpectedSeal = expectedSeal;
        }

        public LegacyGameServerFrameSeal ExpectedSeal { get; }

        public List<byte> Buffer { get; } = new();

        public bool Disabled { get; set; }
    }
}

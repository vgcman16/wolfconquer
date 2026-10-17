using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.Networking;

public static class LegacyGameServerPacketPipeline
{
    public static async ValueTask ReadFramesAsync(
        PipeReader reader,
        LegacyGameServerCipher cipher,
        Func<PacketFrame, CancellationToken, ValueTask> handleFrame,
        int maxFrameSize = PacketHeader.DefaultMaxFrameSize,
        CancellationToken cancellationToken = default,
        LegacyGameServerFrameSeal expectedSeal = LegacyGameServerFrameSeal.Server,
        Action<string>? trace = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(cipher);
        ArgumentNullException.ThrowIfNull(handleFrame);

        var decryptedBuffer = new List<byte>();
        while (true)
        {
            var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var sequence = result.Buffer;

            try
            {
                if (sequence.Length > 0)
                {
                    var bufferedBefore = decryptedBuffer.Count;
                    var encrypted = sequence.ToArray();
                    cipher.DecryptInPlace(encrypted);
                    decryptedBuffer.AddRange(encrypted);
                    trace?.Invoke(
                        "inbound-chunk " +
                        $"encryptedLength={sequence.Length} decryptedLength={encrypted.Length} " +
                        $"bufferedBefore={bufferedBefore} expectedSeal={expectedSeal} " +
                        $"decryptedHex={HexForLog(encrypted)}");
                }

                while (TryTakeFrame(decryptedBuffer, maxFrameSize, expectedSeal, out var frame))
                {
                    trace?.Invoke(
                        "inbound-frame " +
                        $"frameLength={frame.Frame.Length} opcode={frame.Header.Opcode} " +
                        $"payloadLength={frame.Payload.Length} expectedSeal={expectedSeal} " +
                        $"frameHex={HexForLog(frame.Frame.ToArray())}");
                    await handleFrame(frame, cancellationToken).ConfigureAwait(false);
                }

                if (result.IsCompleted)
                {
                    if (decryptedBuffer.Count > 0)
                    {
                        throw new EndOfStreamException(
                            "The legacy GameServer packet stream ended with an incomplete sealed frame. " +
                            $"bufferedBytes={decryptedBuffer.Count} expectedSeal={expectedSeal} maxFrameSize={maxFrameSize}.");
                    }

                    break;
                }
            }
            finally
            {
                reader.AdvanceTo(sequence.End, sequence.End);
            }
        }
    }

    private static bool TryTakeFrame(
        List<byte> decryptedBuffer,
        int maxFrameSize,
        LegacyGameServerFrameSeal expectedSeal,
        out PacketFrame frame)
    {
        frame = default;
        var buffer = decryptedBuffer.ToArray();
        if (!LegacyGameServerFrameCodec.TryStripSeal(
                buffer,
                expectedSeal,
                out var modernFrame,
                out var consumed,
                maxFrameSize))
        {
            return false;
        }

        var sequence = new ReadOnlySequence<byte>(modernFrame);
        if (!PacketFrameDecoder.TryRead(
                sequence,
                out frame,
                out var frameConsumed,
                out _,
                maxFrameSize) ||
            sequence.GetOffset(frameConsumed) != modernFrame.Length)
        {
            throw new InvalidDataException(
                "Decoded legacy GameServer sealed frame did not contain one complete modern packet frame. " +
                $"decodedBytes={modernFrame.Length} consumedBytes={sequence.GetOffset(frameConsumed)} maxFrameSize={maxFrameSize}.");
        }

        decryptedBuffer.RemoveRange(0, consumed);
        return true;
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
}

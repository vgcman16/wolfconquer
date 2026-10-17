using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.Networking;

public static class LegacyAccServerPacketPipeline
{
    public static async ValueTask ReadFramesAsync(
        PipeReader reader,
        LegacyAccServerCipher cipher,
        Func<PacketFrame, CancellationToken, ValueTask> handleFrame,
        int maxFrameSize = PacketHeader.DefaultMaxFrameSize,
        CancellationToken cancellationToken = default,
        Action<LegacyAccServerPacketChunkTrace>? trace = null)
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
                    var encrypted = sequence.ToArray();
                    var decrypted = encrypted.ToArray();
                    var decryptCounterBefore = cipher.DecryptCounter;
                    cipher.Decrypt(decrypted);
                    trace?.Invoke(new LegacyAccServerPacketChunkTrace(
                        EncryptedBytes: encrypted,
                        DecryptedBytes: decrypted,
                        DecryptCounterBefore: decryptCounterBefore,
                        DecryptCounterAfter: cipher.DecryptCounter,
                        BufferedBytesBeforeAppend: decryptedBuffer.Count));
                    decryptedBuffer.AddRange(decrypted);
                }

                while (TryTakeFrame(decryptedBuffer, maxFrameSize, out var frame))
                {
                    await handleFrame(frame, cancellationToken).ConfigureAwait(false);
                }

                if (result.IsCompleted)
                {
                    if (decryptedBuffer.Count > 0)
                    {
                        throw new EndOfStreamException(
                            "The legacy AccountServer packet stream ended with an incomplete frame. " +
                            $"bufferedBytes={decryptedBuffer.Count} maxFrameSize={maxFrameSize}.");
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
        out PacketFrame frame)
    {
        frame = default;
        var buffer = decryptedBuffer.ToArray();
        var sequence = new ReadOnlySequence<byte>(buffer);
        if (!PacketFrameDecoder.TryRead(sequence, out frame, out var consumed, out _, maxFrameSize))
        {
            return false;
        }

        decryptedBuffer.RemoveRange(0, checked((int)sequence.GetOffset(consumed)));
        return true;
    }
}

public readonly record struct LegacyAccServerPacketChunkTrace(
    byte[] EncryptedBytes,
    byte[] DecryptedBytes,
    ushort DecryptCounterBefore,
    ushort DecryptCounterAfter,
    int BufferedBytesBeforeAppend);

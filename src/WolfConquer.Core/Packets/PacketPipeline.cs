using System.IO;
using System.IO.Pipelines;

namespace WolfConquer.Core.Packets;

public static class PacketPipeline
{
    public static async ValueTask ReadFramesAsync(
        PipeReader reader,
        Func<PacketFrame, CancellationToken, ValueTask> handleFrame,
        int maxFrameSize = PacketHeader.DefaultMaxFrameSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(handleFrame);

        while (true)
        {
            var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var sequence = result.Buffer;
            var remaining = sequence;
            var consumed = sequence.Start;
            var examined = sequence.End;

            try
            {
                while (true)
                {
                    if (!PacketFrameDecoder.TryRead(remaining, out var frame, out var frameConsumed, out var frameExamined, maxFrameSize))
                    {
                        examined = frameExamined;
                        break;
                    }

                    await handleFrame(frame, cancellationToken).ConfigureAwait(false);
                    consumed = frameConsumed;
                    examined = frameExamined;
                    remaining = sequence.Slice(consumed);
                }

                if (result.IsCompleted)
                {
                    if (remaining.Length > 0)
                    {
                        throw new EndOfStreamException(
                            $"The packet stream ended with an incomplete frame. remainingBytes={remaining.Length} maxFrameSize={maxFrameSize}.");
                    }

                    break;
                }
            }
            finally
            {
                reader.AdvanceTo(consumed, examined);
            }
        }
    }
}

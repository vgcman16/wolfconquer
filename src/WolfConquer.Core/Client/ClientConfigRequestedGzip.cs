namespace WolfConquer.Core.Client;

/// <summary>
/// The inspected D13 requested-length gzip reader. It preserves buffered reads,
/// member lookahead and count equality; it is not a whole-stream validator.
/// </summary>
public static class ClientConfigRequestedGzip
{
    // Finite offline-tool bounds, not native-format or native allocation limits.
    public const int MaximumInputBytes = 64 * 1024 * 1024;
    public const int MaximumOutputBytes = 64 * 1024 * 1024;

    public static byte[] Decode(ReadOnlySpan<byte> transformedContents, int maximumOutputBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumOutputBytes);
        if (transformedContents.Length > MaximumInputBytes)
            throw new InvalidDataException("Configuration gzip input exceeds the offline tooling bound.");
        var bytes = transformedContents.ToArray();
        var declared = ClientConfigGzipCandidate.ReadDeclaredOutputLength(bytes);
        if (declared > int.MaxValue || declared > MaximumOutputBytes || declared > maximumOutputBytes)
            throw new InvalidDataException("Configuration gzip requested length exceeds the available bound.");

        // The target's zero-length read does not initialize or drain inflation.
        // Header/CRC/trailer validation occurs only when actual reads reach it.
        var output = new byte[(int)declared];
        if (output.Length == 0) return output;
        var reader = new RequestedReader(bytes);
        if (reader.Read(output) != output.Length)
            throw new InvalidDataException("Configuration gzip produced fewer bytes than requested.");
        return output;
    }

    private sealed class RequestedReader(byte[] bytes)
    {
        private const int BufferBytes = 2 * 8192;
        private readonly ClientConfigGzipBitInput input = new(bytes);
        private readonly byte[] buffer = new byte[BufferBytes];
        private ClientConfigGzipInflate? member;
        private int available, offset;
        private bool end;

        internal int Read(Span<byte> output)
        {
            var written = 0;
            while (written < output.Length)
            {
                if (available != 0)
                {
                    var count = Math.Min(available, output.Length - written);
                    buffer.AsSpan(offset, count).CopyTo(output[written..]);
                    available -= count; offset += count; written += count;
                    continue;
                }

                if (end) break;
                if (member is not null && output.Length - written >= BufferBytes)
                {
                    written += member.Inflate(output[written..]);
                    UpdateMemberState();
                }
                else
                {
                    Fill();
                }
            }

            return written;
        }

        private void Fill()
        {
            while (!end && available == 0)
            {
                if (member is null && !StartMember()) break;
                offset = 0;
                available = member!.Inflate(buffer);
                UpdateMemberState();
            }
        }

        private bool StartMember()
        {
            // After a gzip member, the native look state ignores a non-gzip
            // suffix. The outer candidate gate established the first magic.
            if (input.RemainingAlignedBytes < 2 || input.PeekAlignedByte(0) != 0x1F || input.PeekAlignedByte(1) != 0x8B)
            {
                end = true;
                return false;
            }

            member = new(input);
            return true;
        }

        private void UpdateMemberState()
        {
            if (member!.IsTruncated) end = true;
            else if (member.IsFinished) member = null;
        }
    }
}

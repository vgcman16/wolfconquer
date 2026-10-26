namespace WolfConquer.Core.Client;

internal sealed class ClientConfigGzipBitInput(byte[] bytes)
{
    private readonly byte[] bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
    private uint buffered;
    private int bufferedBits;

    internal int BytePosition { get; private set; }
    internal bool HasExhausted { get; private set; }
    internal int RemainingAlignedBytes => bufferedBits == 0 ? bytes.Length - BytePosition :
        throw new InvalidOperationException("The gzip input is not byte-aligned.");

    internal int ReadBits(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, 16);
        while (bufferedBits < count)
        {
            if (BytePosition == bytes.Length) throw Truncated();
            buffered |= (uint)bytes[BytePosition++] << bufferedBits;
            bufferedBits += 8;
        }

        var value = (int)(buffered & ((1u << count) - 1));
        buffered >>= count;
        bufferedBits -= count;
        return value;
    }

    internal void AlignToByte()
    {
        var discard = bufferedBits & 7;
        buffered >>= discard;
        bufferedBits -= discard;
    }

    internal byte PeekAlignedByte(int relativeOffset)
    {
        if (relativeOffset < 0 || relativeOffset >= RemainingAlignedBytes) throw Truncated();
        return bytes[BytePosition + relativeOffset];
    }

    internal byte ReadAlignedByte()
    {
        if (bufferedBits != 0) throw new InvalidOperationException("The gzip input is not byte-aligned.");
        if (BytePosition == bytes.Length) throw Truncated();
        return bytes[BytePosition++];
    }

    internal ushort ReadUInt16LittleEndian() => (ushort)(ReadAlignedByte() | ReadAlignedByte() << 8);

    internal uint ReadUInt32LittleEndian() => (uint)ReadAlignedByte() | (uint)ReadAlignedByte() << 8 |
        (uint)ReadAlignedByte() << 16 | (uint)ReadAlignedByte() << 24;

    internal ReadOnlySpan<byte> Slice(int start, int length) => bytes.AsSpan(start, length);

    private InvalidDataException Truncated()
    {
        HasExhausted = true;
        return new("Configuration gzip input is truncated.");
    }
}

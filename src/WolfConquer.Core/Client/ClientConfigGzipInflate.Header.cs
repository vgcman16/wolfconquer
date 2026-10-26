namespace WolfConquer.Core.Client;

internal sealed partial class ClientConfigGzipInflate
{
    private static void ReadHeader(ClientConfigGzipBitInput input)
    {
        var start = input.BytePosition;
        if (input.ReadAlignedByte() != 0x1F || input.ReadAlignedByte() != 0x8B || input.ReadAlignedByte() != 8)
        {
            throw new InvalidDataException("Configuration gzip member has an invalid header or method.");
        }

        var flags = input.ReadAlignedByte();
        if ((flags & 0xE0) != 0) throw new InvalidDataException("Configuration gzip member has reserved header flags.");
        for (var index = 0; index < 6; index++) input.ReadAlignedByte();
        if ((flags & 4) != 0)
        {
            var count = input.ReadUInt16LittleEndian();
            for (var index = 0; index < count; index++) input.ReadAlignedByte();
        }

        if ((flags & 8) != 0) ReadZeroTerminated(input);
        if ((flags & 16) != 0) ReadZeroTerminated(input);
        if ((flags & 2) != 0)
        {
            var checksum = ClientConfigGzipChecksum.Compute(input.Slice(start, input.BytePosition - start));
            if (input.ReadUInt16LittleEndian() != unchecked((ushort)checksum))
            {
                throw new InvalidDataException("Configuration gzip header checksum is invalid.");
            }
        }
    }

    private static void ReadZeroTerminated(ClientConfigGzipBitInput input)
    {
        while (input.ReadAlignedByte() != 0) { }
    }

    private void ReadTrailer()
    {
        input.AlignToByte();
        if (input.ReadUInt32LittleEndian() != (checksum ^ uint.MaxValue))
        {
            throw new InvalidDataException("Configuration gzip data checksum is invalid.");
        }

        if (input.ReadUInt32LittleEndian() != memberSize)
        {
            throw new InvalidDataException("Configuration gzip member length is invalid.");
        }

        mode = InflateMode.Finished;
    }
}

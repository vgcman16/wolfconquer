namespace WolfConquer.Core.Client;

/// <summary>The inspected inflater's fixed or dynamic literal/distance alphabets.</summary>
internal sealed class ClientConfigDeflateCodeSet
{
    private static ReadOnlySpan<int> CodeLengthOrder =>
        [16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15];

    private ClientConfigDeflateCodeSet(ClientConfigDeflateCodes literals, ClientConfigDeflateCodes distances)
    {
        Literals = literals;
        Distances = distances;
    }

    internal ClientConfigDeflateCodes Literals { get; }
    internal ClientConfigDeflateCodes Distances { get; }
    internal static ClientConfigDeflateCodeSet Fixed { get; } = CreateFixed();

    internal static ClientConfigDeflateCodeSet ReadDynamic(ClientConfigGzipBitInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var literalCount = input.ReadBits(5) + 257;
        var distanceCount = input.ReadBits(5) + 1;
        var codeLengthCount = input.ReadBits(4) + 4;
        // The pinned reader refuses HDIST counts 31 and 32, even though the
        // fixed alphabet contains those two reserved distance symbols.
        if (literalCount > 286 || distanceCount > 30)
            throw new InvalidDataException("DEFLATE dynamic alphabet count is invalid.");

        int[] codeLengths = new int[19];
        for (var index = 0; index < codeLengthCount; index++)
            codeLengths[CodeLengthOrder[index]] = input.ReadBits(3);
        var lengthCodes = ClientConfigDeflateCodes.Create(codeLengths, codeLengthAlphabet: true);
        var lengths = ReadLengths(input, lengthCodes, literalCount + distanceCount);
        if (lengths[256] == 0)
            throw new InvalidDataException("DEFLATE end-of-block code is missing.");

        return new ClientConfigDeflateCodeSet(
            ClientConfigDeflateCodes.Create(lengths.AsSpan(0, literalCount)),
            ClientConfigDeflateCodes.Create(lengths.AsSpan(literalCount), allowEmpty: true));
    }

    private static int[] ReadLengths(ClientConfigGzipBitInput input,
        ClientConfigDeflateCodes lengthCodes, int count)
    {
        int[] lengths = new int[count];
        var cursor = 0;
        while (cursor < count)
        {
            var symbol = lengthCodes.Decode(input);
            if (symbol < 16)
            {
                lengths[cursor++] = symbol;
                continue;
            }

            var value = 0;
            int repeat;
            switch (symbol)
            {
                case 16:
                    if (cursor == 0)
                        throw new InvalidDataException("DEFLATE repeat has no preceding code length.");
                    value = lengths[cursor - 1];
                    repeat = input.ReadBits(2) + 3;
                    break;
                case 17:
                    repeat = input.ReadBits(3) + 3;
                    break;
                case 18:
                    repeat = input.ReadBits(7) + 11;
                    break;
                default:
                    throw new InvalidDataException("DEFLATE code-length symbol is invalid.");
            }
            if (repeat > count - cursor)
                throw new InvalidDataException("DEFLATE repeat exceeds its alphabets.");
            lengths.AsSpan(cursor, repeat).Fill(value);
            cursor += repeat;
        }
        return lengths;
    }

    private static ClientConfigDeflateCodeSet CreateFixed()
    {
        int[] literals = new int[288];
        literals.AsSpan(0, 144).Fill(8);
        literals.AsSpan(144, 112).Fill(9);
        literals.AsSpan(256, 24).Fill(7);
        literals.AsSpan(280, 8).Fill(8);
        int[] distances = new int[32];
        distances.AsSpan().Fill(5);
        return new ClientConfigDeflateCodeSet(
            ClientConfigDeflateCodes.Create(literals), ClientConfigDeflateCodes.Create(distances));
    }
}

namespace WolfConquer.Core.Client;

/// <summary>Immutable canonical DEFLATE codebook; data bits are read from the shared bit input.</summary>
internal sealed class ClientConfigDeflateCodes
{
    private const int MaximumBits = 15;
    private const int MaximumSymbols = 288;
    private readonly int[] _counts;
    private readonly int[] _firstCodes;
    private readonly int[] _firstSymbols;
    private readonly int[] _symbols;
    private readonly int _maximumLength;

    private ClientConfigDeflateCodes(int[] counts, int[] firstCodes, int[] firstSymbols,
        int[] symbols, int maximumLength)
    {
        _counts = counts;
        _firstCodes = firstCodes;
        _firstSymbols = firstSymbols;
        _symbols = symbols;
        _maximumLength = maximumLength;
    }

    internal static ClientConfigDeflateCodes Create(ReadOnlySpan<int> lengths,
        bool codeLengthAlphabet = false, bool allowEmpty = false)
    {
        if (lengths.IsEmpty || lengths.Length > MaximumSymbols)
            throw new InvalidDataException("DEFLATE alphabet size is invalid.");

        var capturedLengths = lengths.ToArray();
        int[] counts = new int[MaximumBits + 1];
        var maximumLength = 0;
        foreach (var length in capturedLengths)
        {
            if (length is < 0 or > MaximumBits)
                throw new InvalidDataException("DEFLATE code length is invalid.");
            if (length == 0)
                continue;
            counts[length]++;
            maximumLength = Math.Max(maximumLength, length);
        }

        if (maximumLength == 0)
        {
            if (!allowEmpty || codeLengthAlphabet)
                throw new InvalidDataException("DEFLATE codebook is empty.");
            return new ClientConfigDeflateCodes(counts, new int[MaximumBits + 1],
                new int[MaximumBits + 1], [], 0);
        }

        var available = 1;
        for (var length = 1; length <= MaximumBits; length++)
        {
            available = (available << 1) - counts[length];
            if (available < 0)
                throw new InvalidDataException("DEFLATE codebook is oversubscribed.");
        }
        if (available != 0 && (codeLengthAlphabet || maximumLength != 1))
            throw new InvalidDataException("DEFLATE codebook is incomplete.");

        int[] firstCodes = new int[MaximumBits + 1];
        int[] firstSymbols = new int[MaximumBits + 1];
        var code = 0;
        var symbolCount = 0;
        for (var length = 1; length <= MaximumBits; length++)
        {
            code = (code + counts[length - 1]) << 1;
            firstCodes[length] = code;
            firstSymbols[length] = symbolCount;
            symbolCount += counts[length];
        }

        int[] symbols = new int[symbolCount];
        var offsets = firstSymbols.ToArray();
        for (var symbol = 0; symbol < capturedLengths.Length; symbol++)
        {
            var length = capturedLengths[symbol];
            if (length != 0)
                symbols[offsets[length]++] = symbol;
        }
        return new ClientConfigDeflateCodes(counts, firstCodes, firstSymbols, symbols, maximumLength);
    }

    internal int Decode(ClientConfigGzipBitInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (_maximumLength == 0)
        {
            // The native empty-distance table has two one-bit invalid entries.
            // It must wait for that bit before distinguishing invalid distance
            // from input exhaustion; the requested gzip reader observes this.
            input.ReadBits(1);
            throw new InvalidDataException("DEFLATE symbol does not belong to its codebook.");
        }
        var code = 0;
        for (var length = 1; length <= _maximumLength; length++)
        {
            // DEFLATE packs bytes least-significant-bit first, but a Huffman
            // code's successive transmitted bits are its canonical high bits first.
            code = (code << 1) | input.ReadBits(1);
            var offset = code - _firstCodes[length];
            if ((uint)offset < (uint)_counts[length])
                return _symbols[_firstSymbols[length] + offset];
        }
        throw new InvalidDataException("DEFLATE symbol does not belong to its codebook.");
    }
}

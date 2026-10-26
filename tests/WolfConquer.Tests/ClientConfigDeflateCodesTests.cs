using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientConfigDeflateCodesTests
{
    [Fact]
    public void CanonicalSymbolOrderConsumesExactCodeBitsAcrossByteBoundaries()
    {
        var codes = ClientConfigDeflateCodes.Create([2, 3, 3, 3, 3, 2]);
        // Canonical words for symbols 0,5,1,2,3,4 are 00,01,100,101,110,111.
        var input = new ClientConfigGzipBitInput([0x98, 0xee, 0xa5]);
        foreach (var symbol in new[] { 0, 5, 1, 2, 3, 4 })
            Assert.Equal(symbol, codes.Decode(input));
        Assert.Equal(0xa5, input.ReadBits(8));
    }

    [Fact]
    public void AdmittedSingleSymbolUsesOneBitAndRejectsTheUnusedBranch()
    {
        var codes = ClientConfigDeflateCodes.Create([0, 1, 0]);
        var input = new ClientConfigGzipBitInput([0b0000_0010]);
        Assert.Equal(1, codes.Decode(input));
        Assert.Throws<InvalidDataException>(() => codes.Decode(input));
    }

    [Fact]
    public void FullFifteenBitTreeDecodesWithoutReadingAhead()
    {
        var codes = ClientConfigDeflateCodes.Create([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 15]);
        var input = new ClientConfigGzipBitInput([0xff, 0x7f]);
        Assert.Equal(15, codes.Decode(input));
        Assert.Equal(0, codes.Decode(input));
    }

    [Fact]
    public void CallerLengthMutationCannotAlterAnAdmittedBook()
    {
        int[] lengths = [1, 1];
        var codes = ClientConfigDeflateCodes.Create(lengths);
        Array.Fill(lengths, 15);
        var input = new ClientConfigGzipBitInput([0b0000_0010]);
        Assert.Equal(0, codes.Decode(input));
        Assert.Equal(1, codes.Decode(input));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("too-many")]
    [InlineData("negative")]
    [InlineData("sixteen")]
    [InlineData("oversubscribed")]
    [InlineData("incomplete")]
    [InlineData("zero")]
    public void InvalidCodebookShapesRefuse(string shape)
    {
        int[] lengths = shape switch
        {
            "empty" => [],
            "too-many" => new int[289],
            "negative" => [-1, 1],
            "sixteen" => [16, 1],
            "oversubscribed" => [1, 1, 1],
            "incomplete" => [2, 2],
            _ => [0, 0]
        };
        Assert.Throws<InvalidDataException>(() => ClientConfigDeflateCodes.Create(lengths));
    }

    [Fact]
    public void CodeLengthAlphabetRequiresACompleteNonemptyTree()
    {
        Assert.Throws<InvalidDataException>(() => ClientConfigDeflateCodes.Create([1, 0], codeLengthAlphabet: true));
        Assert.Throws<InvalidDataException>(() => ClientConfigDeflateCodes.Create([0], codeLengthAlphabet: true, allowEmpty: true));
        var codes = ClientConfigDeflateCodes.Create([1, 1], codeLengthAlphabet: true);
        Assert.Equal(1, codes.Decode(new ClientConfigGzipBitInput([1])));
    }

    [Fact]
    public void ExplicitEmptyDistanceBookConsumesItsNativeInvalidEntryBitBeforeRefusing()
    {
        var codes = ClientConfigDeflateCodes.Create([0], allowEmpty: true);
        var input = new ClientConfigGzipBitInput([0xa5]);
        Assert.Throws<InvalidDataException>(() => codes.Decode(input));
        Assert.False(input.HasExhausted);
        Assert.Equal(0x52, input.ReadBits(7));
    }

    [Fact]
    public void TruncatedSymbolAndNullInputRefuse()
    {
        var codes = ClientConfigDeflateCodes.Create([1, 1]);
        Assert.Throws<InvalidDataException>(() => codes.Decode(new ClientConfigGzipBitInput([])));
        Assert.Throws<ArgumentNullException>(() => codes.Decode(null!));
    }

    [Fact]
    public void FixedLiteralAndReservedDistanceGoldensAreNotRenumbered()
    {
        var literals = ClientConfigDeflateCodeSet.Fixed.Literals;
        var bits = new ClientConfigDeflateTestBits();
        bits.Code("00110000").Code("10111111").Code("110010000").Code("111111111")
            .Code("0000000").Code("0010111").Code("11000000").Code("11000101")
            .Code("11000110").Code("11000111");
        var input = new ClientConfigGzipBitInput(bits.ToArray());
        foreach (var symbol in new[] { 0, 143, 144, 255, 256, 279, 280, 285, 286, 287 })
            Assert.Equal(symbol, literals.Decode(input));

        var distances = new ClientConfigGzipBitInput(
            new ClientConfigDeflateTestBits().Code("00000").Code("11101").Code("11110").Code("11111").ToArray());
        foreach (var symbol in new[] { 0, 29, 30, 31 })
            Assert.Equal(symbol, ClientConfigDeflateCodeSet.Fixed.Distances.Decode(distances));
    }
}

internal sealed class ClientConfigDeflateTestBits
{
    private readonly List<int> _bits = [];

    internal ClientConfigDeflateTestBits Low(int value, int count)
    {
        for (var index = 0; index < count; index++)
            _bits.Add((value >> index) & 1);
        return this;
    }

    internal ClientConfigDeflateTestBits Code(string bits)
    {
        foreach (var bit in bits)
        {
            if (bit is not ('0' or '1'))
                throw new ArgumentException("Fixture bit is invalid.", nameof(bits));
            _bits.Add(bit - '0');
        }
        return this;
    }

    internal byte[] ToArray()
    {
        byte[] result = new byte[(_bits.Count + 7) / 8];
        for (var index = 0; index < _bits.Count; index++)
            result[index / 8] |= (byte)(_bits[index] << (index % 8));
        return result;
    }
}

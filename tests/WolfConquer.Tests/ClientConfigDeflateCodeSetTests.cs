using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientConfigDeflateCodeSetTests
{
    [Fact]
    public void DynamicSingleEobAndEmptyDistanceAreAdmittedWithoutReadAhead()
    {
        var bits = Header();
        ZeroRun(bits, 138);
        ZeroRun(bits, 118);
        Token(bits, 1);
        Token(bits, 0);
        bits.Code("0").Low(0, 1).Low(0x5a, 8); // Native one-bit invalid distance entry, then sentinel.
        var input = new ClientConfigGzipBitInput(bits.ToArray());
        var codes = ClientConfigDeflateCodeSet.ReadDynamic(input);
        Assert.Equal(256, codes.Literals.Decode(input));
        Assert.Throws<InvalidDataException>(() => codes.Distances.Decode(input));
        Assert.Equal(0x5a, input.ReadBits(8));
    }

    [Fact]
    public void DynamicRepeatsSeventeenEighteenAndSixteenCrossTheAlphabetBoundary()
    {
        var bits = Header(distanceCount: 4);
        Token(bits, 2);
        Token(bits, 2);
        Token(bits, 2);
        ZeroRun(bits, 138);
        ZeroRun(bits, 105);
        ZeroRun(bits, 10);
        Token(bits, 2);
        Token(bits, 16);
        bits.Low(1, 2); // Four repetitions of EOB's length fill all four distance lengths.
        bits.Code("00").Code("01").Code("10").Code("11")
            .Code("00").Code("01").Code("10").Code("11").Low(0xa5, 8);
        var input = new ClientConfigGzipBitInput(bits.ToArray());
        var codes = ClientConfigDeflateCodeSet.ReadDynamic(input);
        foreach (var symbol in new[] { 0, 1, 2, 256 })
            Assert.Equal(symbol, codes.Literals.Decode(input));
        foreach (var symbol in new[] { 0, 1, 2, 3 })
            Assert.Equal(symbol, codes.Distances.Decode(input));
        Assert.Equal(0xa5, input.ReadBits(8));
    }

    [Theory]
    [InlineData(18)]
    [InlineData(19)]
    public void MaximumNativeDynamicCountsAndOptionalNineteenthLengthSlotAreAdmitted(int codeLengthCount)
    {
        var bits = Header(literalCount: 286, distanceCount: 30, codeLengthCount: codeLengthCount);
        ZeroRun(bits, 138);
        ZeroRun(bits, 118);
        Token(bits, 1);
        ZeroRun(bits, 59); // Remaining 29 literals plus all 30 distances.
        bits.Code("0");
        var input = new ClientConfigGzipBitInput(bits.ToArray());
        var codes = ClientConfigDeflateCodeSet.ReadDynamic(input);
        Assert.Equal(256, codes.Literals.Decode(input));
        Assert.Throws<InvalidDataException>(() => codes.Distances.Decode(input));
    }

    [Theory]
    [InlineData(30, 0)]
    [InlineData(31, 0)]
    [InlineData(0, 30)]
    [InlineData(0, 31)]
    public void PinnedReaderRefusesLiteralCountsAbove286AndDistanceCountsAbove30(int hlit, int hdist)
    {
        var input = new ClientConfigGzipBitInput(new ClientConfigDeflateTestBits()
            .Low(hlit, 5).Low(hdist, 5).Low(0, 4).ToArray());
        Assert.Throws<InvalidDataException>(() => ClientConfigDeflateCodeSet.ReadDynamic(input));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("incomplete")]
    [InlineData("oversubscribed")]
    public void DynamicCodeLengthAlphabetMustBeComplete(string shape)
    {
        var bits = new ClientConfigDeflateTestBits().Low(0, 5).Low(0, 5).Low(0, 4);
        int[] firstFour = shape switch
        {
            "incomplete" => [0, 0, 0, 1],
            "oversubscribed" => [1, 1, 1, 0],
            _ => [0, 0, 0, 0]
        };
        foreach (var length in firstFour)
            bits.Low(length, 3);
        Assert.Throws<InvalidDataException>(() => ClientConfigDeflateCodeSet.ReadDynamic(
            new ClientConfigGzipBitInput(bits.ToArray())));
    }

    [Fact]
    public void RepeatSixteenCannotInventAPreviousLength()
    {
        var bits = Header();
        Token(bits, 16);
        bits.Low(0, 2);
        Refuses(bits);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    public void EveryRepeatFormRefusesPastTheCombinedAlphabetEnd(int repeat)
    {
        var bits = Header();
        ZeroRun(bits, 138);
        ZeroRun(bits, 118);
        if (repeat == 16)
            Token(bits, 1);
        Token(bits, repeat);
        bits.Low(0, repeat == 16 ? 2 : repeat == 17 ? 3 : 7);
        Refuses(bits);
    }

    [Theory]
    [InlineData("missing-eob")]
    [InlineData("incomplete-literal")]
    [InlineData("oversubscribed-literal")]
    [InlineData("incomplete-distance")]
    [InlineData("oversubscribed-distance")]
    public void DecodedAlphabetsStillRequireEobAndAdmittedTreeShapes(string shape)
    {
        var bits = Header(distanceCount: shape == "oversubscribed-distance" ? 3 : 1);
        if (shape == "missing-eob")
        {
            Token(bits, 1);
            ZeroRun(bits, 138);
            ZeroRun(bits, 118);
            Token(bits, 0);
        }
        else if (shape == "oversubscribed-literal")
        {
            Token(bits, 1);
            Token(bits, 1);
            ZeroRun(bits, 138);
            ZeroRun(bits, 116);
            Token(bits, 1);
            Token(bits, 0);
        }
        else if (shape == "oversubscribed-distance")
        {
            ZeroRun(bits, 138);
            ZeroRun(bits, 118);
            Token(bits, 1);
            Token(bits, 16);
            bits.Low(0, 2);
        }
        else
        {
            ZeroRun(bits, 138);
            ZeroRun(bits, 118);
            Token(bits, shape == "incomplete-literal" ? 2 : 1);
            Token(bits, shape == "incomplete-distance" ? 2 : 0);
        }
        Refuses(bits);
    }

    [Theory]
    [InlineData("header")]
    [InlineData("code-lengths")]
    [InlineData("repeat-extra")]
    public void TruncatedDynamicInputRefuses(string stage)
    {
        byte[] bytes;
        if (stage == "header")
            bytes = [];
        else if (stage == "code-lengths")
            bytes = [0, 0];
        else
        {
            var bits = Header();
            Token(bits, 18); // Header ends at bit68: only one padded bit remains, below seven required.
            bytes = bits.ToArray();
        }
        Assert.Throws<InvalidDataException>(() => ClientConfigDeflateCodeSet.ReadDynamic(new ClientConfigGzipBitInput(bytes)));
    }

    [Fact]
    public void NullDynamicInputRefusesClearly() =>
        Assert.Throws<ArgumentNullException>(() => ClientConfigDeflateCodeSet.ReadDynamic(null!));

    private static void Refuses(ClientConfigDeflateTestBits bits) =>
        Assert.Throws<InvalidDataException>(() => ClientConfigDeflateCodeSet.ReadDynamic(new ClientConfigGzipBitInput(bits.ToArray())));

    private static ClientConfigDeflateTestBits Header(int literalCount = 257, int distanceCount = 1, int codeLengthCount = 18)
    {
        // Caller-selected header counts. The independent complete
        // length-code alphabet is: 0=00,1=01,2=100,16=101,17=110,18=111.
        var bits = new ClientConfigDeflateTestBits().Low(literalCount - 257, 5)
            .Low(distanceCount - 1, 5).Low(codeLengthCount - 4, 4);
        int[] orderedLengths = [3, 3, 3, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 2];
        foreach (var length in orderedLengths)
            bits.Low(length, 3);
        if (codeLengthCount == 19)
            bits.Low(0, 3);
        return bits;
    }

    private static void Token(ClientConfigDeflateTestBits bits, int symbol) => bits.Code(symbol switch
    {
        0 => "00", 1 => "01", 2 => "100", 16 => "101", 17 => "110", 18 => "111",
        _ => throw new ArgumentOutOfRangeException(nameof(symbol))
    });

    private static void ZeroRun(ClientConfigDeflateTestBits bits, int count)
    {
        if (count is >= 3 and <= 10)
        {
            Token(bits, 17);
            bits.Low(count - 3, 3);
        }
        else if (count is >= 11 and <= 138)
        {
            Token(bits, 18);
            bits.Low(count - 11, 7);
        }
        else
            throw new ArgumentOutOfRangeException(nameof(count));
    }
}

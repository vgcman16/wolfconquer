using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientConfigEmptyDistanceTimingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyDistanceEntryDistinguishesExhaustionFromAnAvailableInvalidBit(bool hasBit)
    {
        var input = new ClientConfigGzipBitInput(hasBit ? [0] : []);
        var codes = ClientConfigDeflateCodes.Create([0], allowEmpty: true);
        Assert.Throws<InvalidDataException>(() => codes.Decode(input));
        Assert.Equal(!hasBit, input.HasExhausted);
        Assert.Equal(hasBit ? 1 : 0, input.BytePosition);
        if (hasBit) Assert.Equal(0, input.ReadBits(7));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealInflateRetainsProducedBytesOnlyWhenTheInvalidDistanceBitIsUnavailable(bool hasBit)
    {
        const int literalCount = 16388;
        var bits = new ClientConfigDeflateTestBits().Low(1, 1).Low(2, 2)
            .Low(1, 5).Low(0, 5).Low(14, 4);
        int[] order = [16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15];
        foreach (var symbol in order.Take(18)) bits.Low(symbol is 0 or 1 or 2 or 18 ? 2 : 0, 3);
        // Literal65 has canonical0/1bit, EOB256 is10/2bits, length257 is11.
        // A complete code-length alphabet admits those rows and an empty
        // distance alphabet. The prefix ends exactly after a length symbol.
        bits.Code("11").Low(54, 7).Code("01")
            .Code("11").Low(127, 7).Code("11").Low(41, 7)
            .Code("10").Code("10").Code("00");
        for (var index = 0; index < literalCount; index++) bits.Code("0");
        bits.Code("11");
        var raw = bits.ToArray();
        Assert.Equal(2062, raw.Length); // No zero-padding bit remains at this boundary.
        byte[] header = [0x1F, 0x8B, 8, 0, 0, 0, 0, 0, 0, 3];
        var bytes = header.Concat(raw).Concat(hasBit ? new byte[] { 0 } : Array.Empty<byte>()).ToArray();
        var input = new ClientConfigGzipBitInput(bytes);
        var member = new ClientConfigGzipInflate(input);
        var output = new byte[literalCount];

        if (hasBit)
        {
            Assert.Throws<InvalidDataException>(() => member.Inflate(output));
            Assert.False(input.HasExhausted);
            Assert.False(member.IsTruncated);
        }
        else
        {
            Assert.Equal(literalCount, member.Inflate(output));
            Assert.True(input.HasExhausted);
            Assert.True(member.IsTruncated);
            Assert.False(member.IsFinished);
        }
        Assert.Equal(Enumerable.Repeat((byte)65, literalCount), output);
        Assert.Equal(bytes.Length, input.BytePosition);
    }
}

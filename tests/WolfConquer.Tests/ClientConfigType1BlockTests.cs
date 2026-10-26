using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientConfigType1BlockTests
{
    private static byte[] Golden() => Convert.FromHexString("0001FFFFFFFFFFFFFFFFFFFFFF004142");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullAndMinimalBigEndianGoldensReturnTheSameOwnedPayload(bool minimal)
    {
        var full = Golden();
        Assert.Equal(16, full.Length);
        var encoded = minimal ? full[1..] : full;
        var preserved = encoded.ToArray();

        var payload = ClientConfigType1Block.Decode(encoded, 16, 16);

        Assert.Equal(new byte[] { 0x41, 0x42 }, payload);
        Assert.Equal(preserved, encoded);
        Array.Fill(payload, (byte)0);
        Assert.Equal(preserved, encoded);
    }

    [Fact]
    public void ZeroAndFfBytesAfterTheDelimiterArePayloadRatherThanMorePadding()
    {
        byte[] encoded = [0, 1, 255, 255, 255, 255, 255, 255, 255, 255, 0, 0, 255, 65, 0, 255];
        Assert.Equal(new byte[] { 0, 255, 65, 0, 255 }, ClientConfigType1Block.Decode(encoded, 16, 5));
    }

    [Fact]
    public void ExactlyEightPaddingBytesMayYieldAnEmptyStandalonePayload()
    {
        byte[] encoded = [0, 1, 255, 255, 255, 255, 255, 255, 255, 255, 0];
        Assert.Empty(ClientConfigType1Block.Decode(encoded, 11, 0));
        Assert.Empty(ClientConfigType1Block.Decode(encoded[1..], 11, 0));
    }

    [Fact]
    public void OutputCapacityIsCheckedBeforeAnyPayloadIsPublished()
    {
        var encoded = Golden();
        var preserved = encoded.ToArray();
        Assert.Throws<InvalidDataException>(() => ClientConfigType1Block.Decode(encoded, 16, 1));
        Assert.Equal(preserved, encoded);
        Assert.Equal(new byte[] { 0x41, 0x42 }, ClientConfigType1Block.Decode(encoded, 16, 2));
    }

    [Theory]
    [InlineData(17)]
    [InlineData(int.MaxValue)]
    public void LargerCallerCapacityReturnsOnlyTheBoundedPayloadWithoutChangingInput(int capacity)
    {
        var encoded = Golden();
        var preserved = encoded.ToArray();

        var payload = ClientConfigType1Block.Decode(encoded, 16, capacity);

        Assert.Equal(new byte[] { 0x41, 0x42 }, payload);
        Assert.Equal(preserved, encoded);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("short")]
    [InlineData("long")]
    [InlineData("minimal-short")]
    [InlineData("full-leading-nonzero")]
    [InlineData("double-leading-zero")]
    [InlineData("type-two")]
    [InlineData("seven-padding-bytes")]
    [InlineData("non-ff-padding-byte")]
    [InlineData("missing-delimiter")]
    [InlineData("reversed-byte-order")]
    public void InvalidRecoveredBlocksRefuseWithoutChangingInput(string kind)
    {
        var encoded = Golden();
        switch (kind)
        {
            case "empty": encoded = []; break;
            case "short": encoded = encoded[..^2]; break;
            case "long": encoded = encoded.Concat(new byte[] { 0 }).ToArray(); break;
            case "minimal-short": encoded = encoded[1..^1]; break;
            case "full-leading-nonzero": encoded[0] = 1; break;
            case "double-leading-zero": encoded[1] = 0; break;
            case "type-two": encoded[1] = 2; break;
            case "seven-padding-bytes": encoded[9] = 0; break;
            case "non-ff-padding-byte": encoded[5] = 0xFE; break;
            case "missing-delimiter": encoded.AsSpan(2).Fill(0xFF); break;
            case "reversed-byte-order": Array.Reverse(encoded); break;
        }
        var preserved = encoded.ToArray();

        Assert.Throws<InvalidDataException>(() => ClientConfigType1Block.Decode(encoded, 16, 16));

        Assert.Equal(preserved, encoded);
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(2049, 0)]
    [InlineData(16, -1)]
    public void UnsupportedDefaultProfileBoundsRefuseBeforeBlockAccess(int width, int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ClientConfigType1Block.Decode(Array.Empty<byte>(), width, capacity));
    }

    [Fact]
    public void DefaultMethodMaximumWidthRemainsBoundedAndMayHaveEmptyPayload()
    {
        var encoded = Enumerable.Repeat((byte)0xFF, 2048).ToArray();
        encoded[0] = 0;
        encoded[1] = 1;
        encoded[^1] = 0;
        Assert.Empty(ClientConfigType1Block.Decode(encoded, 2048, 2048));
    }
}

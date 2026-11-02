using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937ItemInfoEnvelopeTests
{
    [Theory]
    [InlineData("E002")]
    [InlineData("E20205FFFF")]
    [InlineData("E90200000000000000")]
    [InlineData("E502010203")]
    [InlineData("FB020800")]
    [InlineData("FC02")]
    [InlineData("FB02FC03")]
    [InlineData("E60200")]
    [InlineData("E70200")]
    [InlineData("00")]
    [InlineData("02")]
    [InlineData("FFFFFFFF1F00")]
    [InlineData("E002FFFFFFFFFFFFFFFFFFFF")]
    public void MalformedExtensionRecordsRefuseWithoutPartialPublication(string tail) =>
        Refuses(Convert.FromHexString(Required + tail), Chinese7937ItemInfoEnvelopeFailure.InvalidWire);

    [Theory]
    [InlineData("0A0100")]
    [InlineData("D80100")]
    [InlineData("F8018080808010")]
    public void InvalidKnownRecordsCannotBeLaunderedIntoOpaqueExtensions(string tail) =>
        Refuses(Convert.FromHexString(Required + tail), Chinese7937ItemInfoEnvelopeFailure.KnownSchema);

    [Fact]
    public void GroupDepthAndExtensionCountHaveExplicitCodecBounds()
    {
        var atDepth = Required + string.Concat(Enumerable.Repeat("E302", 16)) + string.Concat(Enumerable.Repeat("E402", 16));
        Assert.Single(Parse(atDepth).UnknownFields);
        var beyondDepth = Required + string.Concat(Enumerable.Repeat("E302", 17)) + string.Concat(Enumerable.Repeat("E402", 17));
        Refuses(Convert.FromHexString(beyondDepth), Chinese7937ItemInfoEnvelopeFailure.ExtensionBounds);
        var atCount = Required + string.Concat(Enumerable.Repeat("E00200", 1024));
        Assert.Equal(1024, Parse(atCount).UnknownFields.Count);
        Refuses(Convert.FromHexString(atCount + "E00200"), Chinese7937ItemInfoEnvelopeFailure.ExtensionBounds);
    }

    [Theory]
    [InlineData(7899)]
    [InlineData(7931)]
    [InlineData(7935)]
    [InlineData(7938)]
    public void OtherDeclaredProfilesCannotBorrowNumeric2350(int version)
    {
        Assert.False(Chinese7937ItemInfoEnvelope.TryParsePayload(Convert.FromHexString(Required), version,
            out var envelope, out var failure));
        Assert.Null(envelope);
        Assert.Equal(Chinese7937ItemInfoEnvelopeFailure.UnsupportedVersion, failure);
    }

    [Fact]
    public void EmptyAndOversizedPayloadsRefuseBeforePublication()
    {
        Refuses([], Chinese7937ItemInfoEnvelopeFailure.InputBounds);
        Refuses(new byte[Chinese7937ItemInfoEnvelope.MaximumPayloadBytes + 1], Chinese7937ItemInfoEnvelopeFailure.InputBounds);
    }

    [Fact]
    public void FailedLaterParseDoesNotChangeAnEarlierAcceptedEnvelope()
    {
        var earlier = Parse(Extended);
        Refuses(Convert.FromHexString(Required + "FB0200FC02"), Chinese7937ItemInfoEnvelopeFailure.InvalidWire);
        Assert.Equal(Convert.FromHexString(Extended), earlier.ToPayloadBytes());
        Assert.Equal(17u, earlier.KnownFields.ItemUid);
    }

    private static void Refuses(byte[] payload, Chinese7937ItemInfoEnvelopeFailure expected)
    {
        Assert.False(Chinese7937ItemInfoEnvelope.TryParsePayload(payload, 7937, out var envelope, out var failure));
        Assert.Null(envelope);
        Assert.Equal(expected, failure);
    }
}

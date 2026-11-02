using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937EchoOperationEnvelopeTests
{
    [Theory]
    [InlineData("00")]
    [InlineData("010000000000000000")]
    [InlineData("07")]
    [InlineData("08")]
    [InlineData("58")]
    [InlineData("5880")]
    [InlineData("588080808080808080808000")]
    [InlineData("58FFFFFFFFFFFFFFFFFF02")]
    [InlineData("6100000000000000")]
    [InlineData("7D000000")]
    [InlineData("6A01")]
    [InlineData("6A80")]
    [InlineData("6AFFFFFFFFFFFFFFFFFF01")]
    [InlineData("74")]
    [InlineData("73")]
    [InlineData("730800")]
    [InlineData("736C")]
    [InlineData("730074")]
    [InlineData("5E")]
    [InlineData("5F")]
    [InlineData("808080801000")]
    [InlineData("8080808080808080808000")]
    [InlineData("4A0118")]
    [InlineData("4A021880")]
    [InlineData("4A03250000")]
    [InlineData("4A021B24")]
    public void EveryMalformedTailRefusesTheEntireOtherwiseValidMessage(string tail) =>
        Refuses(Convert.FromHexString("0808" + tail), Chinese7937EchoEnvelopeFailure.InvalidWire);

    [Theory]
    [InlineData("5800")]
    [InlineData("73080874")]
    [InlineData("4A020808")]
    [InlineData("08080D00000000")]
    [InlineData("0808108080808010")]
    [InlineData("08084344")]
    [InlineData("08084800")]
    [InlineData("0808508080808010")]
    [InlineData("08084A020A00")]
    [InlineData("08084A06088080808010")]
    [InlineData("08084A06108080808010")]
    public void RequiredOperationAndKnownWireWidthsCannotBeSuppliedByOpaqueOrOtherScopes(string hex) =>
        Refuses(Convert.FromHexString(hex), Chinese7937EchoEnvelopeFailure.KnownSchema);

    [Fact]
    public void UInt64OpaqueValuesAndMaximumFieldNumberAreRetainedWithoutNarrowing()
    {
        const string hex = "080858FFFFFFFFFFFFFFFFFF01F8FFFFFF0F004A0618FFFFFFFF0F";
        var envelope = Parse(hex);
        Assert.Equal(new[] { 11, 536870911, 3 }, envelope.UnknownFields.Select(field => field.FieldNumber));
        Assert.Equal(Convert.FromHexString(hex), envelope.ToPayloadBytes());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7935)]
    [InlineData(7939)]
    public void ExplicitSupportedVersionIsRequiredEvenForAValidKnownOnlyMessage(int version)
    {
        Assert.False(Chinese7937EchoOperationEnvelope.TryParsePayload(Convert.FromHexString("0808"), version,
            out var result, out var failure));
        Assert.Null(result); Assert.Equal(Chinese7937EchoEnvelopeFailure.UnsupportedVersion, failure);
    }

    [Fact]
    public void EmptyInputAndOversizeInputRefuseBeforeAnyPartialEnvelope()
    {
        Refuses([], Chinese7937EchoEnvelopeFailure.InputBounds);
        Refuses(new byte[65532], Chinese7937EchoEnvelopeFailure.InputBounds);
    }

    private static void Refuses(byte[] bytes, Chinese7937EchoEnvelopeFailure expected)
    {
        Assert.False(Chinese7937EchoOperationEnvelope.TryParsePayload(bytes, 7937, out var result, out var failure));
        Assert.Null(result); Assert.Equal(expected, failure);
    }
}

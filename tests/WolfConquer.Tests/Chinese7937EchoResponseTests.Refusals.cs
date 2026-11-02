using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937EchoResponseTests
{
    [Theory]
    [InlineData("38072801")]
    [InlineData("00")]
    [InlineData("0880")]
    [InlineData("088080808010")]
    [InlineData("08081200")]
    [InlineData("0808388080808010")]
    [InlineData("0808420341")]
    [InlineData("080842FFFFFFFFFFFFFFFFFF02")]
    [InlineData("08084800")]
    [InlineData("08084A050801")]
    [InlineData("08084A020A00")]
    [InlineData("08084A021801")]
    [InlineData("08085200")]
    [InlineData("08085801")]
    [InlineData("08080B0C")]
    [InlineData("080880808080800101")]
    [InlineData("080828013880")]
    [InlineData("080A1001180220034A80")]
    [InlineData("080D58")]
    public void CompleteMalformedTailsAndUnsupportedSchemaNeverPublishAResponse(string hex) =>
        Refuses(Convert.FromHexString(hex), Chinese7937EchoResponseFailure.KnownSchema);

    [Theory]
    [InlineData("0800")]
    [InlineData("0806")]
    [InlineData("080D")]
    [InlineData("080E")]
    [InlineData("08FFFFFFFF0F")]
    [InlineData("0808080D")]
    public void OtherOperationsRemainValidInTheOldGenericCodecWithoutGainingThisProjection(string hex)
    {
        var input = Convert.FromHexString(hex);
        Assert.True(Chinese7935XuanbaoOperationPayload.TryParseKnownPayload(input, out var generic));
        Assert.NotNull(generic);
        Refuses(input, Chinese7937EchoResponseFailure.UnsupportedOperation);
    }

    [Theory]
    [InlineData(0)] [InlineData(7899)] [InlineData(7931)] [InlineData(7935)] [InlineData(7938)]
    public void ExplicitVersionRefusalPrecedesPayloadAndFrameInspection(int version)
    {
        Assert.False(Chinese7937EchoResponse.TryParsePayload([], version, out var response, out var failure));
        Assert.Null(response); Assert.Equal(Chinese7937EchoResponseFailure.UnsupportedVersion, failure);
        Assert.False(Chinese7937EchoResponse.TryParseFrame(default, version, out response, out failure));
        Assert.Null(response); Assert.Equal(Chinese7937EchoResponseFailure.UnsupportedVersion, failure);
    }

    [Fact]
    public void ExactPayloadBoundaryHasAnIndependentValidControlAndRefusesOneMoreByte()
    {
        // Operation8 plus raw field8 with declared65525 bytes, in canonical
        // base128 form. Six overhead bytes make the full65531-byte payload.
        var payload = new byte[65_531];
        Convert.FromHexString("080842F5FF03").CopyTo(payload, 0);
        Assert.Equal(payload.Length, Chinese7937EchoResponse.MaximumPayloadBytes);
        var response = Parse(payload);
        Assert.Equal(65_525, response.KnownPayload.CopyData8()!.Length);
        Assert.Equal(payload, response.CopyPayloadBytes());
        var frame = Frame(payload);
        Assert.Equal((ushort)65_535, frame.Header.Length);
        Assert.True(Chinese7937EchoResponse.TryParseFrame(frame, 7937, out var framed, out var failure), failure.ToString());
        Assert.Equal(payload, framed!.CopyPayloadBytes());
        Refuses([], Chinese7937EchoResponseFailure.InputBounds);
        Refuses(new byte[65_532], Chinese7937EchoResponseFailure.InputBounds);
        Assert.False(Chinese7937EchoResponse.TryParseFrame(Frame([]), 7937, out framed, out failure));
        Assert.Null(framed); Assert.Equal(Chinese7937EchoResponseFailure.InputBounds, failure);
    }

    [Fact]
    public void CompleteRepeatedChildBoundDoesNotTruncateOrInventResponseArguments()
    {
        var maximum = Chinese7935XuanbaoOperationPayload.MaximumAccInfoEntries;
        var payload = new byte[2 + maximum * 2];
        payload[0] = 8; payload[1] = 8;
        for (var index = 2; index < payload.Length; index += 2) payload[index] = 0x4a;
        var response = Parse(payload);
        Assert.Equal(maximum, response.KnownPayload.AccInfo.Length);
        Assert.All(response.Arguments.ToArray(), argument => Assert.Null(argument.WireValue));
        Refuses(payload.Concat(new byte[] { 0x4a, 0 }).ToArray(), Chinese7937EchoResponseFailure.KnownSchema);
        Assert.Equal(payload, response.CopyPayloadBytes());
        Assert.Equal(maximum, response.KnownPayload.AccInfo.Length);
    }

    [Fact]
    public void ForgedHeadersWholeFramesAndSeparatePayloadsCannotBeReconciledIntoAResponse()
    {
        var frame = Frame(Convert.FromHexString(Operation8Golden));
        var payloadMismatch = frame.Payload.ToArray(); payloadMismatch[^1] ^= 1;
        var rawLengthMismatch = frame.Frame.ToArray(); rawLengthMismatch[0]--;
        var rawOpcodeMismatch = frame.Frame.ToArray(); rawOpcodeMismatch[2]++;
        PacketFrame[] invalid =
        [
            default,
            frame with { Header = new((ushort)(frame.Header.Length - 1), 2132) },
            frame with { Header = new(frame.Header.Length, 2133) },
            frame with { Frame = new ReadOnlySequence<byte>(rawLengthMismatch) },
            frame with { Frame = new ReadOnlySequence<byte>(rawOpcodeMismatch) },
            frame with { Frame = frame.Frame.Slice(0, frame.Frame.Length - 1) },
            frame with { Frame = new ReadOnlySequence<byte>(frame.Frame.ToArray().Concat(new byte[] { 0 }).ToArray()) },
            frame with { Frame = ReadOnlySequence<byte>.Empty },
            frame with { Payload = new ReadOnlySequence<byte>(payloadMismatch) },
            frame with { Payload = ReadOnlySequence<byte>.Empty },
            Frame(Convert.FromHexString(Operation8Golden), 2133),
        ];
        foreach (var forged in invalid)
        {
            Assert.False(Chinese7937EchoResponse.TryParseFrame(forged, 7937, out var response, out var failure));
            Assert.Null(response); Assert.Equal(Chinese7937EchoResponseFailure.MalformedFrame, failure);
        }
    }

    [Fact]
    public void LaterRefusalDoesNotChangeAnEarlierOwnedResponse()
    {
        var response = Parse(Operation10Golden);
        var preserved = response.CopyPayloadBytes();
        var arguments = response.Arguments.ToArray();
        Refuses(preserved.Concat(new byte[] { 0x38, 0x80 }).ToArray(), Chinese7937EchoResponseFailure.KnownSchema);
        Assert.Equal(preserved, response.CopyPayloadBytes());
        Assert.Equal(arguments, response.Arguments.ToArray());
        Assert.Equal(uint.MaxValue, response.KnownPayload.Fields.Field10);
    }

    private static void Refuses(byte[] input, Chinese7937EchoResponseFailure expected)
    {
        var preserved = input.ToArray();
        Assert.False(Chinese7937EchoResponse.TryParsePayload(input, 7937, out var response, out var failure));
        Assert.Null(response); Assert.Equal(expected, failure);
        Assert.Equal(preserved, input);
    }
}

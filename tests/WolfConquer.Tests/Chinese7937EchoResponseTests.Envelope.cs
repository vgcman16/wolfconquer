using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937EchoResponseTests
{
    // Independent synthetic records, including interleaved opaque parent/child
    // fields, duplicate known fields and valid nonminimal encodings.
    private const string Envelope8 = "58810008880038014A0608021803100028810038005D010203044202AABB";
    private const string Envelope8Frame = "22005408" + Envelope8;
    private const string Envelope10 = "6A0200FF080A10014A041807080010001800208200";
    private const string Envelope10Frame = "19005408" + Envelope10;

    [Fact]
    public void Operation8EnvelopeProjectionPreservesSourceAndEveryOrderedOpaqueRecord()
    {
        var input = Convert.FromHexString(Envelope8);
        var envelope = ParseEnvelope(input);
        Assert.True(Chinese7937EchoResponse.TryProjectEnvelope(envelope, out var result, out var failure), failure.ToString());
        var response = Assert.IsType<Chinese7937EchoResponse>(result);
        Assert.Equal(Chinese7937EchoResponseFailure.None, failure);
        Assert.Same(envelope, response.SourceEnvelope);
        Assert.Equal(8U, response.Operation); Assert.Equal(0x36U, response.InternalUiEventId);
        Assert.Equal(new Chinese7937EchoArgument[] { new(7, 0), new(5, 1) }, response.Arguments.ToArray());
        Assert.Equal(new Chinese7935XuanbaoAccInfo[] { new(2, 0) }, response.KnownPayload.AccInfo.ToArray());
        Assert.Equal(new (int Field, int Wire, int? Child)[] { (11, 0, null), (3, 0, 0), (11, 5, null) },
            envelope.UnknownFields.Select(field => (field.FieldNumber, field.WireType, field.ChildIndex)));
        Assert.Equal(new[] { "588100", "1803", "5D01020304" },
            envelope.UnknownFields.Select(field => Convert.ToHexString(field.CopyEncodedBytes())));
        Assert.Equal(Convert.FromHexString(Envelope8), response.CopyPayloadBytes());
        Assert.Equal(Convert.FromHexString(Envelope8Frame), response.ToFrameBytes());
        Assert.False(Chinese7937EchoResponse.TryParsePayload(input, 7937, out var strict, out var strictFailure));
        Assert.Null(strict); Assert.Equal(Chinese7937EchoResponseFailure.KnownSchema, strictFailure);
        Assert.False(Chinese7937EchoResponse.TryParseFrame(Frame(input), 7937, out strict, out strictFailure));
        Assert.Null(strict); Assert.Equal(Chinese7937EchoResponseFailure.KnownSchema, strictFailure);
    }

    [Fact]
    public void Operation10EnvelopeProjectionRetainsDuplicateLastValuesAndExactFrame()
    {
        var input = Convert.FromHexString(Envelope10);
        Assert.True(Chinese7937EchoOperationEnvelope.TryParseFrame(Frame(input), 7937, out var decoded, out var sourceFailure), sourceFailure.ToString());
        var envelope = Assert.IsType<Chinese7937EchoOperationEnvelope>(decoded);
        Assert.Equal(Chinese7937EchoEnvelopeFailure.None, sourceFailure);
        Assert.True(Chinese7937EchoResponse.TryProjectEnvelope(envelope, out var result, out var failure), failure.ToString());
        var response = Assert.IsType<Chinese7937EchoResponse>(result);
        Assert.Same(envelope, response.SourceEnvelope);
        Assert.Equal(10U, response.Operation); Assert.Equal(0x37U, response.InternalUiEventId);
        Assert.Equal(new Chinese7937EchoArgument[] { new(2, 0), new(3, 0), new(4, 2) }, response.Arguments.ToArray());
        Assert.Equal(new Chinese7935XuanbaoAccInfo[] { new(0, null) }, response.KnownPayload.AccInfo.ToArray());
        Assert.Equal(new (int Field, int Wire, int? Child)[] { (13, 2, null), (3, 0, 0) },
            envelope.UnknownFields.Select(field => (field.FieldNumber, field.WireType, field.ChildIndex)));
        Assert.Equal(new[] { "6A0200FF", "1807" }, envelope.UnknownFields.Select(field => Convert.ToHexString(field.CopyEncodedBytes())));
        Assert.Equal(Convert.FromHexString(Envelope10), response.CopyPayloadBytes());
        Assert.Equal(Convert.FromHexString(Envelope10Frame), response.ToFrameBytes());
    }

    [Theory]
    [InlineData("08085800", "0808380028005800")]
    [InlineData("080A5800", "080A1000180020005800")]
    public void ProjectedEnvelopeKeepsAbsentDistinctFromPresentZero(string absentHex, string zeroHex)
    {
        var absent = Project(ParseEnvelope(Convert.FromHexString(absentHex)));
        var zero = Project(ParseEnvelope(Convert.FromHexString(zeroHex)));
        Assert.Equal(absent.Arguments.ToArray().Select(value => value.FieldNumber), zero.Arguments.ToArray().Select(value => value.FieldNumber));
        Assert.All(absent.Arguments.ToArray(), value => { Assert.False(value.IsPresent); Assert.Null(value.WireValue); Assert.Equal(0U, value.NativeStorageValue); });
        Assert.All(zero.Arguments.ToArray(), value => { Assert.True(value.IsPresent); Assert.Equal(0U, value.WireValue); Assert.Equal(0U, value.NativeStorageValue); });
        Assert.Equal(Convert.FromHexString(absentHex), absent.CopyPayloadBytes());
        Assert.Equal(Convert.FromHexString(zeroHex), zero.CopyPayloadBytes());
    }

    [Fact]
    public void EnvelopeAndResponseRetainOwnedSourceDespiteInputAndReturnedCopyMutations()
    {
        var input = Convert.FromHexString(Envelope8);
        var envelope = ParseEnvelope(input);
        var response = Project(envelope);
        Array.Clear(input);
        var payloadCopy = response.CopyPayloadBytes(); Array.Clear(payloadCopy);
        var frameCopy = response.ToFrameBytes(); Array.Clear(frameCopy);
        var opaqueCopy = envelope.UnknownFields[0].CopyEncodedBytes(); Array.Clear(opaqueCopy);
        var knownCopy = response.KnownPayload.CopyData8()!; Array.Clear(knownCopy);
        var argumentCopy = response.Arguments.ToArray(); argumentCopy[0] = new(99, 99);
        var childCopy = response.KnownPayload.AccInfo.ToArray(); childCopy[0] = new(99, 99);
        Assert.Equal(Convert.FromHexString(Envelope8), envelope.ToPayloadBytes());
        Assert.Equal(Convert.FromHexString(Envelope8), response.CopyPayloadBytes());
        Assert.Equal(Convert.FromHexString(Envelope8Frame), response.ToFrameBytes());
        Assert.Equal("588100", Convert.ToHexString(envelope.UnknownFields[0].CopyEncodedBytes()));
        Assert.Equal(new byte[] { 0xAA, 0xBB }, response.KnownPayload.CopyData8());
        Assert.Equal(new Chinese7937EchoArgument(7, 0), response.Arguments[0]);
        Assert.Equal(new Chinese7935XuanbaoAccInfo(2, 0), response.KnownPayload.AccInfo[0]);
    }

    [Fact]
    public void ExistingStrictResponseRetainsNullSourceAndExactFrame()
    {
        var response = Parse("080838002800");
        Assert.Null(response.SourceEnvelope);
        Assert.Equal(Convert.FromHexString("0A005408080838002800"), response.ToFrameBytes());
    }

    private static Chinese7937EchoOperationEnvelope ParseEnvelope(byte[] bytes)
    {
        Assert.True(Chinese7937EchoOperationEnvelope.TryParsePayload(bytes, 7937, out var envelope, out var failure), failure.ToString());
        Assert.Equal(Chinese7937EchoEnvelopeFailure.None, failure);
        return Assert.IsType<Chinese7937EchoOperationEnvelope>(envelope);
    }

    private static Chinese7937EchoResponse Project(Chinese7937EchoOperationEnvelope envelope)
    {
        Assert.True(Chinese7937EchoResponse.TryProjectEnvelope(envelope, out var response, out var failure), failure.ToString());
        Assert.Equal(Chinese7937EchoResponseFailure.None, failure);
        return Assert.IsType<Chinese7937EchoResponse>(response);
    }
}

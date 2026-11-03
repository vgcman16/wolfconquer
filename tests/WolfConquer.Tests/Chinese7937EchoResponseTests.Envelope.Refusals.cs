using System.Buffers;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937EchoResponseTests
{
    [Fact]
    public void NullEnvelopeAndUnsupportedKnownOperationNeverPublishAResponse()
    {
        Assert.False(Chinese7937EchoResponse.TryProjectEnvelope(null, out var response, out var failure));
        Assert.Null(response); Assert.Equal(Chinese7937EchoResponseFailure.SourceRequired, failure);
        var valid = Project(ParseEnvelope(Convert.FromHexString(Envelope8)));
        var other = ParseEnvelope(Convert.FromHexString("08065800"));
        Assert.False(Chinese7937EchoResponse.TryProjectEnvelope(other, out response, out failure));
        Assert.Null(response); Assert.Equal(Chinese7937EchoResponseFailure.UnsupportedOperation, failure);
        Assert.Equal(Convert.FromHexString(Envelope8), valid.CopyPayloadBytes());
        Assert.NotNull(valid.SourceEnvelope);
    }

    [Theory]
    [InlineData(0)] [InlineData(7931)] [InlineData(7935)] [InlineData(7938)]
    public void EnvelopeSourceRequiresExplicit7937BeforeProjection(int version)
    {
        var payload = Convert.FromHexString(Envelope8);
        Assert.False(Chinese7937EchoOperationEnvelope.TryParsePayload(payload, version, out var envelope, out var failure));
        Assert.Null(envelope); Assert.Equal(Chinese7937EchoEnvelopeFailure.UnsupportedVersion, failure);
        Assert.False(Chinese7937EchoOperationEnvelope.TryParseFrame(Frame(payload), version, out envelope, out failure));
        Assert.Null(envelope); Assert.Equal(Chinese7937EchoEnvelopeFailure.UnsupportedVersion, failure);
    }

    [Theory]
    [InlineData("58")]
    [InlineData("5880")]
    [InlineData("4A021880")]
    [InlineData("6A03AA")]
    [InlineData("74")]
    public void MalformedExtensionTailCannotProduceAnEnvelopeOrProjectedResponse(string tail)
    {
        var bytes = Convert.FromHexString("0808" + tail);
        Assert.False(Chinese7937EchoOperationEnvelope.TryParsePayload(bytes, 7937, out var envelope, out var failure));
        Assert.Null(envelope); Assert.Equal(Chinese7937EchoEnvelopeFailure.InvalidWire, failure);
        Assert.False(Chinese7937EchoResponse.TryProjectEnvelope(envelope, out var response, out var projectionFailure));
        Assert.Null(response); Assert.Equal(Chinese7937EchoResponseFailure.SourceRequired, projectionFailure);
    }

    [Fact]
    public void WrongOpcodeAndSourceBoundsCannotCreateAnEnvelopeForTheJoin()
    {
        var bytes = Convert.FromHexString(Envelope8);
        Assert.False(Chinese7937EchoOperationEnvelope.TryParseFrame(Frame(bytes, 2350), 7937, out var envelope, out var failure));
        Assert.Null(envelope); Assert.Equal(Chinese7937EchoEnvelopeFailure.MalformedFrame, failure);
        Assert.False(Chinese7937EchoOperationEnvelope.TryParsePayload([], 7937, out envelope, out failure));
        Assert.Null(envelope); Assert.Equal(Chinese7937EchoEnvelopeFailure.InputBounds, failure);
        Assert.False(Chinese7937EchoOperationEnvelope.TryParsePayload(new byte[65_532], 7937, out envelope, out failure));
        Assert.Null(envelope); Assert.Equal(Chinese7937EchoEnvelopeFailure.InputBounds, failure);
    }

    [Fact]
    public void ForgedSeparatePayloadIsNotAValidEnvelopeSource()
    {
        var frame = Frame(Convert.FromHexString(Envelope10));
        var changed = frame.Payload.ToArray(); changed[^1] ^= 1;
        var forged = frame with { Payload = new ReadOnlySequence<byte>(changed) };
        Assert.False(Chinese7937EchoOperationEnvelope.TryParseFrame(forged, 7937, out var envelope, out var failure));
        Assert.Null(envelope); Assert.Equal(Chinese7937EchoEnvelopeFailure.MalformedFrame, failure);
        Assert.False(Chinese7937EchoResponse.TryProjectEnvelope(envelope, out var response, out var projectionFailure));
        Assert.Null(response); Assert.Equal(Chinese7937EchoResponseFailure.SourceRequired, projectionFailure);
    }
    [Fact]
    public void ExtensionCountBoundaryRefusesACompleteExtraRecordWithoutPublishingPartialSource()
    {
        var count = Chinese7937EchoOperationEnvelope.MaximumExtensions;
        var input = new byte[2 + 2 * (count + 1)]; input[0] = 0x08; input[1] = 0x08;
        for (var index = 0; index <= count; index++) { input[2 + 2 * index] = 0x58; input[3 + 2 * index] = 0; }
        var accepted = input.AsSpan(0, input.Length - 2);
        Assert.True(Chinese7937EchoOperationEnvelope.TryParsePayload(accepted, 7937, out var valid, out var failure), failure.ToString());
        var projected = Project(Assert.IsType<Chinese7937EchoOperationEnvelope>(valid));
        Assert.Equal(count, projected.SourceEnvelope!.UnknownFields.Count);
        Assert.False(Chinese7937EchoOperationEnvelope.TryParsePayload(input, 7937, out var refused, out failure));
        Assert.Null(refused); Assert.Equal(Chinese7937EchoEnvelopeFailure.ExtensionBounds, failure);
        Assert.Equal(count, projected.SourceEnvelope.UnknownFields.Count);
        Assert.Equal(accepted.ToArray(), projected.CopyPayloadBytes());
    }
}

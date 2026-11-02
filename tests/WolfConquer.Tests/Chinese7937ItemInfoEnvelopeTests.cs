using System.Collections;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937ItemInfoEnvelopeTests
{
    private const string Required = "080010001800";
    // Independently assembled tags44/45/46/47/49, mixed with required1/2/3.
    private const string Extended = "E002090811E90201020304050607081000F2020300FF801804" +
        "FB02080983031D010203048403FC028D0378563412E0020A";

    private static Chinese7937ItemInfoEnvelope Parse(string hex)
    {
        Assert.True(Chinese7937ItemInfoEnvelope.TryParsePayload(Convert.FromHexString(hex), 7937,
            out var envelope, out var failure), failure.ToString());
        Assert.Equal(Chinese7937ItemInfoEnvelopeFailure.None, failure);
        return Assert.IsType<Chinese7937ItemInfoEnvelope>(envelope);
    }

    [Fact]
    public void InterleavedOpaqueWireFormsRetainOrderDuplicatesAndExactOriginalBytes()
    {
        var envelope = Parse(Extended);
        Assert.Equal(17u, envelope.KnownFields.ItemUid);
        Assert.Equal(0u, envelope.KnownFields.Field2);
        Assert.Equal(4u, envelope.KnownFields.Field3);
        Assert.Equal(new[] { 44, 45, 46, 47, 49, 44 }, envelope.UnknownFields.Select(field => field.FieldNumber));
        Assert.Equal(new[] { 0, 1, 2, 3, 5, 0 }, envelope.UnknownFields.Select(field => field.WireType));
        Assert.Equal(Convert.FromHexString("FB02080983031D010203048403FC02"), envelope.UnknownFields[3].CopyEncodedBytes());
        Assert.Equal(Convert.FromHexString(Extended), envelope.ToPayloadBytes());
        Assert.False(envelope.KnownFields.TryGetEchoRelatedFields(out _));
        Assert.False(Chinese7935ItemInfoView.TryParseKnownPayload(Convert.FromHexString(Extended), out _));
    }

    [Fact]
    public void RequiredLookingTagsInsideAnExtensionNeverSatisfyTheKnownMessage()
    {
        Assert.False(Chinese7937ItemInfoEnvelope.TryParsePayload(Convert.FromHexString("FB02080010001800FC02"), 7937,
            out var rejected, out var failure));
        Assert.Null(rejected);
        Assert.Equal(Chinese7937ItemInfoEnvelopeFailure.KnownSchema, failure);
        var present = Parse(Required + "FB0208FFFF03107F1809FC02");
        Assert.Equal(0u, present.KnownFields.ItemUid);
        Assert.Equal(0u, present.KnownFields.Field2);
        Assert.Equal(0u, present.KnownFields.Field3);
        Assert.Single(present.UnknownFields);
    }

    [Fact]
    public void KnownLastValueAndPackedRepeatedSemanticsRemainSeparateFromLosslessStorage()
    {
        const string payload = Required + "08010802E00209F80103FA010BFFFFFFFFFFFFFFFFFF0100F80102";
        var envelope = Parse(payload);
        Assert.Equal(2u, envelope.KnownFields.ItemUid);
        Assert.Equal(new[] { 3, -1, 0, 2 }, envelope.KnownFields.Values31.ToArray());
        Assert.Equal(Convert.FromHexString(payload), envelope.ToPayloadBytes());
        Assert.Single(envelope.UnknownFields);
    }

    [Fact]
    public void NonminimalValidWireSpellingsAndMaximumFieldNumberStayOpaqueAndExact()
    {
        const string payload = "08810010001800E082008100F8FFFFFF0F00";
        var envelope = Parse(payload);
        Assert.Equal(1u, envelope.KnownFields.ItemUid);
        Assert.Equal(new[] { 44, 536_870_911 }, envelope.UnknownFields.Select(field => field.FieldNumber));
        Assert.Equal(Convert.FromHexString("E082008100"), envelope.UnknownFields[0].CopyEncodedBytes());
        Assert.Equal(Convert.FromHexString(payload), envelope.ToPayloadBytes());
    }

    [Fact]
    public void InputExtensionAndOutputCopiesCannotMutateAnAlreadyParsedEnvelope()
    {
        var input = Convert.FromHexString(Extended);
        var expected = input.ToArray();
        Assert.True(Chinese7937ItemInfoEnvelope.TryParsePayload(input, 7937, out var parsed, out _));
        var envelope = Assert.IsType<Chinese7937ItemInfoEnvelope>(parsed);
        Array.Clear(input);
        Array.Clear(envelope.ToPayloadBytes());
        Array.Clear(envelope.UnknownFields[0].CopyEncodedBytes());
        Assert.Equal(expected, envelope.ToPayloadBytes());
        Assert.Equal(Convert.FromHexString("E00209"), envelope.UnknownFields[0].CopyEncodedBytes());
        Assert.Throws<NotSupportedException>(() => ((IList)envelope.UnknownFields).Clear());
        Assert.Equal(17u, envelope.KnownFields.ItemUid);
    }
}

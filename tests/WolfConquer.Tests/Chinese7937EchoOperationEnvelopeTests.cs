using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937EchoOperationEnvelopeTests
{
    // Independent ordered synthetic wire: all five extension wire forms, child
    // extensions interleaved with known fields, duplicates and nonminimal values.
    private const string Golden =
        "58810008084A0E1881000801251122334410000802610102030405060708100358024A021800" +
        "6A0300FF807308091B10011C747DAABBCCDD1000420300FF804A00";

    private static Chinese7937EchoOperationEnvelope Parse(string hex) => Parse(Convert.FromHexString(hex));

    private static Chinese7937EchoOperationEnvelope Parse(byte[] bytes)
    {
        Assert.True(Chinese7937EchoOperationEnvelope.TryParsePayload(bytes, 7937, out var parsed, out var failure), failure.ToString());
        Assert.Equal(Chinese7937EchoEnvelopeFailure.None, failure);
        return Assert.IsType<Chinese7937EchoOperationEnvelope>(parsed);
    }

    [Fact]
    public void OrderedGoldenRetainsParentAndChildExtensionsWithoutAssigningMeanings()
    {
        var envelope = Parse(Golden);
        var known = envelope.KnownProjection;
        Assert.Equal(8U, known.Operation);
        Assert.Equal(new Chinese7935XuanbaoFields(0), known.Fields);
        Assert.Equal(new byte[] { 0, 0xff, 0x80 }, known.CopyData8());
        Assert.Equal(new Chinese7935XuanbaoAccInfo[] { new(2, 0), default, default }, known.AccInfo.ToArray());
        Assert.Equal(new (int Field, int Wire, int? Child)[]
        {
            (11, 0, null), (3, 0, 0), (4, 5, 0), (12, 1, null),
            (11, 0, null), (3, 0, 1), (13, 2, null), (14, 3, null), (15, 5, null),
        }, envelope.UnknownFields.Select(field => (field.FieldNumber, field.WireType, field.ChildIndex)));
        Assert.Equal(new[]
        {
            "588100", "188100", "2511223344", "610102030405060708", "5802",
            "1800", "6A0300FF80", "7308091B10011C74", "7DAABBCCDD",
        }, envelope.UnknownFields.Select(field => Convert.ToHexString(field.CopyEncodedBytes())));
        Assert.Equal(Convert.FromHexString(Golden), envelope.ToPayloadBytes());
        Assert.Equal(Convert.FromHexString("08081000420300FF804A04080210004A004A00"), known.ToPayloadBytes());
    }

    [Fact]
    public void DuplicateKnownScalarsBytesAndChildScalarsKeepExistingLastValueSemantics()
    {
        const string original = "080A588000380128024201414A08080118001002080308880038002881004201424A00";
        var envelope = Parse(original);
        Assert.Equal(8U, envelope.KnownProjection.Operation);
        Assert.Equal(0U, envelope.KnownProjection.Fields.Field7);
        Assert.Equal(1U, envelope.KnownProjection.Fields.Field5);
        Assert.Equal(new byte[] { 0x42 }, envelope.KnownProjection.CopyData8());
        Assert.Equal(new Chinese7935XuanbaoAccInfo[] { new(3, 2), default }, envelope.KnownProjection.AccInfo.ToArray());
        Assert.Equal(Convert.FromHexString(original), envelope.ToPayloadBytes());
        Assert.Equal(Convert.FromHexString("0808280138004201424A04080310024A00"), envelope.KnownProjection.ToPayloadBytes());
    }

    [Fact]
    public void NonminimalTagsLengthsAndValuesKeepExactBytesInBothScopes()
    {
        const string original = "088800D8008000CA00870098008000088000";
        var envelope = Parse(original);
        Assert.Equal(8U, envelope.KnownProjection.Operation);
        Assert.Equal(new Chinese7935XuanbaoAccInfo[] { new(0, null) }, envelope.KnownProjection.AccInfo.ToArray());
        Assert.Equal(new[] { "D8008000", "98008000" },
            envelope.UnknownFields.Select(field => Convert.ToHexString(field.CopyEncodedBytes())));
        Assert.Equal(Convert.FromHexString(original), envelope.ToPayloadBytes());
        Assert.Equal(Convert.FromHexString("08084A020800"), envelope.KnownProjection.ToPayloadBytes());
    }

    [Theory]
    [InlineData("08085800", "080810005800")]
    [InlineData("08084A021800", "08084A0408001800")]
    public void AbsentAndPresentZeroRemainDifferentWithoutNativeDefaultInference(string absentHex, string zeroHex)
    {
        var absent = Parse(absentHex).KnownProjection;
        var zero = Parse(zeroHex).KnownProjection;
        if (absent.AccInfo.Length == 0)
        { Assert.Null(absent.Fields.Field2); Assert.Equal(0U, zero.Fields.Field2); }
        else
        { Assert.Null(absent.AccInfo[0].Field1); Assert.Equal(0U, zero.AccInfo[0].Field1); }
        Assert.NotEqual(absent.ToPayloadBytes(), zero.ToPayloadBytes());
    }

    [Fact]
    public void UnknownLengthDelimitedBytesAndNestedGroupsAreOpaqueEvenWhenTheyLookLikeKnownFields()
    {
        var envelope = Parse("080A6A0300FF807308091B10011C744A081B080110021C1800");
        Assert.Equal(10U, envelope.KnownProjection.Operation);
        Assert.Null(envelope.KnownProjection.Fields.Field2);
        Assert.Equal(new Chinese7935XuanbaoAccInfo[] { default }, envelope.KnownProjection.AccInfo.ToArray());
        Assert.Equal(new[] { 13, 14, 3, 3 }, envelope.UnknownFields.Select(field => field.FieldNumber));
        Assert.Equal(new int?[] { null, null, 0, 0 }, envelope.UnknownFields.Select(field => field.ChildIndex));
    }

    [Fact]
    public void CapturedSourceAndEveryExposedByteCopyHaveIndependentOwnership()
    {
        var original = Convert.FromHexString(Golden);
        var source = original.ToArray();
        var envelope = Parse(source);
        Array.Clear(source);
        var payload = envelope.ToPayloadBytes(); Array.Clear(payload);
        var opaque = envelope.UnknownFields[0].CopyEncodedBytes(); Array.Clear(opaque);
        var raw = envelope.KnownProjection.CopyData8()!; Array.Clear(raw);
        var children = envelope.KnownProjection.AccInfo.ToArray(); children[0] = new(99, 99);
        Assert.Equal(original, envelope.ToPayloadBytes());
        Assert.Equal(Convert.FromHexString("588100"), envelope.UnknownFields[0].CopyEncodedBytes());
        Assert.Equal(new byte[] { 0, 0xff, 0x80 }, envelope.KnownProjection.CopyData8());
        Assert.Equal(new Chinese7935XuanbaoAccInfo(2, 0), envelope.KnownProjection.AccInfo[0]);
    }

    [Theory]
    [InlineData("08085800")]
    [InlineData("08084A021800")]
    public void ExistingClosedConsumersRefuseExtensionsInsteadOfSilentlyDroppingThem(string hex)
    {
        var bytes = Convert.FromHexString(hex);
        Assert.False(Chinese7935XuanbaoOperationPayload.TryParseKnownPayload(bytes, out var generic));
        Assert.Null(generic);
        Assert.False(Chinese7937EchoResponse.TryParsePayload(bytes, 7937, out var response, out var failure));
        Assert.Null(response); Assert.Equal(Chinese7937EchoResponseFailure.KnownSchema, failure);
        Assert.Equal(bytes, Parse(bytes).ToPayloadBytes());
    }
}

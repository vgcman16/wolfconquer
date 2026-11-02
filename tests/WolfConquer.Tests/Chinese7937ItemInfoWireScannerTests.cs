using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class Chinese7937ItemInfoWireScannerTests
{
    [Fact]
    public void IndependentInterleavedLiteralRetainsExactCompleteRecordRanges()
    {
        var bytes = Convert.FromHexString(
            "0801E0028100E90201020304050607081002F202030800FFFB020801A50311223344FC021803850301020304");
        Assert.True(Chinese7937ItemInfoWireScanner.TryScan(bytes, out var ranges, out var failure), failure.ToString());
        Assert.Equal(Chinese7937ItemInfoEnvelopeFailure.None, failure);
        Assert.Equal(new[]
        {
            new Chinese7937ItemWireRange(1, 0, 0, 2), new Chinese7937ItemWireRange(44, 0, 2, 4),
            new Chinese7937ItemWireRange(45, 1, 6, 10), new Chinese7937ItemWireRange(2, 0, 16, 2),
            new Chinese7937ItemWireRange(46, 2, 18, 6), new Chinese7937ItemWireRange(47, 3, 24, 12),
            new Chinese7937ItemWireRange(3, 0, 36, 2), new Chinese7937ItemWireRange(48, 5, 38, 6),
        }, ranges);
        Assert.Equal(new[] { true, false, false, true, false, false, true, false }, ranges.Select(range => range.IsKnown));
        Assert.Equal(bytes, ranges.SelectMany(range => bytes.AsSpan(range.Offset, range.Length).ToArray()).ToArray());
    }

    [Fact]
    public void RepeatedUnknownNumbersAndNonminimalSpellingsRemainSeparateRecords()
    {
        var bytes = Convert.FromHexString("E082008000E00201E20200");
        Assert.True(Chinese7937ItemInfoWireScanner.TryScan(bytes, out var ranges, out var failure), failure.ToString());
        Assert.Equal(new[] { new Chinese7937ItemWireRange(44, 0, 0, 5),
            new Chinese7937ItemWireRange(44, 0, 5, 3), new Chinese7937ItemWireRange(44, 2, 8, 3) }, ranges);
        Assert.Equal(bytes, ranges.SelectMany(range => bytes.AsSpan(range.Offset, range.Length).ToArray()).ToArray());
    }

    [Theory]
    [InlineData("FB020801FB021002FC021803FC02")]
    [InlineData("FB020B10020CFC02")]
    public void NestedRequiredLookingTagsStayWhollyInsideTheirOpaqueGroup(string hex)
    {
        var bytes = Convert.FromHexString(hex);
        Assert.True(Chinese7937ItemInfoWireScanner.TryScan(bytes, out var ranges, out var failure), failure.ToString());
        Assert.Equal(new[] { new Chinese7937ItemWireRange(47, 3, 0, bytes.Length) }, ranges);
        Assert.False(ranges[0].IsKnown);
    }

    [Fact]
    public void LengthDelimitedContentIsNotRecursivelyInterpretedAsMessages()
    {
        var bytes = Convert.FromHexString("E2020300FF80");
        Assert.True(Chinese7937ItemInfoWireScanner.TryScan(bytes, out var ranges, out var failure), failure.ToString());
        Assert.Equal(new[] { new Chinese7937ItemWireRange(44, 2, 0, 6) }, ranges);
    }

    [Theory]
    [InlineData("0D01020304", 5)]
    [InlineData("0B0C", 3)]
    public void KnownWrongWireIsDelimitedForTheStrictKnownReaderToReject(string hex, int wire)
    {
        var bytes = Convert.FromHexString(hex);
        Assert.True(Chinese7937ItemInfoWireScanner.TryScan(bytes, out var ranges, out var failure), failure.ToString());
        Assert.Equal(new[] { new Chinese7937ItemWireRange(1, wire, 0, bytes.Length) }, ranges);
        Assert.True(ranges[0].IsKnown);
        Assert.False(Chinese7935ItemInfoView.TryParseKnownPayload(bytes, out var known));
        Assert.Null(known);
    }

    [Fact]
    public void UInt64AndMaximumFieldNumberIncludeTheirExactUpperEdges()
    {
        var bytes = Convert.FromHexString("E002FFFFFFFFFFFFFFFFFF01F8FFFFFF0F00");
        Assert.True(Chinese7937ItemInfoWireScanner.TryScan(bytes, out var ranges, out var failure), failure.ToString());
        Assert.Equal(new[] { new Chinese7937ItemWireRange(44, 0, 0, 12),
            new Chinese7937ItemWireRange(536870911, 0, 12, 6) }, ranges);
    }

    [Fact]
    public void EmptyStructuralInputHasNoRangesAndDoesNotSatisfyKnownRequiredFields()
    {
        Assert.True(Chinese7937ItemInfoWireScanner.TryScan([], out var ranges, out var failure));
        Assert.Empty(ranges); Assert.Equal(Chinese7937ItemInfoEnvelopeFailure.None, failure);
        Assert.False(Chinese7935ItemInfoView.TryParseKnownPayload([], out var known)); Assert.Null(known);
    }

    [Theory]
    [InlineData("00")]
    [InlineData("010000000000000000")]
    [InlineData("07")]
    [InlineData("08")]
    [InlineData("E0")]
    [InlineData("E082")]
    [InlineData("E602")]
    [InlineData("E702")]
    [InlineData("808080801000")]
    [InlineData("8080808080808080808000")]
    [InlineData("E00280")]
    [InlineData("E002FFFFFFFFFFFFFFFFFF02")]
    [InlineData("E00280808080808080808080")]
    [InlineData("E10200000000000000")]
    [InlineData("E502000000")]
    [InlineData("E20201")]
    [InlineData("E20280")]
    [InlineData("E202FFFFFFFFFFFFFFFFFF01")]
    [InlineData("FC02")]
    [InlineData("FB02")]
    [InlineData("FB020800")]
    [InlineData("FB02F402")]
    [InlineData("FB0200FC02")]
    [InlineData("FB0201FC02")]
    public void MalformedTagValueLengthFixedDataAndGroupsReturnNoPartialRanges(string hex) =>
        Refuses(Convert.FromHexString(hex), Chinese7937ItemInfoEnvelopeFailure.InvalidWire);

    [Fact]
    public void ValidPrefixBeforeMalformedTailNeverPublishesPartialRanges() =>
        Refuses(Convert.FromHexString("0801E00200E20203FF"), Chinese7937ItemInfoEnvelopeFailure.InvalidWire);

    [Fact]
    public void GroupDepthLimitIncludesSixteenAndRejectsSeventeenBeforeRecursing()
    {
        var edge = Groups(16);
        Assert.True(Chinese7937ItemInfoWireScanner.TryScan(edge, out var ranges, out var failure), failure.ToString());
        Assert.Equal(new[] { new Chinese7937ItemWireRange(47, 3, 0, edge.Length) }, ranges);
        Refuses(Groups(17), Chinese7937ItemInfoEnvelopeFailure.ExtensionBounds);
    }

    [Fact]
    public void TopLevelExtensionCountDoesNotCountKnownRecordsOrNestedUnknownRecords()
    {
        var extension = Convert.FromHexString("E00200");
        var edge = Enumerable.Range(0, 1024).SelectMany(_ => extension).ToArray();
        var known = Enumerable.Range(0, 2000).SelectMany(_ => new byte[] { 0x08, 0x00 });
        Assert.True(Chinese7937ItemInfoWireScanner.TryScan(known.Concat(edge).ToArray(), out var ranges, out var failure), failure.ToString());
        Assert.Equal(3024, ranges.Length);
        Refuses([.. edge, .. extension], Chinese7937ItemInfoEnvelopeFailure.ExtensionBounds);
        var nested = new byte[] { 0xfb, 0x02 }.Concat(edge).Concat(new byte[] { 0xfc, 0x02 }).ToArray();
        Assert.True(Chinese7937ItemInfoWireScanner.TryScan(nested, out ranges, out failure), failure.ToString());
        Assert.Equal(new[] { new Chinese7937ItemWireRange(47, 3, 0, nested.Length) }, ranges);
    }

    [Fact]
    public void WholePayloadBoundIsCheckedBeforeRecordsAndIncludesItsExactEdge()
    {
        // Independent field44/wire2 header: length65526 occupies3varint bytes.
        var edge = new byte[65531];
        new byte[] { 0xe2, 0x02, 0xf6, 0xff, 0x03 }.CopyTo(edge, 0);
        Assert.True(Chinese7937ItemInfoWireScanner.TryScan(edge, out var ranges, out var failure), failure.ToString());
        Assert.Equal(new[] { new Chinese7937ItemWireRange(44, 2, 0, edge.Length) }, ranges);
        Refuses(new byte[65532], Chinese7937ItemInfoEnvelopeFailure.InputBounds);
    }

    private static byte[] Groups(int depth) => Enumerable.Range(0, depth).SelectMany(_ => new byte[] { 0xfb, 0x02 })
        .Concat(Enumerable.Range(0, depth).SelectMany(_ => new byte[] { 0xfc, 0x02 })).ToArray();

    private static void Refuses(byte[] bytes, Chinese7937ItemInfoEnvelopeFailure expected)
    {
        Assert.False(Chinese7937ItemInfoWireScanner.TryScan(bytes, out var ranges, out var failure));
        Assert.Empty(ranges);
        Assert.Equal(expected, failure);
    }
}

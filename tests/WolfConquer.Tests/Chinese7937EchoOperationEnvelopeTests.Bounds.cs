using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937EchoOperationEnvelopeTests
{
    [Fact]
    public void GlobalOpaqueRecordBudgetIncludesTheParentAndEveryChildScope()
    {
        byte[] child = [0x4a, 0x02, 0x18, 0x00];
        var edge = new byte[] { 0x08, 0x08, 0x58, 0x00 }
            .Concat(Enumerable.Range(0, 1023).SelectMany(_ => child)).ToArray();
        var envelope = Parse(edge);
        Assert.Equal(1024, envelope.UnknownFields.Count);
        Assert.Equal(1023, envelope.KnownProjection.AccInfo.Length);
        Assert.Equal(edge, envelope.ToPayloadBytes());
        Refuses([.. edge, .. child], Chinese7937EchoEnvelopeFailure.ExtensionBounds);
    }

    [Fact]
    public void EachChildBudgetAndParentBudgetRefuseWithoutPublishingPartialData()
    {
        var parent = Enumerable.Range(0, 1024).SelectMany(_ => new byte[] { 0x58, 0 }).ToArray();
        Assert.Equal(1024, Parse([0x08, 0x08, .. parent]).UnknownFields.Count);
        Refuses([0x08, 0x08, .. parent, 0x58, 0], Chinese7937EchoEnvelopeFailure.ExtensionBounds);
        // Independent child9 length2048 = 0x800 -> varint80 10.
        var child = Enumerable.Range(0, 1024).SelectMany(_ => new byte[] { 0x18, 0 }).ToArray();
        Assert.Equal(1024, Parse([0x08, 0x08, 0x4a, 0x80, 0x10, .. child]).UnknownFields.Count);
        Refuses([0x08, 0x08, 0x4a, 0x82, 0x10, .. child, 0x18, 0], Chinese7937EchoEnvelopeFailure.ExtensionBounds);
    }

    [Fact]
    public void KnownEmptyChildrenPreserveTheExisting1024BoundAndAppendOrder()
    {
        var children = Enumerable.Range(0, 1024).SelectMany(_ => new byte[] { 0x4a, 0 }).ToArray();
        var envelope = Parse([0x08, 0x08, .. children]);
        Assert.Equal(1024, envelope.KnownProjection.AccInfo.Length);
        Assert.Empty(envelope.UnknownFields);
        Refuses([0x08, 0x08, .. children, 0x4a, 0], Chinese7937EchoEnvelopeFailure.KnownSchema);
    }

    [Fact]
    public void OpaqueGroupsUseExistingDepthLimitInBothScopesWithoutCountingNestedRecords()
    {
        var groups = Groups(16);
        var parent = Parse([0x08, 0x08, .. groups]);
        Assert.Single(parent.UnknownFields);
        Refuses([0x08, 0x08, .. Groups(17)], Chinese7937EchoEnvelopeFailure.ExtensionBounds);
        Assert.Single(Parse([0x08, 0x08, 0x4a, 0x20, .. groups]).UnknownFields);
        Refuses([0x08, 0x08, 0x4a, 0x22, .. Groups(17)], Chinese7937EchoEnvelopeFailure.ExtensionBounds);
    }

    [Fact]
    public void PayloadLimitAcceptsExact65531BytesAndRetainsUnknownRawData()
    {
        // Independent header: op8 plus field11/bytes length65525 (F5 FF 03).
        var bytes = new byte[65531];
        new byte[] { 0x08, 0x08, 0x5a, 0xf5, 0xff, 0x03 }.CopyTo(bytes, 0);
        var envelope = Parse(bytes);
        Assert.Single(envelope.UnknownFields);
        Assert.Equal(bytes, envelope.ToPayloadBytes());
        Assert.Equal(65535, envelope.ToFrameBytes().Length);
        Assert.Equal(new byte[] { 0xff, 0xff, 0x54, 0x08 }, envelope.ToFrameBytes()[..4]);
    }

    [Fact]
    public void ClosedStructuralScopesKeepItem43Echo10AndChild2Distinct()
    {
        Assert.Equal(Chinese7937ItemInfoWireScanner.MaximumPayloadBytes, Chinese7937ProtoWireScanner.MaximumPayloadBytes);
        Assert.Equal(Chinese7937ItemInfoWireScanner.MaximumExtensions, Chinese7937ProtoWireScanner.MaximumExtensions);
        Assert.Equal(Chinese7937ItemInfoWireScanner.MaximumGroupDepth, Chinese7937ProtoWireScanner.MaximumGroupDepth);
        var bytes = Convert.FromHexString("1800");
        Assert.True(Chinese7937ProtoWireScanner.TryScan(bytes, Chinese7937ProtoScope.EchoChild2132, out var child, out var failure));
        Assert.Equal(Chinese7937ProtoFailure.None, failure);
        Assert.Equal(new Chinese7937ProtoRange(3, 0, 0, 2, -1, 0), Assert.Single(child));
        Assert.True(Chinese7937ItemInfoWireScanner.TryScan(bytes, out var item, out var oldFailure));
        Assert.Equal(Chinese7937ItemInfoEnvelopeFailure.None, oldFailure);
        Assert.True(Assert.Single(item).IsKnown);
        Assert.False(Chinese7937ProtoWireScanner.TryScan(bytes, (Chinese7937ProtoScope)99, out var invalid, out failure));
        Assert.Empty(invalid); Assert.Equal(Chinese7937ProtoFailure.InvalidWire, failure);
    }

    private static byte[] Groups(int depth) =>
        Enumerable.Repeat((byte)0x73, depth).Concat(Enumerable.Repeat((byte)0x74, depth)).ToArray();
}

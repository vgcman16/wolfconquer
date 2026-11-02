using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937EchoRuntimeCatalogTests
{
    [Fact]
    public void CatalogBackedSelectionZeroMatchesIndependentObservedFrame()
    {
        var catalog = Create();
        Assert.True(catalog.TryPrepareActivationRequest(0, out var prepared));
        Assert.Equal(Convert.FromHexString("0800540808062001"), prepared!.ToFrameBytes());
        var payload = prepared.CreatePayload();
        Assert.Equal(6u, payload.Operation);
        Assert.Equal(1u, payload.Fields.Field4);
        Assert.Null(payload.Fields.Field2);
        Assert.Null(payload.Fields.Field3);
        Assert.Null(payload.Fields.Field5);
        Assert.Null(payload.Fields.Field6);
        Assert.Null(payload.Fields.Field7);
        Assert.Null(payload.Fields.Field10);
        Assert.False(payload.HasData8);
        Assert.Empty(payload.AccInfo.ToArray());
        Assert.Equal(100u, prepared.Cost.MoneyField);
        Assert.Equal(2u, prepared.Cost.ItemNumberField);
        Assert.Equal(42u, prepared.Item.ItemType);
    }

    [Fact]
    public void LastCatalogSelectionWorksAndCostKeysBeyondMaximumRemainOnlyData()
    {
        var catalog = Create();
        Assert.True(catalog.TryPrepareActivationRequest(4, out var request));
        Assert.Equal(5u, request!.Cost.HoleNumber);
        Assert.Equal(500u, request.Cost.MoneyField);
        Assert.Equal(Convert.FromHexString("0800540808062005"), request.ToFrameBytes());
        Assert.True(catalog.Requirements.TryGetCost(7, out var retained));
        Assert.Equal(7u, retained.HoleNumber);
        foreach (var selection in new uint[] { 5, 6, 7, uint.MaxValue })
        {
            Assert.False(catalog.TryPrepareActivationRequest(selection, out var rejected));
            Assert.Null(rejected);
        }
    }

    [Fact]
    public void FreshFrameCopiesAndRealDecoderKeepRequestPresenceAndCatalogUnchanged()
    {
        var catalog = Create();
        Assert.True(catalog.TryPrepareActivationRequest(0, out var request));
        var first = request!.ToFrameBytes();
        var second = request.ToFrameBytes();
        Assert.NotSame(first, second);
        Array.Clear(first);
        Assert.Equal(Convert.FromHexString("0800540808062001"), second);
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(second), out var frame, out _, out _));
        Assert.True(Chinese7935XuanbaoOperationPayload.TryParseKnownFrame(frame, out var parsed));
        Assert.Equal(1u, parsed!.Fields.Field4);
        Assert.Null(parsed.Fields.Field10);
        Assert.Equal(100u, catalog.Requirements.CostRows[0].MoneyField);
    }
}

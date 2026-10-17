using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyVendorTransportAdapterTests
{
    [Theory]
    [InlineData(21, MsgItemUsageAction.ShowVendingList)]
    [InlineData(22, MsgItemUsageAction.AddVendingItemGold)]
    [InlineData(23, MsgItemUsageAction.RemoveVendingItem)]
    [InlineData(24, MsgItemUsageAction.BuyVendingItem)]
    [InlineData(29, MsgItemUsageAction.AddVendingItemConquerPoints)]
    public void D13VendorActionsUseDecompiledFixedLayoutValues(int expected, MsgItemUsageAction action)
    {
        Assert.Equal(expected, (ushort)action);
    }

    [Fact]
    public void D13AdapterPreservesFullAddPriceAndExactEcho()
    {
        var packet = new MsgItemUsagePacket(
            Id: 90_001,
            DwParam: 4_000_000_000,
            MsgItemUsageAction.AddVendingItemGold,
            Timestamp: 1_234,
            DwParam2: 2,
            DwParam3: 3,
            DwParam4: 4);

        Assert.True(LegacyVendorTransportAdapters.TryAdaptVisibleDx8D13(
            packet,
            out var request,
            out var responses,
            out var reason), reason);
        Assert.Equal(LegacyVendorRequestAction.AddListingGold, request.Action);
        Assert.Equal(packet.Id, request.ItemUid);
        Assert.Equal(packet.DwParam, request.Price);
        Assert.Null(request.BoothUid);
        Assert.Equal(packet, ParseUsage(responses.CreateAcceptedResponse()));
    }

    [Fact]
    public void D13BuyAdapterUsesDwParamAsBoothAndBuildsSellerRemoveEcho()
    {
        var packet = new MsgItemUsagePacket(
            Id: 90_002,
            DwParam: D13VendorTestHarness.SellerBoothUid,
            MsgItemUsageAction.BuyVendingItem,
            Timestamp: 2_345,
            DwParam2: 5,
            DwParam3: 6,
            DwParam4: 7);

        Assert.True(LegacyVendorTransportAdapters.TryAdaptVisibleDx8D13(
            packet,
            out var request,
            out var responses,
            out var reason), reason);
        Assert.Equal(LegacyVendorRequestAction.BuyListing, request.Action);
        Assert.Equal(D13VendorTestHarness.SellerBoothUid, request.BoothUid);
        Assert.Equal(packet, ParseUsage(responses.CreateAcceptedResponse()));
        Assert.Equal(
            packet with { Action = MsgItemUsageAction.RemoveVendingItem },
            ParseUsage(responses.CreateSellerRemovalResponse()));
    }

    [Fact]
    public void RootD11ConquerPointAdapterNormalizesFieldThreeIntoSharedPrice()
    {
        var packet = MsgItemOperationPacket.WithSecondaryValue(
            itemUid: 90_003,
            secondaryValue: 2_000_000_000,
            MsgItemOperationAction.AddVendingItemConquerPoints,
            timestampToken: 3_456);

        Assert.True(LegacyVendorTransportAdapters.TryAdaptRootD11(
            packet,
            out var request,
            out var responses,
            out var reason), reason);
        Assert.Equal(LegacyVendorRequestAction.AddListingConquerPoints, request.Action);
        Assert.Equal(2_000_000_000UL, request.Price);
        Assert.Null(request.BoothUid);
        Assert.True(MsgItemOperationPacket.TryParse(
            D13VendorTestHarness.ReadFrame(responses.CreateAcceptedResponse()),
            out var echoed));
        Assert.Equal(packet, echoed);
    }

    private static MsgItemUsagePacket ParseUsage(byte[] bytes)
    {
        Assert.True(MsgItemUsagePacket.TryParse(D13VendorTestHarness.ReadFrame(bytes), out var packet));
        return packet;
    }
}

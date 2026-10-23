using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDragonSkinTransactionTests
{
    [Theory]
    [InlineData("overflow")]
    [InlineData("malformed")]
    [InlineData("missing-field")]
    [InlineData("foreign-id")]
    [InlineData("null-row")]
    [InlineData("null-list")]
    public Task PresentInvalidRetrieveMetadataCannotConsumeEntitlementOrPublishAnItem(string fault) => RunAsync(async f =>
    {
        f.Seed("1/1/1/");
        f.Catalog = LegacyItemWorldDataFixture.Create(Metadata(Mapped(1)));
        var rows = Assert.IsType<Dictionary<int, IReadOnlyList<ItemType>>>(f.Catalog.ItemsById);
        rows[(int)Mapped(1)] = fault switch
        {
            "overflow" => [Metadata(Mapped(1), "65536")],
            "malformed" => [Metadata(Mapped(1), "invalid")],
            "missing-field" => [Metadata(Mapped(1)) with { Fields = [] }],
            "foreign-id" => [Metadata(Mapped(2))],
            "null-row" => [null!],
            "null-list" => null!,
            _ => throw new ArgumentOutOfRangeException(nameof(fault)),
        };
        var before = Capture(f.Current);
        await f.InvokeAsync(LegacyDragonSkinType.Retrieve);
        AssertRejected(f, before, "dragonSkinItemMetadataInvalid");
    });

    [Fact]
    public Task ActualCheckedCurrentUidExhaustionIsARefusalNotAnEntitlementDebit() => RunAsync(async f =>
    {
        f.Seed("1/1/1/", [Item(uint.MaxValue, 720_001)]);
        var before = Capture(f.Current);
        await f.InvokeAsync(LegacyDragonSkinType.Retrieve);
        AssertRejected(f, before, "dragonSkinPreparation:OverflowException");
        Assert.Equal(uint.MaxValue, Assert.Single(f.Current.PlayerItems.InventoryItems).Uid);
    });

    [Fact]
    public Task RealItemFrameProjectionFaultOccursBeforeCostEntitlementOrFileEffects() => RunAsync(async f =>
    {
        f.Seed("0/0/", [new PlayerItemSnapshotItem { Uid = 10, ItemId = Mapped(1), StackSize = 2, Effect = 256 }]);
        var before = Capture(f.Current);
        await f.InvokeAsync(LegacyDragonSkinType.Unlock);
        AssertRejected(f, before, "dragonSkinPreparation:ArgumentOutOfRangeException");
        Assert.Equal((ushort)2, Assert.Single(f.Current.PlayerItems.InventoryItems).StackSize);
    });

    [Theory]
    [InlineData(2, "character")]
    [InlineData(2, "items")]
    [InlineData(3, "character")]
    [InlineData(3, "items")]
    public Task RealExclusiveGameFileLockRejectsUnchangedAndSameClaimCanRetryAfterRelease(int type, string target) => RunAsync(async f =>
    {
        f.Seed(type == 2 ? "0/0/" : "1/1/1/", [Item(9, 720_001), Item(10, Mapped(1))]);
        var before = Capture(f.Current);
        using (var actualBlocker = new FileStream(target == "character" ? f.CharacterPath : f.ItemPath,
            FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await f.InvokeAsync((LegacyDragonSkinType)type);
        AssertRejected(f, before, "dragonSkinPreparation:IOException");
        Assert.False(f.Recovery.IsBlocked);
        f.Log.GetStringBuilder().Clear(); f.Trace.Clear();
        // No reset/reseed of entitlement, session or recovery between attempts.
        await f.InvokeAsync((LegacyDragonSkinType)type);
        AssertCommitted(f, before, type == 2 ? "0/1/1/" : "0/0/");
        AssertWireAndLogOrder(f, (LegacyDragonSkinType)type, 1, type == 2
            ? new MsgItemUsagePacket(10, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes()
            : ItemGolden(11, Mapped(1), 1, 1));
    });

    [Theory]
    [InlineData("bound-only")]
    [InlineData("zero-unbound")]
    [InlineData("bound-and-zero")]
    public Task BoundOrEmptyMappedRowsNeverAuthorizeAnUnboundSkinExchange(string kind) => RunAsync(async f =>
    {
        PlayerItemSnapshotItem[] rows = kind switch
        {
            "bound-only" => new[] { Item(10, Mapped(1), 2, 1) },
            "zero-unbound" => [Item(10, Mapped(1), 0)],
            _ => [Item(10, Mapped(1), 2, 1), Item(11, Mapped(1), 0)],
        };
        f.Seed("0/0/", rows);
        var before = Capture(f.Current);
        await f.InvokeAsync(LegacyDragonSkinType.Unlock);
        Assert.Same(before.Session, f.Current); AssertModelUnchanged(before); AssertFilesUnchanged(f);
        Assert.Equal(SkinGolden(LegacyDragonSkinType.Unlock, 1, false), Assert.Single(f.Sent));
        Assert.DoesNotContain("WorldServer persisted LegacyDragonSkin", f.Log.ToString());
    });
}

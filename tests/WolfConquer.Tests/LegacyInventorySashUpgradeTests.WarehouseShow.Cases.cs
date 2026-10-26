using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(LegacyWarehouseAction.Show)]
    [InlineData(LegacyWarehouseAction.Show_WH_House)]
    [InlineData(LegacyWarehouseAction.ShowInventorySash)]
    [InlineData(LegacyWarehouseAction.ShashShow)]
    public Task RegisteredWarehouseShowPreservesExactRichFramesWithoutEconomyOwnersOrAnyFileWrite(
        LegacyWarehouseAction action) => RunAsync(async fixture =>
    {
        fixture.SeedWarehouseShow(action);
        fixture.SetContext(fixture.Context with { Trades = null, TradeOperations = null, TradeRecovery = null });
        var before = fixture.Capture();
        var expected = ExpectedWarehouseShowFrames(before.Owner, action);
        var observed = 0;
        fixture.SendObserver = frame =>
        {
            AssertWarehouseShowReadOnly(fixture, before);
            Assert.Equal(expected[observed++], frame);
        };

        await fixture.InvokeWarehouseShowAsync(action);

        Assert.Equal(action == LegacyWarehouseAction.ShashShow ? 2 : 9, observed);
        AssertWarehouseShowReadOnly(fixture, before);
        AssertWarehouseShowFrames(fixture, before, action);
        // Inventory sash has five stored roots but capacity two: read-only display
        // must remain available for safe recovery from already-overfull storage.
    });

    [Theory]
    [InlineData(LegacyWarehouseAction.Show, 666U)]
    [InlineData(LegacyWarehouseAction.Show_WH_House, 666U)]
    [InlineData(LegacyWarehouseAction.ShowInventorySash, ShowNpc)]
    public Task RegisteredWarehouseShowRejectsUnauthorizedTargetEvenWhenItsRowsAreLoaded(
        LegacyWarehouseAction action, uint target) => RunAsync(async fixture =>
    {
        fixture.SeedWarehouseShow(action, target);
        Assert.Equal(5, fixture.Current.PlayerItems.WarehouseItems.Count(row => row.WarehouseId == target));
        var before = fixture.Capture();

        await fixture.InvokeWarehouseShowAsync(action, target);

        fixture.AssertUnchanged(before);
    });

    [Fact]
    public Task RegisteredWarehouseShowUsesActualCurrentStoreInsteadOfStaleContext() => RunAsync(async fixture =>
    {
        fixture.SeedWarehouseShow(LegacyWarehouseAction.Show);
        var retired = fixture.Current;
        var retiredJson = JsonSerializer.Serialize(retired);
        fixture.SaveTransferSetup(retired.PlayerItems with
        {
            WarehouseItems = retired.PlayerItems.WarehouseItems.Select(row =>
                row.WarehouseId == ShowNpc ? row.WithStackSize(9) : row)
                .OrderBy(row => row.WarehouseId).ThenBy(row => row.Uid).ToArray(),
        }, keepStaleContext: true);
        fixture.PublishWarehouseShowInputOrder(LegacyWarehouseAction.Show, keepStaleContext: true);
        Assert.Same(retired, fixture.Context.Session);
        Assert.NotSame(retired, fixture.Current);
        var before = fixture.Capture();
        fixture.SendObserver = _ => AssertWarehouseShowReadOnly(fixture, before);

        await fixture.InvokeWarehouseShowAsync(LegacyWarehouseAction.Show);

        AssertWarehouseShowReadOnly(fixture, before);
        AssertWarehouseShowFrames(fixture, before, LegacyWarehouseAction.Show);
        Assert.Equal(retiredJson, JsonSerializer.Serialize(retired));
    });

    [Fact]
    public Task RegisteredPhysicalSashShowNeedsActualStoreNotOnlyCapturedSession() => RunAsync(async fixture =>
    {
        fixture.SeedWarehouseShow(LegacyWarehouseAction.ShashShow);
        var before = fixture.Capture();
        fixture.SetContext(fixture.Context with { Sessions = null });

        await fixture.InvokeWarehouseShowAsync(LegacyWarehouseAction.ShashShow);

        fixture.AssertUnchanged(before);
    });

    [Fact]
    public Task RegisteredPhysicalSashShowRejectsMalformedForeignTailBeforePublishing() => RunAsync(async fixture =>
    {
        fixture.SeedWarehouseShow(LegacyWarehouseAction.ShashShow);
        var owner = fixture.Current;
        fixture.Sessions.Set(fixture.Connection, owner with
        {
            PlayerItems = owner.PlayerItems with
            { WarehouseItems = owner.PlayerItems.WarehouseItems.Concat(new PlayerItemSnapshotItem[] { null! }).ToArray() },
        });
        var before = fixture.Capture();

        await fixture.InvokeWarehouseShowAsync(LegacyWarehouseAction.ShashShow);

        fixture.AssertUnchanged(before);
    });

    [Fact]
    public Task RegisteredWarehouseShowPreparesLateWireInvalidRowBeforeSendingAnyEarlierChunk() => RunAsync(async fixture =>
    {
        fixture.SeedWarehouseShow(LegacyWarehouseAction.Show);
        var late = new PlayerItemSnapshotItem
        { Uid = 24, ItemId = 1_088_001, StackSize = 1, WarehouseId = ShowNpc, Effect = 65_536 };
        fixture.SaveTransferSetup(fixture.Current.PlayerItems with
        {
            WarehouseItems = fixture.Current.PlayerItems.WarehouseItems.Select(row => row.Uid == 24 ? late : row)
                .OrderBy(row => row.WarehouseId).ThenBy(row => row.Uid).ToArray(),
        });
        fixture.PublishWarehouseShowInputOrder(LegacyWarehouseAction.Show);
        // Effect is uint in the save codec, but the warehouse packet also encodes
        // a UInt16 copy. The fifth selected row fails only during wire preparation.
        Assert.True(LegacyItemSaveGraph.TryCapture(fixture.Current.PlayerItems, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LegacyWarehousePacket(ShowNpc, LegacyWarehouseAction.Show, 0, 60, [late]).ToFrameBytes());
        var before = fixture.Capture();

        await fixture.InvokeWarehouseShowAsync(LegacyWarehouseAction.Show);

        fixture.AssertUnchanged(before);
    });

    [Fact]
    public Task RegisteredInventorySashShowRejectsPresentMalformedCapacityWithoutPublishing() => RunAsync(async fixture =>
    {
        fixture.SeedWarehouseShow(LegacyWarehouseAction.ShowInventorySash);
        fixture.SetWarehouseFamilyCount("not-a-count");
        var before = fixture.Capture();

        await fixture.InvokeWarehouseShowAsync(LegacyWarehouseAction.ShowInventorySash);

        fixture.AssertUnchanged(before);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RegisteredWarehouseShowStopsRemainingChunksAfterCurrentOwnerReplacementOrEndedMarker(
        bool ended) => RunAsync(async fixture =>
    {
        fixture.SeedWarehouseShow(LegacyWarehouseAction.Show);
        var before = fixture.Capture();
        var first = ExpectedWarehouseShowFrames(before.Owner, LegacyWarehouseAction.Show)[0];
        WorldSession? replacement = null;
        var observed = 0;
        fixture.SendObserver = frame =>
        {
            AssertWarehouseShowReadOnly(fixture, before);
            Assert.Equal(first, frame);
            Assert.Equal(1, ++observed);
            if (ended)
            {
                lock (fixture.Trades.SyncRoot) fixture.Trades.MarkConnectionEnded(fixture.Connection);
            }
            else
            {
                replacement = before.Owner with { };
                fixture.Sessions.Set(fixture.Connection, replacement);
            }
        };

        await fixture.InvokeWarehouseShowAsync(LegacyWarehouseAction.Show);

        Assert.Equal(1, observed);
        Assert.Equal(first, Assert.Single(fixture.Sent.Skip(before.FrameCount)));
        Assert.Same(ended ? before.Owner : replacement, fixture.Current);
        Assert.Equal(before.Json, JsonSerializer.Serialize(fixture.Current));
        fixture.AssertFiles(before.Files);
        if (ended)
        {
            fixture.SendObserver = null;
            var after = fixture.Capture();
            await fixture.InvokeWarehouseShowAsync(LegacyWarehouseAction.Show);
            fixture.AssertUnchanged(after);
        }
    });

    [Fact]
    public Task RegisteredPhysicalSashShowStopsAggregateAfterCurrentOwnerReplacement() => RunAsync(async fixture =>
    {
        fixture.SeedWarehouseShow(LegacyWarehouseAction.ShashShow);
        var before = fixture.Capture();
        var first = ExpectedWarehouseShowFrames(before.Owner, LegacyWarehouseAction.ShashShow)[0];
        var replacement = before.Owner with { };
        var observed = 0;
        fixture.SendObserver = frame =>
        {
            AssertWarehouseShowReadOnly(fixture, before);
            Assert.Equal(first, frame);
            Assert.Equal(1, ++observed);
            fixture.Sessions.Set(fixture.Connection, replacement);
        };

        await fixture.InvokeWarehouseShowAsync(LegacyWarehouseAction.ShashShow);

        Assert.Equal(1, observed);
        Assert.Equal(first, Assert.Single(fixture.Sent.Skip(before.FrameCount)));
        Assert.Same(replacement, fixture.Current);
        Assert.Equal(before.Json, JsonSerializer.Serialize(fixture.Current));
        fixture.AssertFiles(before.Files);
    });
}

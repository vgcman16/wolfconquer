using WolfConquer.Core.World;
using GoodsEx = WolfConquer.Core.Data.LegacyNativeExchangeShopGoodsEx;
using Shop = WolfConquer.Core.World.LegacyNativeExchangeShopRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    [Theory]
    [InlineData(17, 1)] [InlineData(17, 3)] [InlineData(17, 9)]
    [InlineData(71, 1)] [InlineData(71, 3)] [InlineData(71, 9)]
    public Task RefreshUsesBoundedPartialShuffleAndPreservesUnrelatedRealRows(int seed, int count)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope();
        ReadyRefreshPrefix(scope);
        ExCatalog(scope.State, 168, 1001, 9);
        var displaced = new Shop.Item { shopid = 168, goods_rec_id = 1001, times = 77 };
        scope.Shop.Items.Add(1001, displaced);
        var before = CaptureRefresh(scope);
        var random = new LegacyNativeSafeRandom(seed);
        LegacyNativePoolRandom.GetRandom = random;
        before = before with { Random = random };
        var expectedRandom = new Random(seed);
        uint[] expected = Enumerable.Range(1001, 9).Select(id => (uint)id).ToArray();
        for (int index = 0; index < count; index++)
        {
            int selected = expectedRandom.Next(index, expected.Length);
            (expected[index], expected[selected]) = (expected[selected], expected[index]);
        }

        Assert.True(scope.Shop.TryGetRandomEx(168, (uint)count, out var reason), reason);
        Assert.NotSame(before.Items, scope.Shop.Items);
        Assert.Equal(new uint[] { 17, 19 }.Concat(expected.Take(count)), scope.Shop.Items.Keys);
        Assert.Same(before.Entries![0].Value, scope.Shop.Items[17]);
        Assert.Same(before.Entries[1].Value, scope.Shop.Items[19]);
        foreach (uint id in expected.Take(count)) ItemIs(scope.Shop.Items[id], 168, id, 0);
        Assert.Equal(count, scope.Shop.Items.Values.Count(row => row.shopid == 168));
        AssertDetachedRefreshStorageUnchanged(before);
        AssertRefreshSurroundingsUnchanged(scope, before);
        Assert.Equal(expectedRandom.Next(), random.Next()); // Exactly count, never retry-until-unique draws.
    });

    [Fact]
    public Task RefreshValidZeroCountRetiresOnlyTargetCatalogIdsAtomicallyWithoutRng()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        ExCatalog(scope.State, 168, 1001, 3);
        var oldTarget = new Shop.Item { shopid = 168, goods_rec_id = 1002, times = 99 };
        scope.Shop.Items.Add(1002, oldTarget);
        var priorRandom = LegacyNativePoolRandom.GetRandom;
        LegacyNativePoolRandom.GetRandom = null!;
        try
        {
            var before = CaptureRefresh(scope);
            Assert.True(scope.Shop.TryGetRandomEx(168, 0, out var reason), reason);
            Assert.NotSame(before.Items, scope.Shop.Items);
            Assert.Equal(new uint[] { 17, 19 }, scope.Shop.Items.Keys);
            Assert.Same(before.Entries![0].Value, scope.Shop.Items[17]);
            Assert.Same(before.Entries[1].Value, scope.Shop.Items[19]);
            AssertDetachedRefreshStorageUnchanged(before);
            AssertRefreshSurroundingsUnchanged(scope, before);
        }
        finally { LegacyNativePoolRandom.GetRandom = priorRandom; }
    });

    [Fact]
    public Task RefreshEmptyValidCatalogWithZeroCountIsAnUnchangedNoRngSuccess()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        var priorRandom = LegacyNativePoolRandom.GetRandom;
        LegacyNativePoolRandom.GetRandom = null!;
        try
        {
            var before = CaptureRefresh(scope);
            Assert.True(scope.Shop.TryGetRandomEx(168, 0, out var reason), reason);
            AssertRefreshRejected(scope, before); // Same state; this is a successful no-op, not refusal.
        }
        finally { LegacyNativePoolRandom.GetRandom = priorRandom; }
    });

    [Theory]
    [InlineData("missing-catalog")] [InlineData("aliased-catalog")]
    [InlineData("null-book-tail")] [InlineData("mismatched-book")]
    public Task RefreshZeroCountStillRequiresCompleteValidPhysicalOwners(string malformed)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        ExCatalog(scope.State, 168, 1001, 2);
        if (malformed == "missing-catalog") GoodsEx.exchange_shop_goods_ex = null!;
        else if (malformed == "aliased-catalog") scope.State.ExRows.Add(2001, scope.State.ExRows[1001]);
        else if (malformed == "null-book-tail") scope.Shop.Items.Add(2001, null!);
        else scope.Shop.Items[19].goods_rec_id = 2001;
        var before = CaptureRefresh(scope);
        Assert.False(scope.Shop.TryGetRandomEx(168, 0, out var reason));
        Assert.StartsWith("exchangeShop", reason);
        AssertRefreshRejected(scope, before);
    });

    [Fact]
    public Task RefreshResetPublishesExactlyEightNewRowsOnlyAfterCompletePreparation()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        ExCatalog(scope.State, 168, 1001, 8);
        var before = CaptureRefresh(scope);
        Assert.True(scope.Shop.TryReset(out var reason), reason);
        Assert.NotSame(before.Items, scope.Shop.Items);
        Assert.Equal(8, scope.Shop.Items.Count);
        Assert.Equal(Enumerable.Range(1001, 8).Select(id => (uint)id), scope.Shop.Items.Keys.OrderBy(id => id));
        Assert.All(scope.Shop.Items.Values, row => ItemIs(row, 168, row.goods_rec_id, 0));
        Assert.DoesNotContain(17u, scope.Shop.Items.Keys);
        Assert.DoesNotContain(19u, scope.Shop.Items.Keys);
        AssertDetachedRefreshStorageUnchanged(before);
        AssertRefreshSurroundingsUnchanged(scope, before);
    });

    [Fact]
    public Task RefreshOpenKeepsExact2317LayoutPhysicalGoodsOrderAndZeroUseCounts()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope();
        ExCatalog(scope.State, 168, 1001, 8);
        Assert.True(scope.Shop.TryReset(out var reason), reason);
        var items = scope.Shop.Items.Values.Select(row => (row.goods_rec_id, row.times)).ToArray();
        Assert.Empty(scope.Queue); // Refresh alone does not invent a success packet.
        DateTime before = DateTime.Now;
        scope.Shop.Open(scope.Packet, 77, 168);
        DateTime after = DateTime.Now;
        scope.Sent(77, 168, before, after, items);
    });

    [Fact]
    public Task RefreshFiniteSeedSetAlwaysReturnsDistinctPhysicalRowsAndReachesEveryCandidate()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope();
        ExCatalog(scope.State, 168, 1001, 9);
        var observed = new HashSet<uint>();
        for (int seed = 1; seed <= 32; seed++)
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(seed);
            Assert.True(scope.Shop.TryGetRandomEx(168, 3, out var reason), reason);
            Assert.Equal(3, scope.Shop.Items.Count);
            Assert.Equal(3, scope.Shop.Items.Values.Distinct(ReferenceEqualityComparer.Instance).Count());
            foreach (var pair in scope.Shop.Items)
            {
                Assert.Equal(pair.Key, pair.Value.goods_rec_id);
                ItemIs(pair.Value, 168, pair.Key, 0);
                Assert.Contains(pair.Key, scope.State.ExRows.Keys);
                observed.Add(pair.Key);
            }
        }
        Assert.Equal(scope.State.ExRows.Keys.OrderBy(id => id), observed.OrderBy(id => id));
        Assert.Empty(scope.Queue); // Coverage control, not a statistical uniformity proof.
    });

    [Theory]
    [InlineData(0u, 0u)] [InlineData(uint.MaxValue, uint.MaxValue)]
    public Task RefreshAllUInt32PhysicalIdentifiersRemainRepresentable(uint shop, uint goods)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope();
        scope.State.Metadata(true, goods, goods, shop, amount: 0);
        scope.State.ExRows[goods].itemtypeid = goods == 0 ? uint.MaxValue : 0;
        Assert.True(scope.Shop.TryGetRandomEx(shop, 1, out var reason), reason);
        var row = Assert.Single(scope.Shop.Items);
        Assert.Equal(goods, row.Key); ItemIs(row.Value, shop, goods, 0);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task RefreshLoadingPhysical168GateDoesNotRequireOptionalCatalogOrRng()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        scope.Shop.Items.Add(168, new() { shopid = 999, goods_rec_id = 168, times = 7 });
        var priorRandom = LegacyNativePoolRandom.GetRandom;
        GoodsEx.exchange_shop_goods_ex = null!; LegacyNativePoolRandom.GetRandom = null!;
        try
        {
            var before = CaptureRefresh(scope);
            Assert.True(scope.Shop.TryLoading(out var reason), reason);
            AssertRefreshRejected(scope, before);
        }
        finally { LegacyNativePoolRandom.GetRandom = priorRandom; }
    });
}

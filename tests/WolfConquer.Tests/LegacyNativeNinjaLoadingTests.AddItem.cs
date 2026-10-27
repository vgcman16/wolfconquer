using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using ItemAction = WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaItem.TypeID;
using RankType = WolfConquer.Core.Data.LegacyNativeNinjaRank.Type;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Fact]
    public Task NativeItemUpdateIsGenuinelyEmptyAndDoesNotTouchMetadataOrOtherOwners() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = scope.Item(17, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);
        LegacyNativeNinjaFile.gouyu_type = null!;
        scope.UseNullBin();
        item.UPDATE();
        Assert.Equal(Enumerable.Repeat(uint.MaxValue, 5), new[] { item.ItemID, item.Level, item.Position, item.Exp, item.UK });
        Assert.Same(item, scope.Ninja.Items[17]);
        scope.RestoreOwnedBin();
        AssertNoRankingPackets(scope);
    });

    [Theory]
    [InlineData(0u, 1u)]
    [InlineData(1u, 1u)]
    [InlineData(9u, 9u)]
    [InlineData(10u, 9u)]
    [InlineData(uint.MaxValue, 9u)]
    public Task AddItemClampsOnlyLevelAndKeepsFullStoredValuesWhileSingleProducerOmitsUk(uint input, uint expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Meta(7, 100, expected, score: 0);
        scope.Ninja.AddItem(100, input, uint.MaxValue, uint.MaxValue, uint.MaxValue);
        var item = Assert.Single(scope.Ninja.Items).Value;
        Assert.Equal(new uint[] { 100, expected, uint.MaxValue, uint.MaxValue, uint.MaxValue },
            new[] { item.ItemID, item.Level, item.Position, item.Exp, item.UK });
        byte[] frame = ItemFrame(ItemAction.Add, 17, (100, expected, uint.MaxValue, uint.MaxValue, 0));
        scope.Sent(frame); scope.Serialized(0, frame);
        Assert.Equal(11, scope.Available);
        Assert.All(LegacyNativeNinjaRank.Ranks.Values, rank => Assert.Empty(rank.RankingList));
    });

    [Fact]
    public Task AddItemReplacesOnlyTheCanonicalSlotAndKeepsEscapedPreviousMapsUnchanged() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var old = scope.Item(100, 100, 2, 3, 4, 5);
        var other = scope.Item(101, 101, 2);
        var escaped = scope.Ninja.Items;
        scope.Meta(1, 100, 2); scope.Meta(2, 100, 3);
        scope.Meta(3, 101, 2); scope.Meta(4, 101, 4);

        scope.Ninja.AddItem(100, 3, 6, 7, 8);
        var afterFirst = scope.Ninja.Items;
        Assert.NotSame(escaped, afterFirst);
        Assert.Same(old, escaped[100]); Assert.Same(other, escaped[101]);
        Assert.Same(other, afterFirst[101]);
        scope.Ninja.AddItem(101, 4, 9, 10, 11);

        Assert.Equal(2, scope.Ninja.Items.Count);
        Assert.NotSame(old, scope.Ninja.Items[100]);
        Assert.NotSame(other, scope.Ninja.Items[101]);
        Assert.Same(other, afterFirst[101]);
        Assert.Equal(new uint[] { 100, 2, 3, 4, 5 }, new[] { old.ItemID, old.Level, old.Position, old.Exp, old.UK });
        scope.Sent(ItemFrame(ItemAction.Add, 17, (100, 3, 6, 7, 0)), ItemFrame(ItemAction.Add, 17, (101, 4, 9, 10, 0)));
        Assert.Equal(10, scope.Available);
    });

    [Fact]
    public Task NullItemsRefuseBeforeAnyItemRankOrPacketPublication() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var old = scope.Item(100, 100);
        var dictionary = scope.Ninja.Items;
        scope.Ninja.Items = null!;
        var before = new RankingGraphWitness();
        Assert.Throws<InvalidOperationException>(() => scope.Ninja.AddItem(100, 0, 3, 4, 5));
        Assert.Null(scope.Ninja.Items); Assert.Same(old, dictionary[100]);
        before.RequireUnchanged();
        AssertNoRankingPackets(scope);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MissingOwnerOrPacketStorageRefusesBeforeItemAndRankPublication(bool recycle) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var old = scope.Item(100, 100);
        var map = scope.Ninja.Items;
        scope.Meta(1, 100, 1); scope.Meta(9, 100, 9);
        var before = new RankingGraphWitness();
        if (recycle) scope.UseNullBin(); else scope.Ninja.user = null!;
        Assert.Throws<InvalidOperationException>(() => scope.Ninja.AddItem(100, 9, 3, 4, 5));
        Assert.Same(map, scope.Ninja.Items);
        Assert.Same(old, Assert.Single(scope.Ninja.Items).Value);
        before.RequireUnchanged();
        if (recycle) scope.RestoreOwnedBin();
        AssertNoRankingPackets(scope);
    });

    [Fact]
    public Task RankRefusalLeavesItemMapEveryRankAndOutputQueueUnchanged() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var old = scope.Item(101, 101, 2);
        var map = scope.Ninja.Items;
        scope.Meta(1, 101, 2); scope.Meta(7, 100, 3, score: 1000);
        LegacyNativeNinjaRank.Ranks.Remove(RankType.Fire);
        var before = new RankingGraphWitness();
        Assert.Throws<InvalidOperationException>(() => scope.Ninja.AddItem(100, 3, 4, 5, 6));
        Assert.Same(map, scope.Ninja.Items);
        Assert.Same(old, Assert.Single(scope.Ninja.Items).Value);
        before.RequireUnchanged();
        AssertNoRankingPackets(scope);
    });
}

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
        Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available); scope.Untaken(0);
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
    public Task AddItemUsesPhysicalIdKeyOverwritesOnlyThatSlotAndRetainsOtherAliases() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var old = scope.Item(100, 200, 2, 3, 4, 5);
        Assert.True(scope.Ninja.Items.TryAdd(900, old));
        var differentKey = scope.Item(901, 101, 2);
        scope.Ninja.AddItem(100, 3, 6, 7, 8);
        scope.Ninja.AddItem(101, 4, 9, 10, 11);
        Assert.Equal(4, scope.Ninja.Items.Count);
        Assert.NotSame(old, scope.Ninja.Items[100]);
        Assert.Same(old, scope.Ninja.Items[900]);
        Assert.Same(differentKey, scope.Ninja.Items[901]);
        Assert.NotSame(differentKey, scope.Ninja.Items[101]);
        Assert.Equal(new uint[] { 200, 2, 3, 4, 5 }, new[] { old.ItemID, old.Level, old.Position, old.Exp, old.UK });
        scope.Sent(ItemFrame(ItemAction.Add, 17, (100, 3, 6, 7, 0)),
            ItemFrame(ItemAction.Add, 17, (101, 4, 9, 10, 0)));
        Assert.Equal(10, scope.Available);
    });

    [Fact]
    public Task NullItemsFaultAfterSendBeforePublicationLeavesFormerDictionaryUntouched() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var old = scope.Item(100, 200);
        var dictionary = scope.Ninja.Items;
        scope.Ninja.Items = null!;
        Assert.Throws<NullReferenceException>(() => scope.Ninja.AddItem(100, 0, 3, 4, 5));
        byte[] frame = ItemFrame(ItemAction.Add, 17, (100, 1, 3, 4, 0));
        scope.Sent(frame); scope.Serialized(0, frame);
        Assert.Null(scope.Ninja.Items); Assert.Same(old, dictionary[100]);
        Assert.Equal(11, scope.Available);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ProducerOwnerOrRecycleFailureHappensBeforeDictionaryPublication(bool recycle) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var old = scope.Item(100, 200);
        if (recycle) scope.UseNullBin(); else scope.Ninja.user = null!;
        Assert.Throws<NullReferenceException>(() => scope.Ninja.AddItem(100, 9, 3, 4, 5));
        Assert.Same(old, Assert.Single(scope.Ninja.Items).Value);
        Assert.Empty(scope.Queue);
        if (recycle)
        {
            scope.RestoreOwnedBin(); Assert.Equal(12, scope.Available); scope.Untaken(0);
        }
        else { Assert.Equal(11, scope.Available); scope.UnserializedDisposed(0); }
    });

    [Fact]
    public Task RankFaultRetainsAlreadySentAndStoredNewItemAndEarlierOverallRank() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Meta(7, 100, 3, score: 1000);
        LegacyNativeNinjaRank.Ranks.Remove(RankType.Fire);
        Assert.Throws<NullReferenceException>(() => scope.Ninja.AddItem(100, 3, 4, 5, 6));
        var item = scope.Ninja.Items[100];
        Assert.Equal(new uint[] { 100, 3, 4, 5, 6 }, new[] { item.ItemID, item.Level, item.Position, item.Exp, item.UK });
        Assert.Equal(1000u, scope.Rank(RankType.Overall).RankingList[17].TotalPoints);
        Assert.False(LegacyNativeNinjaRank.Ranks.ContainsKey(RankType.Fire));
        byte[] frame = ItemFrame(ItemAction.Add, 17, (100, 3, 4, 5, 0));
        scope.Sent(frame); scope.Serialized(0, frame); Assert.Equal(11, scope.Available);
    });
}

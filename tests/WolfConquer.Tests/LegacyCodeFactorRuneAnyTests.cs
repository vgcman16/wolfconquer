using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyCodeFactorRuneAnyTests
{
    private static LegacyNativeGameItemRuntime Item(uint uid, ushort position) =>
        new() { Uid = uid, ITEM_ID = 181305, Position = position };

    [Theory]
    [InlineData((ushort)101)] [InlineData((ushort)103)] [InlineData((ushort)211)] [InlineData((ushort)212)]
    public void AnyRuneFreeAcceptsEachValidEmptyPosition(ushort position)
    {
        var rune = new LegacyNativeRuneItemStorage(null);
        Assert.True(rune.Free(position)); Assert.Empty(rune.Objects); Assert.Equal((byte)0, rune.Count);
    }

    [Fact]
    public void AnyRuneFreeFindsRealOccupancyAndFollowsMutablePositionWithoutMutation()
    {
        var rune = new LegacyNativeRuneItemStorage(null); var item = Item(7, 101); Assert.True(rune.Add(item));
        var cache = rune.Objects; Assert.False(rune.Free(101)); Assert.True(rune.Free(103));
        item.Position = 103; Assert.True(rune.Free(101)); Assert.False(rune.Free(103));
        Assert.Same(cache, rune.Objects); Assert.Same(item, Assert.Single(cache)); Assert.True(rune.ContainsUID(7));
    }

    [Fact]
    public void AnyRuneFreeRetainsBagCapacityFallbackAndUnconditionalSpecialPosition()
    {
        var rune = new LegacyNativeRuneItemStorage(null);
        for (uint uid = 1; uid <= 100; uid++) Assert.True(rune.Add(Item(uid, 212)));
        Assert.True(rune.Free(211)); rune.Objects[0].Position = 211;
        Assert.False(rune.Free(211)); Assert.True(rune.Free(212)); Assert.Equal((byte)100, rune.Count);
    }

    [Fact]
    public void AnyRuneFreeFailsClosedWhenCacheCannotProveOrdinaryOrBagSlotOccupancy()
    {
        var rune = new LegacyNativeRuneItemStorage(null); Assert.True(rune.Add(Item(7, 101)));
        var cache = rune.Objects; cache[0] = null!;
        Assert.False(rune.Free(101)); Assert.False(rune.Free(103)); Assert.False(rune.Free(211));
        Assert.True(rune.Free(212)); Assert.Same(cache, rune.Objects); Assert.True(rune.ContainsUID(7)); Assert.Null(cache[0]);
    }

    [Fact]
    public void AnyRuneFreeUsesCurrentCacheRatherThanAnEscapedArrayAfterSuccessfulAdd()
    {
        var rune = new LegacyNativeRuneItemStorage(null); var first = Item(7, 101); Assert.True(rune.Add(first));
        var escaped = rune.Objects; Assert.True(rune.Add(Item(8, 103))); escaped[0] = null!;
        Assert.NotSame(escaped, rune.Objects); Assert.False(rune.Free(101)); Assert.False(rune.Free(103)); Assert.True(rune.Free(104));
        Assert.Same(first, rune.Objects[0]); Assert.Null(escaped[0]);
    }

    [Fact]
    public void AnyRuneFreeDoesNotReopenAnOccupiedBagBecauseThePublicByteCountWrapped()
    {
        var rune = new LegacyNativeRuneItemStorage(null);
        for (uint uid = 1; uid <= 256; uid++) Assert.True(rune.Add(Item(uid, 212)));
        rune.Objects[0].Position = 211;
        Assert.Equal(256, rune.Objects.Length);
        Assert.False(rune.Free(211)); Assert.True(rune.Free(212));
    }
}

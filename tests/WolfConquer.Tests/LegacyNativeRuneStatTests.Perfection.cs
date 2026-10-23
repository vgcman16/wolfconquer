using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeRuneStatTests
{
    [Theory]
    [InlineData(0, 1100u)] [InlineData(1, 1100u)] [InlineData(2, 1300u)]
    [InlineData(3, 1500u)] [InlineData(4, 1800u)] [InlineData(5, 2300u)]
    [InlineData(6, 2800u)] [InlineData(7, 3400u)] [InlineData(8, 4000u)]
    [InlineData(9, 4600u)] [InlineData(10, 5300u)] [InlineData(11, 6000u)]
    [InlineData(12, 6900u)] [InlineData(13, 7900u)] [InlineData(14, 8900u)]
    [InlineData(15, 9900u)] [InlineData(16, 11000u)] [InlineData(17, 13000u)]
    [InlineData(18, 14000u)] [InlineData(19, 15500u)] [InlineData(20, 17000u)]
    [InlineData(21, 18600u)] [InlineData(22, 20500u)] [InlineData(23, 22000u)]
    [InlineData(24, 24500u)] [InlineData(25, 26500u)] [InlineData(26, 29000u)]
    [InlineData(27, 31000u)] [InlineData(28, 1100u)] [InlineData(99, 1100u)]
    public void BluePerfectionPreservesAllNativeLevelsAndDefault(int level, uint expected)
    {
        using var scope = new Scope();
        var item = scope.Add(1, 4020000u + (uint)level, 102);
        Assert.Same(item, scope.Rune.EquippedBlueRune);
        Assert.Equal(expected, scope.Rune.PerfectionBlueRune);
        item.Position = 122; scope.Client.Equipment!.Alternante = true;
        Assert.Same(item, scope.Rune.EquippedBlueRune);
        Assert.Equal(expected, scope.Rune.PerfectionBlueRune);
    }

    [Theory]
    [InlineData(0, 1200u)] [InlineData(1, 1200u)] [InlineData(2, 1600u)]
    [InlineData(3, 2200u)] [InlineData(4, 3000u)] [InlineData(5, 4000u)]
    [InlineData(6, 5300u)] [InlineData(7, 6900u)] [InlineData(8, 8800u)]
    [InlineData(9, 11000u)] [InlineData(10, 1200u)] [InlineData(99, 1200u)]
    public void YellowPerfectionPreservesEachTierAndAllTwelveNativeSlotPairs(int level, uint expected)
    {
        using var scope = new Scope();
        for (ushort position = 103; position <= 114; position++)
            scope.Add(position, 4030000u + (uint)level, position);
        Assert.Equal(expected * 12, scope.Rune.PerfectionYellowRune);
        foreach (var item in scope.Rune.Objects) item.Position += 20;
        scope.Client.Equipment!.Alternante = true;
        Assert.Equal(expected * 12, scope.Rune.PerfectionYellowRune);
    }

    [Fact]
    public void SlotPropertiesUseActualFirstMatchingPositionsNotFamilyClassificationOrDictionaryKey()
    {
        using var scope = new Scope();
        var red = scope.Add(1, 7000000, 101);
        var blue = scope.Add(2, 7000027, 102);
        scope.Add(3, 4020001, 102);
        Assert.Equal(3000u, scope.Rune.PerfectionRedRune); Assert.Same(red, scope.Rune.EquippedRedRune);
        Assert.Equal(31000u, scope.Rune.PerfectionBlueRune); Assert.Same(blue, scope.Rune.EquippedBlueRune);
        red.Position = 212; blue.Position = 212;
        Assert.Null(scope.Rune.EquippedRedRune); Assert.Equal(0u, scope.Rune.PerfectionRedRune);
        Assert.Equal(1100u, scope.Rune.PerfectionBlueRune);
    }

    [Fact]
    public void YellowPerfectionChoosesFirstPerSlotAndExcludesTheExtraEquippedCountSlot()
    {
        using var scope = new Scope();
        scope.Add(1, 4030002, 103);
        scope.Add(2, 4030009, 103);
        scope.Add(3, 4030003, 104);
        scope.Add(4, 4030009, 115); // EquippedCount accepts it; perfection loop ends114.
        Assert.Equal(3800u, scope.Rune.PerfectionYellowRune);
        Assert.Equal((byte)4, scope.Rune.EquippedCount);
        Assert.Equal(0u, scope.Rune.PerfectionBlueRune);
    }

    [Theory]
    [InlineData(false, 101, 1)] [InlineData(false, 115, 1)] [InlineData(false, 116, 0)]
    [InlineData(false, 120, 0)] [InlineData(false, 121, 0)] [InlineData(true, 120, 0)]
    [InlineData(true, 121, 1)] [InlineData(true, 135, 1)] [InlineData(true, 136, 0)]
    [InlineData(true, 101, 0)]
    public void EquippedCountUsesRawActiveRangesNotWornFallback(bool alternate, int position, int expected)
    {
        using var scope = new Scope();
        scope.Client.Equipment!.Alternante = alternate;
        scope.Add(1, 7000001, (ushort)position);
        Assert.Equal((byte)expected, scope.Rune.EquippedCount);
    }

    [Fact]
    public void EquippedCountSkipsNullAfterOwnerReadAndRetainsNativeByteWrap()
    {
        using var scope = new Scope();
        for (uint uid = 1; uid <= 257; uid++) scope.Add(uid, 7000001, 101);
        Assert.Equal((byte)1, scope.Rune.EquippedCount);
        scope.Rune.Objects[0] = null!;
        Assert.Equal((byte)0, scope.Rune.EquippedCount);
        scope.Rune.Owner = null;
        Assert.Throws<NullReferenceException>(() => scope.Rune.EquippedCount);
        Assert.Throws<NullReferenceException>(() => scope.Rune.EquippedRedRune);
    }

    [Fact]
    public void WornFallbackChoosesAlternateSlotOnlyWhileScoreIncludesBothPhysicalItems()
    {
        using var scope = new Scope();
        var normal = scope.Add(1, 4020027, 102);
        var alternate = scope.Add(2, 4020002, 122);
        Assert.Same(normal, scope.Rune.EquippedBlueRune); Assert.Equal(9700u, scope.Rune.Score);
        scope.Client.Equipment!.Alternante = true;
        Assert.Same(alternate, scope.Rune.EquippedBlueRune); Assert.Equal(1300u, scope.Rune.PerfectionBlueRune);
        alternate.Position = 212;
        Assert.Same(normal, scope.Rune.EquippedBlueRune); // Genuine normal-slot fallback.
        Assert.Equal((byte)0, scope.Rune.EquippedCount);
        Assert.Equal(9700u, scope.Rune.Score); //212 excluded from worn, NOT from Score.
    }

    [Theory]
    [InlineData(4010001u, 101u, true)] [InlineData(4010001u, 121u, true)]
    [InlineData(4020027u, 102u, true)] [InlineData(4020001u, 122u, true)]
    [InlineData(4030009u, 103u, true)] [InlineData(4030009u, 111u, true)]
    [InlineData(4030009u, 123u, true)] [InlineData(4030009u, 131u, true)]
    [InlineData(4030009u, 112u, false)] [InlineData(4030009u, 114u, false)]
    [InlineData(4030009u, 132u, false)] [InlineData(4030009u, 135u, false)]
    [InlineData(4010001u, 102u, false)] [InlineData(4020001u, 101u, false)]
    [InlineData(4030009u, 211u, false)] [InlineData(7000001u, 103u, false)]
    public void CheckPreservesNarrowNativeYellowRangesAndFamilies(uint id, uint position, bool expected)
    {
        using var scope = new Scope();
        scope.Rune.Owner = null;
        Assert.Equal(expected, scope.Rune.Check(id, position));
    }

    [Fact]
    public void FullRequiresAtLeastElevenWornValuesAndUsesActualNativeMaxLevelForEach()
    {
        using var scope = new Scope();
        for (uint uid = 1; uid <= 10; uid++) scope.Add(uid, 4030009, 103);
        Assert.False(scope.Rune.Full());
        var last = scope.Add(11, 4020026, 102);
        Assert.False(scope.Rune.Full());
        last.ITEM_ID = Item(99, 4020027).ITEM_ID;
        Assert.True(scope.Rune.Full());
        scope.Add(12, 4010001, 101);
        Assert.True(scope.Rune.Full());
        last.Position = 211;
        Assert.True(scope.Rune.Full()); //Still eleven; uniqueness/slot validation is not added.
        scope.Rune.Objects[0].Position = 211;
        Assert.False(scope.Rune.Full());
    }
}

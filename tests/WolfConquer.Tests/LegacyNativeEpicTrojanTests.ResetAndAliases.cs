namespace WolfConquer.Tests;

public sealed partial class LegacyNativeEpicTrojanTests
{
    [Fact]
    public void ResetClearsNativeScalarsAndGhostRestoresFourFlagsButRetainsEveryArrayByteAndAlias()
    {
        var player = Seed();
        var arrays = Arrays(player);
        var before = arrays.Select(array => array!.ToArray()).ToArray();
        player.ResetEpicTrojan();
        Assert.Equal(new uint[] { 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 0, 1, 0 }, Scalars(player));
        SameArrays(player, arrays);
        var after = Arrays(player);
        for (int group = 0; group < 4; group++) Assert.Equal(before[group], after[group]);
    }

    [Fact]
    public void ResetDoesNotDereferenceNullEmptyShortOrSharedArrays()
    {
        var shared = new uint[] { 71, 72, 73 };
        uint[]?[][] controls =
        [
            [null, null, null, null],
            [[], [81], shared, shared],
            [shared, shared, shared, shared]
        ];
        foreach (var control in controls)
        {
            var player = Seed();
            for (int group = 0; group < 4; group++) SetArray(player, group, control[group]);
            var arrays = Arrays(player);
            var before = arrays.Select(array => array?.ToArray()).ToArray();
            player.ResetEpicTrojan();
            Assert.Equal(new uint[] { 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 0, 1, 0 }, Scalars(player));
            SameArrays(player, arrays);
            var after = Arrays(player);
            for (int group = 0; group < 4; group++) Assert.Equal(before[group], after[group]);
        }
        Assert.Equal(new uint[] { 71, 72, 73 }, shared);
    }

    [Fact]
    public void AliasedArraysAreWrittenInNativeLoadOrderSoGeneralStageValuesWinEverySharedSlot()
    {
        using var culture = new CultureScope();
        var player = Seed(); var shared = new uint[] { 701, 702 };
        for (int group = 0; group < 4; group++) SetArray(player, group, shared);
        player.LoadEpicTrojan(Sequence);
        Assert.Equal(new uint[] { 16, 17 }, shared);
        Assert.All(Arrays(player), array => Assert.Same(shared, array));
        Assert.Equal(new uint[] { 3, 4, 5, 10, 11, 7, 6, 201, 14, 15, 18, 19, 20 }, Scalars(player));
        Assert.Equal("16/17/3/4/5/6/7/16/17/10/11/16/17/14/15/16/17/18/19/20/", player.SaveEpicTrojan());
        player.ResetEpicTrojan();
        Assert.Equal(new uint[] { 16, 17 }, shared);
        Assert.All(Arrays(player), array => Assert.Same(shared, array));
    }
}

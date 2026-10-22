namespace WolfConquer.Tests;

public sealed partial class LegacyNativeEpicTrojanTests
{
    [Fact]
    public void ShortFirstArrayReceivesItsFirstElementThenFaultsBeforeAnyScalarChange()
    {
        var player = Seed(); player.EpicTrojanItemChange = [700];
        var scalars = Scalars(player); var arrays = Arrays(player);
        Assert.Throws<IndexOutOfRangeException>(() => player.LoadEpicTrojan(Sequence));
        Assert.Equal(new uint[] { 1 }, player.EpicTrojanItemChange);
        Assert.Equal(scalars, Scalars(player)); SameArrays(player, arrays);
        Assert.Equal(new uint[] { 108, 109 }, player.EpicTrojanArrayItemChange);
        Assert.Equal(new uint[] { 112, 113 }, player.EpicTrojanMr_MirrorItemChange);
        Assert.Equal(new uint[] { 116, 117 }, player.EpicTrojanGeneralPakItemChange);
    }

    [Fact]
    public void ShortArrayStagePreservesEarlierMainFieldsAndItsFirstWriteButLeavesLaterScalarsOld()
    {
        var player = Seed(); player.EpicTrojanArrayItemChange = [700];
        var arrays = Arrays(player);
        Assert.Throws<IndexOutOfRangeException>(() => player.LoadEpicTrojan(Sequence));
        Assert.Equal(new uint[] { 1, 2 }, player.EpicTrojanItemChange);
        Assert.Equal(new uint[] { 8 }, player.EpicTrojanArrayItemChange);
        Assert.Equal(new uint[] { 3, 4, 5, 110, 111, 7, 6, 201, 114, 115, 118, 119, 120 }, Scalars(player));
        Assert.Equal(new uint[] { 112, 113 }, player.EpicTrojanMr_MirrorItemChange);
        Assert.Equal(new uint[] { 116, 117 }, player.EpicTrojanGeneralPakItemChange);
        SameArrays(player, arrays);
    }

    [Fact]
    public void ShortMirrorArrayFaultsAfterPointsAndAbysalWritesBeforeMirrorChangeOrGeneralFields()
    {
        var player = Seed(); player.EpicTrojanMr_MirrorItemChange = [700];
        var arrays = Arrays(player);
        Assert.Throws<IndexOutOfRangeException>(() => player.LoadEpicTrojan(Sequence));
        Assert.Equal(new uint[] { 1, 2 }, player.EpicTrojanItemChange);
        Assert.Equal(new uint[] { 8, 9 }, player.EpicTrojanArrayItemChange);
        Assert.Equal(new uint[] { 12 }, player.EpicTrojanMr_MirrorItemChange);
        Assert.Equal(new uint[] { 3, 4, 5, 10, 11, 7, 6, 201, 114, 115, 118, 119, 120 }, Scalars(player));
        Assert.Equal(new uint[] { 116, 117 }, player.EpicTrojanGeneralPakItemChange);
        SameArrays(player, arrays);
    }

    [Fact]
    public void ShortGeneralArrayRetainsAllEarlierStagesAndItsFirstWriteBeforeFinalThreeScalars()
    {
        var player = Seed(); player.EpicTrojanGeneralPakItemChange = [700];
        var arrays = Arrays(player);
        Assert.Throws<IndexOutOfRangeException>(() => player.LoadEpicTrojan(Sequence));
        Assert.Equal(new uint[] { 1, 2 }, player.EpicTrojanItemChange);
        Assert.Equal(new uint[] { 8, 9 }, player.EpicTrojanArrayItemChange);
        Assert.Equal(new uint[] { 12, 13 }, player.EpicTrojanMr_MirrorItemChange);
        Assert.Equal(new uint[] { 16 }, player.EpicTrojanGeneralPakItemChange);
        Assert.Equal(new uint[] { 3, 4, 5, 10, 11, 7, 6, 201, 14, 15, 118, 119, 120 }, Scalars(player));
        SameArrays(player, arrays);
    }

    [Fact]
    public void NullArrayAtEveryStageKeepsTheExactCompletedPrefixAndDoesNotAllocateAReplacement()
    {
        uint[][] scalarPrefixes =
        [
            [103, 104, 105, 110, 111, 107, 106, 201, 114, 115, 118, 119, 120],
            [3, 4, 5, 110, 111, 7, 6, 201, 114, 115, 118, 119, 120],
            [3, 4, 5, 10, 11, 7, 6, 201, 114, 115, 118, 119, 120],
            [3, 4, 5, 10, 11, 7, 6, 201, 14, 15, 118, 119, 120]
        ];
        uint[][] completedArrays = [[1, 2], [8, 9], [12, 13], [16, 17]];
        for (int stage = 0; stage < 4; stage++)
        {
            var player = Seed(); SetArray(player, stage, null);
            var arrays = Arrays(player);
            var oldBytes = arrays.Select(array => array?.ToArray()).ToArray();
            Assert.Throws<NullReferenceException>(() => player.LoadEpicTrojan(Sequence));
            SameArrays(player, arrays);
            Assert.Equal(scalarPrefixes[stage], Scalars(player));
            var actual = Arrays(player);
            Assert.Null(actual[stage]);
            for (int group = 0; group < 4; group++)
                if (group != stage)
                    Assert.Equal(group < stage ? completedArrays[group] : oldBytes[group], actual[group]);
        }
    }

    [Fact]
    public void SaveRetainsNativeNullOrShortArrayFaultsWithoutRepairingOrMutatingAnyState()
    {
        foreach (int stage in Enumerable.Range(0, 4))
        foreach (uint[]? invalid in new uint[]?[] { null, [], [77] })
        {
            var player = Seed(); SetArray(player, stage, invalid);
            var arrays = Arrays(player); var scalars = Scalars(player);
            var before = arrays.Select(array => array?.ToArray()).ToArray();
            if (invalid is null) Assert.Throws<NullReferenceException>(() => player.SaveEpicTrojan());
            else Assert.Throws<IndexOutOfRangeException>(() => player.SaveEpicTrojan());
            SameArrays(player, arrays); Assert.Equal(scalars, Scalars(player));
            var after = Arrays(player);
            for (int group = 0; group < 4; group++) Assert.Equal(before[group], after[group]);
        }
    }
}

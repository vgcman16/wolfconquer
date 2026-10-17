using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class LegacySobNpcNameTranslatorTests
{
    [Fact]
    public void TranslateUsesExactBackupRowAndPreservesLocalizedName()
    {
        var target = "21010,战神觉醒,2,50000,11724,24,39,0,0,1,11,ارشيف المحارب\r\n";
        var reference = "21010,ArchiveWarrior,2,50000,11724,24,39,0,0,1,11,ارشيف المحارب\r\n";

        var result = LegacySobNpcNameTranslator.Translate(target, reference, out var updated);

        Assert.True(result.Changed);
        Assert.Equal(1, result.ReferenceRowsScanned);
        Assert.Equal(1, result.TargetRowsScanned);
        Assert.Equal(1, result.TranslatableNames);
        Assert.Equal(1, result.ExactRowMatches);
        Assert.Equal(0, result.UniqueIdMatches);
        Assert.Equal(0, result.UnmatchedNames);
        Assert.Equal(1, result.ChangedNames);
        Assert.Equal("21010,ArchiveWarrior,2,50000,11724,24,39,0,0,1,11,ارشيف المحارب\r\n", updated);
    }

    [Fact]
    public void TranslateUsesExactRowToResolveDuplicateIds()
    {
        var target =
            "6001,活动米饭,2,11,1002,10,11,0,0,1,0\n" +
            "6001,木桩,21,11,1002,12,13,0,0,1,0\n";
        var reference =
            "6001,HoursRice,2,11,1002,10,11,0,0,1,0\n" +
            "6001,Stake,21,11,1002,12,13,0,0,1,0\n";

        var result = LegacySobNpcNameTranslator.Translate(target, reference, out var updated);

        Assert.True(result.Changed);
        Assert.Equal(2, result.ExactRowMatches);
        Assert.Equal(0, result.UniqueIdMatches);
        Assert.Equal(2, result.ChangedNames);
        Assert.Contains("6001,HoursRice,2,11,1002,10,11,0,0,1,0\n", updated);
        Assert.Contains("6001,Stake,21,11,1002,12,13,0,0,1,0\n", updated);
    }

    [Fact]
    public void TranslateUsesUniqueIdFallbackWhenPlacementChanged()
    {
        var target = "5600,骑术技能训练师,2,4427,1002,341,451,0,0,1,0\n";
        var reference = "5600,Mount-Trainer,2,4427,1002,340,450,0,0,1,0\n";

        var result = LegacySobNpcNameTranslator.Translate(target, reference, out var updated);

        Assert.True(result.Changed);
        Assert.Equal(0, result.ExactRowMatches);
        Assert.Equal(1, result.UniqueIdMatches);
        Assert.Equal("5600,Mount-Trainer,2,4427,1002,341,451,0,0,1,0\n", updated);
    }

    [Fact]
    public void TranslateDoesNotUseConflictingDuplicateIdWithoutExactRow()
    {
        var target = "6001,测试对象,2,11,1002,99,99,0,0,1,0\n";
        var reference =
            "6001,HoursRice,2,11,1002,10,11,0,0,1,0\n" +
            "6001,Stake,21,11,1002,12,13,0,0,1,0\n";

        var result = LegacySobNpcNameTranslator.Translate(target, reference, out var updated);

        Assert.False(result.Changed);
        Assert.Equal(0, result.ExactRowMatches);
        Assert.Equal(0, result.UniqueIdMatches);
        Assert.Equal(1, result.UnmatchedNames);
        Assert.Equal(target, updated);
    }
}

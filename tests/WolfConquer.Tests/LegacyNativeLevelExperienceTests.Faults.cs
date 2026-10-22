using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeLevelExperienceTests
{
    [Theory]
    [InlineData("", "FormatException")]
    [InlineData("// comment", "FormatException")]
    [InlineData("0 1", "IndexOutOfRangeException")]
    [InlineData("0 1 2 3", "IndexOutOfRangeException")]
    [InlineData("0  1 2 3 4", "FormatException")]
    [InlineData("256 1 2 3 4", "OverflowException")]
    [InlineData("0 256 2 3 4", "OverflowException")]
    [InlineData("0 1 18446744073709551616 3 4", "OverflowException")]
    [InlineData("0 1 2 2147483648 4", "OverflowException")]
    [InlineData("0 1 2 3 -2147483649", "OverflowException")]
    [InlineData("0 1 -1 3 4", "OverflowException")]
    [InlineData("0 1 2 nope 4", "FormatException")]
    [InlineData("0 1 2 3 nope", "FormatException")]
    [InlineData("0\t1\t2\t3\t4", "FormatException")]
    public void NativeSplitAndParseFaultsKeepOnlyCompleteEarlierRowsAndStopBeforeLaterRows(string invalid, string expected)
    {
        using var scope = new Scope();
        scope.Seed("9 9 19 29 39\r\n" + invalid + "\r\n1 7 11 13 17\r\n");
        var physical = LegacyNativePoolLevelInfo.LevelInfo;
        Exception failure = Assert.IsAssignableFrom<Exception>(Record.Exception(LegacyNativeServerExperience.LoadExpInfo));
        Assert.Equal(expected, failure.GetType().Name);
        Assert.Same(physical, LegacyNativePoolLevelInfo.LevelInfo);
        Assert.Equal(LegacyNativeDbLevExp.Sort.Wrangler, Assert.Single(physical).Key);
        var kept = Assert.Single(physical[LegacyNativeDbLevExp.Sort.Wrangler]).Value;
        Assert.Equal((byte)9, kept.Level); Assert.Equal(19UL, kept.Experience);
        Assert.Equal(29, kept.UpLevTime); Assert.Equal(39, kept.MentorUpLevTime);
        using var exclusive = new FileStream(scope.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.CanRead); // Throwing parse path still disposes the actual reader.
    }

    [Fact]
    public void DuplicateLevelThrowsWithoutReplacingOriginalAndWithoutLoadingLaterAction()
    {
        using var scope = new Scope(); scope.Seed("0 1 2 3 4\r\n0 1 99 88 77\r\n9 7 11 13 17");
        Assert.Throws<ArgumentException>(LegacyNativeServerExperience.LoadExpInfo);
        var rows = Assert.Single(LegacyNativePoolLevelInfo.LevelInfo).Value;
        var kept = Assert.Single(rows).Value;
        Assert.Equal(2UL, kept.Experience); Assert.Equal(3, kept.UpLevTime); Assert.Equal(4, kept.MentorUpLevTime);
        Assert.False(LegacyNativePoolLevelInfo.LevelInfo.ContainsKey(LegacyNativeDbLevExp.Sort.Wrangler));
    }

    [Fact]
    public void RepeatedLoadIsNotIdempotentAndRetainsSameRowsWhenFirstDuplicateFails()
    {
        using var scope = new Scope(); scope.Seed("0 1 2 3 4\r\n0 2 5 6 7");
        LegacyNativeServerExperience.LoadExpInfo();
        var outer = LegacyNativePoolLevelInfo.LevelInfo; var inner = outer[LegacyNativeDbLevExp.Sort.User];
        var first = inner[1]; var second = inner[2];
        Assert.Throws<ArgumentException>(LegacyNativeServerExperience.LoadExpInfo);
        Assert.Same(outer, LegacyNativePoolLevelInfo.LevelInfo); Assert.Same(inner, outer[LegacyNativeDbLevExp.Sort.User]);
        Assert.Same(first, inner[1]); Assert.Same(second, inner[2]); Assert.Equal(2, inner.Count);
    }

    [Fact]
    public void NullInnerDictionaryFaultsAfterParseWithoutRepairingOuterOwner()
    {
        using var scope = new Scope(); scope.Seed("0 1 2 3 4");
        var outer = LegacyNativePoolLevelInfo.LevelInfo; outer.Add(LegacyNativeDbLevExp.Sort.User, null!);
        Assert.Throws<NullReferenceException>(LegacyNativeServerExperience.LoadExpInfo);
        Assert.Same(outer, LegacyNativePoolLevelInfo.LevelInfo);
        Assert.Null(Assert.Single(outer).Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompleteRowParsingPrecedesNullOuterDictionaryAccess(bool invalidLastField)
    {
        using var scope = new Scope(); scope.Seed(invalidLastField ? "0 1 2 3 nope" : "0 1 2 3 4");
        LegacyNativePoolLevelInfo.LevelInfo = null!;
        if (invalidLastField) Assert.Throws<FormatException>(LegacyNativeServerExperience.LoadExpInfo);
        else Assert.Throws<NullReferenceException>(LegacyNativeServerExperience.LoadExpInfo);
        Assert.Null(LegacyNativePoolLevelInfo.LevelInfo);
    }

    [Fact]
    public void ExclusiveOwnedFileFailureDoesNotPublishRowsAndDoesNotLeaveADelayedReader()
    {
        Assert.True(OperatingSystem.IsWindows());
        using var scope = new Scope(); scope.Seed("0 1 2 3 4");
        using (var exclusive = new FileStream(scope.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.ThrowsAny<IOException>(LegacyNativeServerExperience.LoadExpInfo);
        Assert.Empty(LegacyNativePoolLevelInfo.LevelInfo);
        LegacyNativeServerExperience.LoadExpInfo();
        Assert.Equal(2UL, LegacyNativePoolLevelInfo.LevelInfo[LegacyNativeDbLevExp.Sort.User][1].Experience);
    }
}

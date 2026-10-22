using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeLevelExperienceTests
{
    [Fact]
    public void ExistingGenuineRowRetainsNativePhysicalDefaultsWidthsAndCompleteSortDeclaration()
    {
        var row = new LegacyNativeDbLevExp();
        Assert.Equal(LegacyNativeDbLevExp.Sort.User, row.Action); Assert.Equal((byte)0, row.Level);
        Assert.Equal(0UL, row.Experience); Assert.Equal(0, row.UpLevTime); Assert.Equal(0, row.MentorUpLevTime);
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(LegacyNativeDbLevExp.Sort)));
        Assert.Equal(new[] { "User", "MartialArtist", "Warlock", "ChiMaster", "Sage", "Apothecary", "Performer", "Wrangler" },
            Enum.GetNames<LegacyNativeDbLevExp.Sort>());
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 9 }, Enum.GetValues<LegacyNativeDbLevExp.Sort>().Select(value => (byte)value));
    }

    [Fact]
    public void WholeLoadKeepsAllWidthsUnknownSortsSignedTimesPhysicalLevelsAndIgnoredExtraColumns()
    {
        using var scope = new Scope();
        scope.Seed("0 255 18446744073709551615 -2147483648 2147483647 extra ignored\r\n" +
            "255 0 0 -1 0\r\n0 0 9 11 13\r\n");
        var physical = LegacyNativePoolLevelInfo.LevelInfo;
        LegacyNativeServerExperience.LoadExpInfo();
        Assert.Same(physical, LegacyNativePoolLevelInfo.LevelInfo);
        Assert.Equal(new[] { LegacyNativeDbLevExp.Sort.User, (LegacyNativeDbLevExp.Sort)255 }, physical.Keys);
        var maximum = physical[LegacyNativeDbLevExp.Sort.User][255];
        Assert.Equal(LegacyNativeDbLevExp.Sort.User, maximum.Action); Assert.Equal(byte.MaxValue, maximum.Level);
        Assert.Equal(ulong.MaxValue, maximum.Experience); Assert.Equal(int.MinValue, maximum.UpLevTime);
        Assert.Equal(int.MaxValue, maximum.MentorUpLevTime);
        var unknown = physical[(LegacyNativeDbLevExp.Sort)255][0];
        Assert.Equal((LegacyNativeDbLevExp.Sort)255, unknown.Action); Assert.Equal((byte)0, unknown.Level);
        Assert.Equal(0UL, unknown.Experience); Assert.Equal(-1, unknown.UpLevTime); Assert.Equal(0, unknown.MentorUpLevTime);
        var last = physical[LegacyNativeDbLevExp.Sort.User][0];
        Assert.Equal(9UL, last.Experience); Assert.Equal(11, last.UpLevTime); Assert.Equal(13, last.MentorUpLevTime);
        Assert.NotSame(maximum, unknown); Assert.NotSame(maximum, last); Assert.NotSame(unknown, last);
        using var exclusive = new FileStream(scope.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.CanRead); // Successful using path actually releases the reader.
    }

    [Fact]
    public void LoaderAppendsToActualExistingOuterAndInnerOwnersWithoutNormalizingPreexistingRowAliases()
    {
        using var scope = new Scope(); scope.Seed("0 1 2 3 4");
        var existing = new LegacyNativeDbLevExp { Action = LegacyNativeDbLevExp.Sort.Wrangler, Level = 7, Experience = 8 };
        var inner = new Dictionary<byte, LegacyNativeDbLevExp> { [99] = existing };
        var outer = LegacyNativePoolLevelInfo.LevelInfo; outer.Add(LegacyNativeDbLevExp.Sort.User, inner);
        LegacyNativeServerExperience.LoadExpInfo();
        Assert.Same(outer, LegacyNativePoolLevelInfo.LevelInfo); Assert.Same(inner, outer[LegacyNativeDbLevExp.Sort.User]);
        Assert.Same(existing, inner[99]); Assert.Equal((byte)7, existing.Level);
        Assert.Equal(LegacyNativeDbLevExp.Sort.Wrangler, existing.Action); Assert.Equal(2, inner.Count);
        Assert.Equal(2UL, inner[1].Experience); Assert.Equal(3, inner[1].UpLevTime); Assert.Equal(4, inner[1].MentorUpLevTime);
    }

    [Fact]
    public void LiteralLocationPrefixIsConcatenatedRatherThanCombinedOrNormalizedAsADirectory()
    {
        using var scope = new Scope(); scope.Seed("0 1 2 3 4"); scope.Seed("9 7 11 13 17", prefix: true);
        scope.LiteralPrefix(); LegacyNativeServerExperience.LoadExpInfo();
        Assert.False(LegacyNativePoolLevelInfo.LevelInfo.ContainsKey(LegacyNativeDbLevExp.Sort.User));
        var row = Assert.Single(LegacyNativePoolLevelInfo.LevelInfo[LegacyNativeDbLevExp.Sort.Wrangler]).Value;
        Assert.Equal((byte)7, row.Level); Assert.Equal(11UL, row.Experience);
        Assert.Equal(13, row.UpLevTime); Assert.Equal(17, row.MentorUpLevTime);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrEmptyFileDoesNotClearExistingTableOrRequireANonNullTable(bool emptyFile)
    {
        using var scope = new Scope();
        if (emptyFile) scope.Seed("");
        var row = new LegacyNativeDbLevExp();
        LegacyNativePoolLevelInfo.LevelInfo.Add(LegacyNativeDbLevExp.Sort.User, new() { [9] = row });
        var physical = LegacyNativePoolLevelInfo.LevelInfo;
        LegacyNativeServerExperience.LoadExpInfo();
        Assert.Same(physical, LegacyNativePoolLevelInfo.LevelInfo);
        Assert.Same(row, physical[LegacyNativeDbLevExp.Sort.User][9]);
        LegacyNativePoolLevelInfo.LevelInfo = null!;
        LegacyNativeServerExperience.LoadExpInfo();
        Assert.Null(LegacyNativePoolLevelInfo.LevelInfo);
    }

    [Fact]
    public void ParsesWithCurrentCultureSignsAndStreamReaderBomDetectionWithoutInvariantSubstitution()
    {
        using var scope = new Scope();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p"; culture.NumberFormat.NegativeSign = "m";
        CultureInfo.CurrentCulture = culture;
        scope.Seed("p9 p7 p11 m13 p17\r\n", unicode: true);
        LegacyNativeServerExperience.LoadExpInfo();
        var row = LegacyNativePoolLevelInfo.LevelInfo[LegacyNativeDbLevExp.Sort.Wrangler][7];
        Assert.Equal(11UL, row.Experience); Assert.Equal(-13, row.UpLevTime); Assert.Equal(17, row.MentorUpLevTime);
    }
}

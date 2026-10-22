using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using RankTable = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeCombatGearRankPersistenceTests
{
    [Fact]
    public void SaveThenLoadUsesAllTwentyTwoLiteralColumnsAndRealFileBytes()
    {
        using var files = new NativeRankFileScope();
        var original = Entry();
        Place(original);
        RankTable.Save();
        Assert.Equal(Encoding.UTF8.GetBytes(NativeRankFileScope.Text("Count=1", FullRow)), File.ReadAllBytes(files.RankPath));
        RankTable.Ranks = new();
        RankTable.Load("ignored");
        var actual = Assert.Single(RankTable.Ranks[(RankTable.Type)19].RankingList).Value;
        Assert.NotSame(original, actual);
        Assert.Equal(new object[] { 41u, "Hero", 19u, 404u, 505u, (byte)206, 707u, 808u,
            909u, 1010u, 1111u, 1212u, 1313u, 1414u, 1515u, 1616u, 1717u, 1818u,
            1919u, 2020u, "Guild", 2222u }, Values(actual));
        Assert.Equal(18, RankTable.Ranks.Count);
    }

    [Fact]
    public void LoadIgnoresEvenNullOrAlternativeDirectoryArgument()
    {
        using var files = new NativeRankFileScope();
        files.Rows(FullRow);
        string alternate = Path.Combine(files.Root, "alternative");
        Directory.CreateDirectory(alternate);
        string otherFile = Path.Combine(alternate, "CombatGearRank.txt");
        File.WriteAllText(otherFile, NativeRankFileScope.Text("Count=1", Row(99)));
        foreach (string? argument in new string?[] { null, alternate + Path.DirectorySeparatorChar })
        {
            RankTable.Ranks = new();
            RankTable.Load(argument!);
            Assert.Equal(41u, Assert.Single(RankTable.Ranks[(RankTable.Type)19].RankingList).Value.UID);
        }
        Assert.Contains("99/", File.ReadAllText(otherFile));
    }

    [Fact]
    public void LoadConcatenatesGlobalPrefixWithoutInsertingASeparator()
    {
        using var files = new NativeRankFileScope();
        string prefix = Path.Combine(files.Root, "raw-prefix-");
        string rawFile = prefix + "CombatGearRank.txt";
        File.WriteAllText(rawFile, NativeRankFileScope.Text("Count=1", Row(7)));
        files.Rows(Row(88));
        LegacyNativeServerConfig.DbLocation = prefix;
        RankTable.Load(files.Database);
        Assert.Equal(7u, Assert.Single(RankTable.Ranks[(RankTable.Type)19].RankingList).Key);
    }

    [Fact]
    public void RepeatedTrueGateLoadFaultsInCreateBeforeAnyFileRead()
    {
        using var files = new NativeRankFileScope();
        files.Rows();
        RankTable.Load("");
        var ranks = RankTable.Ranks;
        var first = ranks[(RankTable.Type)19];
        LegacyNativeServerConfig.DbLocation = Path.Combine(files.Root, "missing") + Path.DirectorySeparatorChar;
        Assert.Throws<ArgumentException>(() => RankTable.Load("unused"));
        Assert.Same(ranks, RankTable.Ranks);
        Assert.Same(first, RankTable.Ranks[(RankTable.Type)19]);
        Assert.Equal(new uint[] { 19, 20, 21, 22, 26, 27, 28, 29, 30, 33, 34, 35, 36, 37, 38, 39, 40, 42 },
            ranks.Keys.Select(type => (uint)type));
        Assert.All(ranks.Values, rank => { Assert.Equal(10, rank.MaxItems); Assert.Empty(rank.RankingList); });
    }

    [Fact]
    public void CreatePublishesEarlierRankBeforeLaterDuplicateAndNeverStartsReader()
    {
        using var files = new NativeRankFileScope();
        var retained = new RankTable.Rank((RankTable.Type)20) { MaxItems = 77 };
        RankTable.Ranks.Add((RankTable.Type)20, retained);
        LegacyNativeServerConfig.DbLocation = Path.Combine(files.Root, "absent") + Path.DirectorySeparatorChar;
        Assert.Throws<ArgumentException>(() => RankTable.Load("ignored"));
        Assert.Same(retained, RankTable.Ranks[(RankTable.Type)20]);
        Assert.Equal(10, RankTable.Ranks[(RankTable.Type)19].MaxItems);
        Assert.Equal(new uint[] { 20, 19 }, RankTable.Ranks.Keys.Select(type => (uint)type));
    }

    [Fact]
    public void FalseGateStillLoadsIntoRetainedRankAndReplacesOnlyItsRankingDictionary()
    {
        using var files = new NativeRankFileScope();
        LegacyNativeCombatGearTable.m_Load = false;
        var existing = Entry(5);
        var rank = Place(existing, mapKey: 5);
        var oldRows = rank.RankingList;
        var ranks = RankTable.Ranks;
        files.Rows(Row(7, 800));
        RankTable.Load("ignored");
        Assert.Same(ranks, RankTable.Ranks);
        Assert.Same(rank, Assert.Single(RankTable.Ranks).Value);
        Assert.NotSame(oldRows, rank.RankingList);
        Assert.Same(existing, rank.RankingList[5]);
        Assert.Equal(new uint[] { 7, 5 }, rank.RankingList.Keys);
        Assert.Equal(new uint[] { 5, 7 }, oldRows.Keys);
        Assert.Same(existing, oldRows[5]);
        Assert.Same(rank.RankingList[7], oldRows[7]); // Source mutates the escaped map before replacing its sorted owner.
    }

    [Fact]
    public void ZeroCountCreatesRanksButIgnoresPhysicalRowsAndReturnsNormally()
    {
        using var files = new NativeRankFileScope();
        File.WriteAllText(files.RankPath, NativeRankFileScope.Text("Count=0", "not/a/valid/row"));
        RankTable.Load("");
        Assert.Equal(18, RankTable.Ranks.Count);
        Assert.All(RankTable.Ranks.Values, rank => Assert.Empty(rank.RankingList));
    }

    [Fact]
    public void CountUsesSecondEqualsTokenAndIgnoresRowsBeyondCount()
    {
        using var files = new NativeRankFileScope();
        File.WriteAllText(files.RankPath, NativeRankFileScope.Text("anything=1=ignored", Row(7), "bad"));
        RankTable.Load("");
        Assert.Equal(7u, Assert.Single(RankTable.Ranks[(RankTable.Type)19].RankingList).Key);
    }

    [Fact]
    public void InvalidOverflowAndMissingNumericColumnsUseNativeZeroDefaults()
    {
        using var files = new NativeRankFileScope();
        files.Rows("4294967296//19/not-a-number/4294967296/256/-1/", "2/Short/19");
        RankTable.Load("");
        var rank = RankTable.Ranks[(RankTable.Type)19];
        Assert.Equal(new object[] { 0u, "", 19u, 0u, 0u, (byte)0, 0u, 0u, 0u, 0u,
            0u, 0u, 0u, 0u, 0u, 0u, 0u, 0u, 0u, 0u, "", 0u }, Values(rank.RankingList[0]));
        Assert.Equal("Short", rank.RankingList[2].Name);
        Assert.Equal(0u, rank.RankingList[2].WeaponWar);
    }

    [Fact]
    public void HeaderAndRowParsingHonorCurrentCulturePositiveSign()
    {
        using var files = new NativeRankFileScope();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = culture;
        File.WriteAllText(files.RankPath, NativeRankFileScope.Text("Count=p1", "p41/Hero/p19/p404/p505/p206/"));
        RankTable.Load("");
        var entry = RankTable.Ranks[(RankTable.Type)19].RankingList[41];
        Assert.Equal(404u, entry.TotalPoints);
        Assert.Equal(505u, entry.Class);
        Assert.Equal((byte)206, entry.Level);
    }

    [Fact]
    public void PrematureEndOfFileFaultsOnDefaultTypeAfterPublishingEarlierRow()
    {
        using var files = new NativeRankFileScope();
        File.WriteAllText(files.RankPath, NativeRankFileScope.Text("Count=2", Row(7)));
        Assert.Throws<KeyNotFoundException>(() => RankTable.Load(""));
        Assert.Equal(7u, Assert.Single(RankTable.Ranks[(RankTable.Type)19].RankingList).Key);
        Assert.False(RankTable.Ranks.ContainsKey((RankTable.Type)0));
    }

    [Fact]
    public void InvalidOrOverflowingByteTypeFaultsAfterEarlierPublicationWithoutNormalizing()
    {
        using var files = new NativeRankFileScope();
        foreach (string type in new[] { "256", "bad", "255" })
        {
            RankTable.Ranks = new();
            files.Rows(Row(7), Row(8, type: type));
            Assert.Throws<KeyNotFoundException>(() => RankTable.Load(""));
            Assert.Equal(7u, Assert.Single(RankTable.Ranks[(RankTable.Type)19].RankingList).Key);
        }
    }

    [Fact]
    public void LoadReplacesDuplicateUidThenKeepsTopTenAndNativeStableTieOrder()
    {
        using var files = new NativeRankFileScope();
        string[] rows = Enumerable.Range(1, 12).Select(i => Row((uint)i, (uint)i)).Append(Row(5, 100, name: "Latest")).ToArray();
        files.Rows(rows);
        RankTable.Load("");
        var rank = RankTable.Ranks[(RankTable.Type)19];
        Assert.Equal(new uint[] { 5, 12, 11, 10, 9, 8, 7, 6, 4, 3 }, rank.RankingList.Keys);
        Assert.Equal("Latest", rank.RankingList[5].Name);
        RankTable.Ranks = new();
        files.Rows(Row(8, 7), Row(2, 7), Row(9, 7));
        RankTable.Load("");
        Assert.Equal(new uint[] { 8, 2, 9 }, RankTable.Ranks[(RankTable.Type)19].RankingList.Keys);
    }
}

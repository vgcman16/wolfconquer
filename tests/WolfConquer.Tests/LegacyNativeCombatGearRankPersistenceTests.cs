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
    public void RepeatedCompleteLoadReplacesAllRanksAndMissingReloadPreservesTheLatestGraph()
    {
        using var files = new NativeRankFileScope();
        files.Rows();
        RankTable.Load("");
        var ranks = RankTable.Ranks;
        var first = ranks[(RankTable.Type)19];
        files.Rows(Row(7, 800));
        RankTable.Load("unused");
        var latest = RankTable.Ranks;
        Assert.NotSame(ranks, latest);
        Assert.NotSame(first, latest[(RankTable.Type)19]);
        Assert.Equal(7u, Assert.Single(latest[(RankTable.Type)19].RankingList).Key);
        Assert.Empty(first.RankingList);
        LegacyNativeServerConfig.DbLocation = Path.Combine(files.Root, "missing") + Path.DirectorySeparatorChar;
        Assert.ThrowsAny<IOException>(() => RankTable.Load("unused"));
        Assert.Same(latest, RankTable.Ranks);
        Assert.Equal(7u, Assert.Single(latest[(RankTable.Type)19].RankingList).Key);
        Assert.Equal(new uint[] { 19, 20, 21, 22, 26, 27, 28, 29, 30, 33, 34, 35, 36, 37, 38, 39, 40, 42 },
            ranks.Keys.Select(type => (uint)type));
        Assert.All(ranks.Values, rank => { Assert.Equal(10, rank.MaxItems); Assert.Empty(rank.RankingList); });
    }

    [Fact]
    public void MissingFilePreservesAnExistingLaterRankWithoutPublishingEarlierDetachedOwners()
    {
        using var files = new NativeRankFileScope();
        var retained = new RankTable.Rank((RankTable.Type)20) { MaxItems = 77 };
        RankTable.Ranks.Add((RankTable.Type)20, retained);
        var prior = RankTable.Ranks;
        LegacyNativeServerConfig.DbLocation = Path.Combine(files.Root, "absent") + Path.DirectorySeparatorChar;
        Assert.ThrowsAny<IOException>(() => RankTable.Load("ignored"));
        Assert.Same(prior, RankTable.Ranks);
        Assert.Same(retained, RankTable.Ranks[(RankTable.Type)20]);
        Assert.Equal(77, retained.MaxItems);
        Assert.Equal(new uint[] { 20 }, RankTable.Ranks.Keys.Select(type => (uint)type));
    }

    [Fact]
    public void StandaloneRankLoadPreparesAllOwnersEvenWithFalseWireGateAndPreservesEscapedRanks()
    {
        using var files = new NativeRankFileScope();
        LegacyNativeCombatGearTable.m_Load = false;
        var existing = Entry(5);
        var rank = Place(existing, mapKey: 5);
        var oldRows = rank.RankingList;
        var ranks = RankTable.Ranks;
        files.Rows(Row(7, 800));
        RankTable.Load("ignored");
        Assert.NotSame(ranks, RankTable.Ranks);
        Assert.Equal(18, RankTable.Ranks.Count);
        Assert.NotSame(rank, RankTable.Ranks[(RankTable.Type)19]);
        Assert.Same(oldRows, rank.RankingList);
        Assert.Same(existing, Assert.Single(oldRows).Value);
        Assert.Equal(7u, Assert.Single(RankTable.Ranks[(RankTable.Type)19].RankingList).Key);
        Assert.Same(existing, oldRows[5]);
        Assert.False(LegacyNativeCombatGearTable.m_Load);
        Assert.False(LegacyNativeCombatGearTable.TryCaptureMailboxLoadProof(out var proof));
        Assert.Null(proof);
    }

    [Fact]
    public void ZeroCountRejectsUncountedPhysicalRowsAndCompleteZeroCountCreatesAllRanks()
    {
        using var files = new NativeRankFileScope();
        File.WriteAllText(files.RankPath, NativeRankFileScope.Text("Count=0", "not/a/valid/row"));
        var prior = RankTable.Ranks;
        Assert.Throws<InvalidDataException>(() => RankTable.Load(""));
        Assert.Same(prior, RankTable.Ranks);
        Assert.Empty(prior);
        files.Rows();
        RankTable.Load("");
        Assert.Equal(18, RankTable.Ranks.Count);
        Assert.All(RankTable.Ranks.Values, rank => Assert.Empty(rank.RankingList));
    }

    [Fact]
    public void HeaderMustBeExactAndEveryPhysicalRowMustBelongToItsDeclaredCount()
    {
        using var files = new NativeRankFileScope();
        var retained = Entry();
        var rank = Place(retained);
        var prior = RankTable.Ranks;
        foreach (string text in new[] { NativeRankFileScope.Text("anything=1=ignored", Row(7)),
            NativeRankFileScope.Text("Count=1", Row(7), "bad") })
        {
            File.WriteAllText(files.RankPath, text);
            Assert.Throws<InvalidDataException>(() => RankTable.Load(""));
            Assert.Same(prior, RankTable.Ranks);
            Assert.Same(rank, Assert.Single(prior).Value);
            Assert.Same(retained, Assert.Single(rank.RankingList).Value);
        }
    }

    [Fact]
    public void InvalidOverflowAndMissingNumericColumnsRefuseWithoutInventingDefaultRows()
    {
        using var files = new NativeRankFileScope();
        var retained = Entry();
        var rank = Place(retained);
        var prior = RankTable.Ranks;
        foreach ((string row, Type exceptionType) in new[]
        {
            ("4294967296//19/not-a-number/4294967296/256/-1/", typeof(InvalidDataException)),
            ("2/Short/19", typeof(InvalidDataException)),
            (FullRow.Replace("41/Hero/", "4294967296/Hero/", StringComparison.Ordinal), typeof(OverflowException)),
            (FullRow.Replace("/404/", "/bad/", StringComparison.Ordinal), typeof(FormatException)),
            (FullRow.Replace("/206/", "/256/", StringComparison.Ordinal), typeof(OverflowException)),
            (FullRow.Replace("/707/", "/-1/", StringComparison.Ordinal), typeof(OverflowException)),
        })
        {
            files.Rows(Row(7), row);
            Assert.Throws(exceptionType, () => RankTable.Load(""));
            Assert.Same(prior, RankTable.Ranks);
            Assert.Same(rank, Assert.Single(prior).Value);
            Assert.Same(retained, Assert.Single(rank.RankingList).Value);
        }
    }

    [Fact]
    public void HeaderAndRowParsingHonorCurrentCulturePositiveSign()
    {
        using var files = new NativeRankFileScope();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = culture;
        var fields = FullRow.TrimEnd('/').Split('/');
        for (int index = 0; index < fields.Length; index++)
            if (index is not (1 or 20)) fields[index] = "p" + fields[index];
        File.WriteAllText(files.RankPath, NativeRankFileScope.Text("Count=p1", string.Join('/', fields) + "/"));
        RankTable.Load("");
        var entry = RankTable.Ranks[(RankTable.Type)19].RankingList[41];
        Assert.Equal(404u, entry.TotalPoints);
        Assert.Equal(505u, entry.Class);
        Assert.Equal((byte)206, entry.Level);
    }

    [Fact]
    public void PrematureEndOfFileRejectsTheWholePreparedGraph()
    {
        using var files = new NativeRankFileScope();
        File.WriteAllText(files.RankPath, NativeRankFileScope.Text("Count=2", Row(7)));
        var prior = RankTable.Ranks;
        Assert.Throws<InvalidDataException>(() => RankTable.Load(""));
        Assert.Same(prior, RankTable.Ranks);
        Assert.Empty(prior);
        Assert.False(RankTable.Ranks.ContainsKey((RankTable.Type)0));
    }

    [Fact]
    public void InvalidUnknownOrOverflowingByteTypeRefusesWithoutEarlierPublication()
    {
        using var files = new NativeRankFileScope();
        foreach (string type in new[] { "256", "bad", "255" })
        {
            var prior = RankTable.Ranks;
            files.Rows(Row(7), Row(8, type: type));
            Type exceptionType = type == "256" ? typeof(OverflowException) : type == "bad" ? typeof(FormatException) : typeof(InvalidDataException);
            Assert.Throws(exceptionType, () => RankTable.Load(""));
            Assert.Same(prior, RankTable.Ranks);
            Assert.Empty(prior);
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

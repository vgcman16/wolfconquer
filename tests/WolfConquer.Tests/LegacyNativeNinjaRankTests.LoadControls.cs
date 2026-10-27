using System.Text;
using Ranking = WolfConquer.Core.Data.LegacyNativeNinjaRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaRankTests
{
    [Fact]
    public void ValidLoadPublishesDetachedOwnersAndPreservesStableTopTenAndRealQueries() => WithOwnedRankPersistence(files =>
    {
        uint[] points = [100, 100, 100, 90, 90, 80, 70, 60, 50, 40, 30, uint.MaxValue];
        files.Seed(Text(points.Select((score, index) => Row((uint)index + 1, score, "2", "R" + (index + 1))).ToArray()));
        var before = SeedPersistenceGraph();
        Ranking.Load();
        var rank = Ranking.Ranks[Ranking.Type.Water];
        Assert.Equal(new uint[] { 12, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, rank.RankingList.Keys);
        Assert.Equal(uint.MaxValue, rank.RankingList[12].TotalPoints);
        Assert.Same(rank.RankingList[12], Ranking.BestOf(Ranking.Type.Water));
        Assert.Same(rank.RankingList[1], Ranking.GetInfo(Ranking.Type.Water, 1));
        Assert.Equal(2U, Ranking.GetMyRank(Ranking.Type.Water, 1));
        Assert.Null(Ranking.GetInfo(Ranking.Type.Water, 10));
        Assert.Equal(0U, Ranking.GetMyRank(Ranking.Type.Water, 11));
        RequireFreshSixRanks(before);
    });

    [Fact]
    public void SameUidAcrossAllSixCategoriesIsValidAndTheSixtyRowInputBoundaryRetainsFirstTenTies() =>
        WithOwnedRankPersistence(files =>
        {
            var rows = Enumerable.Range(0, 6).SelectMany(category => Enumerable.Range(1, 10)
                .Select(uid => Row((uint)uid, 0, category.ToString(), "P" + uid))).ToArray();
            Assert.Equal(60, rows.Length);
            files.Seed(Text(rows));
            var before = SeedPersistenceGraph();
            Ranking.Load();
            RequireFreshSixRanks(before);
            foreach (var type in Enumerable.Range(0, 6).Select(value => (Ranking.Type)value))
            {
                Assert.Equal(Enumerable.Range(1, 10).Select(uid => (uint)uid), Ranking.Ranks[type].RankingList.Keys);
                Assert.All(Ranking.Ranks[type].RankingList.Values, row =>
                {
                    Assert.Equal(type, row.Type);
                    Assert.Equal(0U, row.TotalPoints); // No new persistence eligibility threshold.
                });
            }
        });

    [Fact]
    public void FullUnsignedWidthsAndByteLevelRemainRealValuesRatherThanDefaults() => WithOwnedRankPersistence(files =>
    {
        var fields = FullRow.Split('/');
        for (int index = 0; index < 21; index++)
            if (index is not 1 and not 2 and not 5 and not 20) fields[index] = "4294967295";
        fields[1] = "Wide"; fields[2] = "5"; fields[5] = "255";
        files.Seed(Text(string.Join('/', fields)));
        var before = SeedPersistenceGraph();
        Ranking.Load();
        var loaded = Assert.Single(Ranking.Ranks[Ranking.Type.Lightning].RankingList).Value;
        var values = Values(loaded);
        Assert.Equal("Wide", loaded.Name); Assert.Equal("Guild", loaded.GuildName);
        Assert.Equal((byte)255, loaded.Level); Assert.Equal(Ranking.Type.Lightning, loaded.Type);
        foreach (int index in Enumerable.Range(0, 21).Where(index => index is not 1 and not 2 and not 5 and not 20))
            Assert.Equal(uint.MaxValue, values[index]);
        RequireFreshSixRanks(before);
        Ranking.Save();
        files.Expect(Text(string.Join('/', fields)));
    });

    [Fact]
    public void Utf8BomUnicodeAndBlankNamesKeepExactTextAndCanonicalSavedBytes() => WithOwnedRankPersistence(files =>
    {
        var unicode = Row(41, 7, "2", "\u82F1\u96C4\U0001F409").Split('/'); unicode[20] = "\u4F1A";
        var blank = Row(42, 3, "1", "").Split('/'); blank[20] = "";
        var unicodeRow = string.Join('/', unicode); var blankRow = string.Join('/', blank);
        files.SeedBytes(new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(Text(unicodeRow, blankRow))).ToArray());
        var before = SeedPersistenceGraph();
        Ranking.Load();
        RequireFreshSixRanks(before);
        Assert.Equal("\u82F1\u96C4\U0001F409", Ranking.Ranks[Ranking.Type.Water].RankingList[41].Name);
        Assert.Equal("\u4F1A", Ranking.Ranks[Ranking.Type.Water].RankingList[41].GuildName);
        Assert.Equal("", Ranking.Ranks[Ranking.Type.Fire].RankingList[42].Name);
        Assert.Equal("", Ranking.Ranks[Ranking.Type.Fire].RankingList[42].GuildName);
        Ranking.Save();
        files.Expect(Text(blankRow, unicodeRow)); // Canonical six-category order; no BOM from the writer.
    });

    [Fact]
    public void Exactly4096CharacterPhysicalRowCanLoadAndSaveWithoutTruncation() => WithOwnedRankPersistence(files =>
    {
        var emptyName = Row(91, name: "");
        var row = Row(91, name: new string('B', 4096 - emptyName.Length));
        Assert.Equal(4096, row.Length);
        files.Seed(Text(row));
        var before = SeedPersistenceGraph();
        Ranking.Load();
        RequireFreshSixRanks(before);
        Assert.Equal(4096 - emptyName.Length, Ranking.Ranks[Ranking.Type.Overall].RankingList[91].Name.Length);
        Ranking.Save();
        files.Expect(Text(row));
    });
}

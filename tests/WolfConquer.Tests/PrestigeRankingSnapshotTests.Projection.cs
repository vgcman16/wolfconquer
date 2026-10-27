using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class PrestigeRankingSnapshotTests
{
    [Fact]
    public void CompleteSavedRowProjectsAllColumnsWithoutRecomputingStoredTotal()
    {
        var snapshot = PrestigeRankingSnapshot.Create([PrestigeRankingQueryTestData.Row()]);
        var entry = Assert.Single(snapshot.Entries);
        Assert.Equal(41U, entry.UID);
        Assert.Equal("Hero", entry.Name);
        Assert.Equal((PrestigeRankCategory)4, entry.Category);
        Assert.Equal(5051U, entry.Class);
        Assert.Equal((byte)140, entry.Level);
        Assert.Equal(7U, entry.Mesh);
        Assert.Equal(30_004U, entry.TotalPoints);
        Assert.Equal(8U, entry.HairStyle);
        Assert.Equal(9U, entry.Head);
        Assert.Equal(10U, entry.Garment);
        Assert.Equal(11U, entry.LeftWeapon);
        Assert.Equal(12U, entry.LeftWeaponAccessory);
        Assert.Equal(13U, entry.RightWeapon);
        Assert.Equal(14U, entry.RightWeaponAccessory);
        Assert.Equal(15U, entry.MountArmor);
        Assert.Equal(16U, entry.Armor);
        Assert.Equal(17U, entry.Wing);
        Assert.Equal(18U, entry.WingPlus);
        Assert.Equal(19U, entry.Title);
        Assert.Equal(20U, entry.Flag);
        Assert.Equal("Guild", entry.GuildName);
        Assert.Equal(Enumerable.Range(1001, 43).Select(value => (uint)value), entry.Points);
        Assert.Same(entry, snapshot.BestOfTheWorld);
        Assert.Same(entry, snapshot.GetInfo(41));
        Assert.Equal(1U, snapshot.GetMyRank((PrestigeRankCategory)0, 41));
        Assert.Equal(1U, snapshot.GetMyRank((PrestigeRankCategory)4, 41));
    }

    [Fact]
    public void ThresholdAndStableTiesUseCompletePersistedRowsAndKeepBlankGuildValid()
    {
        var snapshot = PrestigeRankingSnapshot.Create(
        [
            PrestigeRankingQueryTestData.Row(11, total: 19_999),
            PrestigeRankingQueryTestData.Row(17, total: 30_000, category: 3),
            PrestigeRankingQueryTestData.Row(19, total: 30_000, category: 4, guild: ""),
            PrestigeRankingQueryTestData.Row(23, total: 20_000, category: 4),
        ]);
        Assert.Equal(new uint[] { 11, 17, 19, 23 }, snapshot.Entries.Select(entry => entry.UID));
        Assert.Equal(new uint[] { 17, 19, 23 }, snapshot.WorldRanks.Select(entry => entry.UID));
        Assert.Equal(new uint[] { 19, 23 }, snapshot.GetRanks((PrestigeRankCategory)4).Select(entry => entry.UID));
        Assert.Equal(17U, snapshot.BestOfTheWorld!.UID);
        Assert.Equal("", snapshot.GetInfo(19)!.GuildName);
        Assert.Null(snapshot.GetInfo(11));
        Assert.Equal(0U, snapshot.GetMyRank((PrestigeRankCategory)0, 11));
        Assert.Equal(2U, snapshot.GetMyRank((PrestigeRankCategory)0, 19));
        Assert.Equal(1U, snapshot.GetMyRank((PrestigeRankCategory)4, 19));
        Assert.Equal(0U, snapshot.GetMyRank((PrestigeRankCategory)3, 19));
        Assert.Null(snapshot.GetInfo(uint.MaxValue));
    }

    [Fact]
    public void WorldAndClassLimitsAreAppliedAfterDescendingStableRanking()
    {
        var rows = Enumerable.Range(0, 110)
            .Select(index => PrestigeRankingQueryTestData.Row(1000 + index, total: (uint)(20_000 + index)))
            .ToArray();
        var snapshot = PrestigeRankingSnapshot.Create(rows);
        Assert.Equal(110, snapshot.Entries.Count);
        Assert.Equal(Enumerable.Range(1010, 100).Reverse().Select(value => (uint)value),
            snapshot.WorldRanks.Select(entry => entry.UID));
        Assert.Equal(Enumerable.Range(1080, 30).Reverse().Select(value => (uint)value),
            snapshot.GetRanks((PrestigeRankCategory)4).Select(entry => entry.UID));
        Assert.Equal(100U, snapshot.GetMyRank((PrestigeRankCategory)0, 1010));
        Assert.Equal(30U, snapshot.GetMyRank((PrestigeRankCategory)4, 1080));
        Assert.Equal(0U, snapshot.GetMyRank((PrestigeRankCategory)4, 1079));
        Assert.NotNull(snapshot.GetInfo(1010)); // Still retained by the world list.
        Assert.Null(snapshot.GetInfo(1009));
    }

    [Fact]
    public void AllElevenClassesHaveIndependentListsAndAnEmptyCatalogIsValid()
    {
        var snapshot = PrestigeRankingSnapshot.Create(Enumerable.Range(1, 11)
            .Select(category => PrestigeRankingQueryTestData.Row(100 + category, category: (uint)category)).ToArray());
        for (var category = 1; category <= 11; category++)
        {
            Assert.Equal((uint)(100 + category), Assert.Single(snapshot.GetRanks((PrestigeRankCategory)category)).UID);
            Assert.Equal(1U, snapshot.GetMyRank((PrestigeRankCategory)category, (uint)(100 + category)));
        }
        Assert.Equal(Enumerable.Range(101, 11).Select(value => (uint)value), snapshot.WorldRanks.Select(entry => entry.UID));
        var empty = PrestigeRankingSnapshot.Create([]);
        Assert.Empty(empty.Entries);
        Assert.Empty(empty.WorldRanks);
        Assert.Null(empty.BestOfTheWorld);
        Assert.Null(empty.GetInfo(41));
        Assert.Empty(empty.GetRanks((PrestigeRankCategory)4));
        Assert.Equal(0U, empty.GetMyRank((PrestigeRankCategory)4, 41));
    }

    [Fact]
    public void PublishedSnapshotDoesNotAliasMutableInputOrPointStorage()
    {
        var row = PrestigeRankingQueryTestData.Row();
        var fields = row.Fields.ToArray();
        var ranking = row.RankingFields.ToArray();
        var rows = new List<PrestigeRankEntry> { row with { Fields = fields, RankingFields = ranking } };
        var snapshot = PrestigeRankingSnapshot.Create(rows);
        fields[1] = "Mutated";
        fields[22] = "999";
        ranking[20] = "888";
        rows.Clear();
        var entry = Assert.Single(snapshot.Entries);
        Assert.Equal("Hero", entry.Name);
        Assert.Equal(1001U, entry.Points[0]);
        Assert.Equal(1043U, entry.Points[42]);
        Assert.Equal(41U, Assert.Single(snapshot.WorldRanks).UID);
        if (entry.Points is IList<uint> points)
            Assert.Throws<NotSupportedException>(() => points[0] = 123);
        if (snapshot.Entries is IList<PrestigeRankingEntry> entries)
            Assert.Throws<NotSupportedException>(() => entries.Clear());
    }

    [Fact]
    public void ValidUnsignedFieldMaximaRemainExactWithoutSignedNarrowing()
    {
        var row = PrestigeRankingQueryTestData.Row(int.MaxValue, uint.MaxValue, category: 11);
        foreach (var index in new[] { 3, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 }
            .Concat(Enumerable.Range(22, 43)))
            row = PrestigeRankingQueryTestData.ChangeField(row, index, "4294967295");
        row = PrestigeRankingQueryTestData.ChangeField(row, 4, "255");
        Assert.True(PrestigeRankingSnapshot.TryCreate([row], out var snapshot, out var rejection));
        Assert.Null(rejection);
        var entry = Assert.Single(snapshot!.WorldRanks);
        Assert.Equal((uint)int.MaxValue, entry.UID);
        Assert.Equal(uint.MaxValue, entry.Class);
        Assert.Equal(byte.MaxValue, entry.Level);
        Assert.Equal(uint.MaxValue, entry.TotalPoints);
        Assert.Equal(uint.MaxValue, entry.Mesh);
        Assert.Equal(uint.MaxValue, entry.Flag);
        Assert.All(entry.Points, value => Assert.Equal(uint.MaxValue, value));
    }

    [Fact]
    public void ValidNonAsciiAndSupplementaryScalarTextRemainsExact()
    {
        const string name = "H\u00E9ro\U0001F409";
        const string guild = "\u96EA\u9F8D";
        Assert.True(PrestigeRankingSnapshot.TryCreate(
            [PrestigeRankingQueryTestData.Row(name: name, guild: guild)], out var snapshot, out var rejection));
        Assert.Null(rejection);
        Assert.Equal(name, snapshot!.BestOfTheWorld!.Name);
        Assert.Equal(guild, snapshot.BestOfTheWorld.GuildName);
    }
}

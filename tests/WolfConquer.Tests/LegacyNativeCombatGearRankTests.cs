using WolfConquer.Core.Data;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using Ranks = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeCombatGearRankTests
{
    [Fact]
    public void EntryShallowCopyKeepsAllSourceFieldsWithoutSharingTheEntryItself()
    {
        var name = new string('N', 7);
        var guild = new string('G', 9);
        var entry = new Ranks.Entry { UID = 17, Type = Ranks.Type.ColdMoon, Class = 4001, Name = name,
            GuildName = guild, Level = 140, Mesh = uint.MaxValue, TotalPoints = 77, WeaponWar = 402 };
        var copy = entry.ShallowCopy();
        Assert.NotSame(entry, copy);
        foreach (var field in typeof(Ranks.Entry).GetFields())
            Assert.Equal(field.GetValue(entry), field.GetValue(copy));
        Assert.Same(name, copy.Name);
        Assert.Same(guild, copy.GuildName);
        copy.TotalPoints = 99;
        Assert.Equal(77u, entry.TotalPoints);
        Assert.Null(new Ranks.Entry().Name);
        Assert.Equal("", new Ranks.Entry().GuildName);
    }

    [Fact]
    public void RankDefaultZeroCapacityPublishesIntoOldListThenReplacesItWithEmptyList()
    {
        var rank = new Ranks.Rank(Ranks.Type.Overall_Warrior);
        var other = new Ranks.Rank(Ranks.Type.Overall_Warrior);
        var initial = rank.RankingList;
        var item = new Ranks.Entry { UID = 17, TotalPoints = 10 };
        Assert.Equal(0, rank.MaxItems);
        Assert.NotSame(rank.SynRoot, other.SynRoot);
        Assert.NotSame(initial, other.RankingList);

        rank.UpdateItem(item);

        Assert.Same(item, initial[17]);
        Assert.NotSame(initial, rank.RankingList);
        Assert.Empty(rank.RankingList);
    }

    [Fact]
    public void RankSortsCapsReplacesActualUidAndOnlyReplacesListWhenRemovalFindsTheKey()
    {
        var rank = new Ranks.Rank(Ranks.Type.Overall_Warrior) { MaxItems = 2 };
        var low = new Ranks.Entry { UID = 1, TotalPoints = 10 };
        var high = new Ranks.Entry { UID = 2, TotalPoints = 30 };
        var middle = new Ranks.Entry { UID = 3, TotalPoints = 20 };
        rank.UpdateItem(low);
        rank.UpdateItem(high);
        rank.UpdateItem(middle);
        Assert.Equal(new uint[] { 2, 3 }, rank.RankingList.Keys);
        Assert.Same(high, rank.RankingList[2]);
        Assert.Same(middle, rank.RankingList[3]);
        var replacement = new Ranks.Entry { UID = 3, TotalPoints = 40 };
        rank.UpdateItem(replacement);
        Assert.Equal(new uint[] { 3, 2 }, rank.RankingList.Keys);
        Assert.Same(replacement, rank.RankingList[3]);
        var before = rank.RankingList;
        rank.Remove(99);
        Assert.Same(before, rank.RankingList);
        rank.Remove(3);
        Assert.False(before.ContainsKey(3)); // Actual old list is mutated before publication.
        Assert.NotSame(before, rank.RankingList);
        Assert.Same(high, Assert.Single(rank.RankingList).Value);
    }

    [Fact]
    public void CreatePublishesEighteenSourceRanksAndRepeatedCreateFaultsWithoutClearingThem()
    {
        using var scope = new NativeCombatGearQueryScope();
        var dictionary = Ranks.Ranks;
        Ranks.Create();
        Assert.Same(dictionary, Ranks.Ranks);
        Assert.Equal(new uint[] { 19, 20, 21, 22, 26, 27, 28, 29, 30, 33, 34, 35, 36, 37, 38, 39, 40, 42 },
            dictionary.Keys.Select(value => (uint)value));
        Assert.Equal(18, dictionary.Values.Distinct().Count());
        Assert.All(dictionary, pair =>
        {
            Assert.Equal(pair.Key, pair.Value._Type);
            Assert.Equal(10, pair.Value.MaxItems);
            Assert.Empty(pair.Value.RankingList);
        });
        var before = dictionary.ToArray();
        Assert.Throws<ArgumentException>(() => Ranks.Create());
        Assert.Equal(before, dictionary.ToArray());
    }

    [Fact]
    public void QueriesSearchEntryUidsAndCurrentListOrderWithoutRepairingKeysOrSorting()
    {
        using var scope = new NativeCombatGearQueryScope();
        var rank = new Ranks.Rank(Ranks.Type.Overall_Warrior);
        Ranks.Ranks[rank._Type] = rank;
        var first = new Ranks.Entry { UID = 17, TotalPoints = 1 };
        var second = new Ranks.Entry { UID = 18, TotalPoints = 999 };
        rank.RankingList[999] = first;
        rank.RankingList[998] = second;
        Assert.Same(first, Ranks.GetInfo(rank._Type, 17));
        Assert.Null(Ranks.GetInfo(rank._Type, 999));
        Assert.Equal(1u, Ranks.GetMyRank(rank._Type, 17));
        Assert.Equal(2u, Ranks.GetMyRank(rank._Type, 18));
        Assert.Equal(0u, Ranks.GetMyRank(rank._Type, 999));
        Assert.Same(first, Ranks.BestOf(rank._Type));
        Assert.Throws<KeyNotFoundException>(() => Ranks.GetInfo(Ranks.Type.ColdMoon, 17));
        Assert.Throws<KeyNotFoundException>(() => Ranks.GetMyRank(Ranks.Type.ColdMoon, 17));
        Assert.Throws<KeyNotFoundException>(() => Ranks.BestOf(Ranks.Type.ColdMoon));
    }

    [Theory]
    [InlineData(2001u, 4)]
    [InlineData(4001u, 5)]
    [InlineData(13001u, 7)]
    [InlineData(14001u, 7)]
    [InlineData(7001u, 2)]
    public void UpdateRankClonesPerDestinationAndRemovesTheUidFromOtherClassFamilies(uint classId, int count)
    {
        using var scope = new NativeCombatGearQueryScope();
        Ranks.Create();
        foreach (var rank in Ranks.Ranks.Values)
        {
            rank.UpdateItem(new Ranks.Entry { UID = 17, TotalPoints = 1000 });
            rank.UpdateItem(new Ranks.Entry { UID = 99, TotalPoints = 99999 });
        }
        var gear = LegacyNativeCombatGearQueryTests.FamilyGear();
        var original = new Ranks.Entry { UID = 17, Name = "Actual", Class = classId,
            Type = Ranks.Type.Overall_Pirate, TotalPoints = 777, Mesh = 1234 };

        gear.UpdateRank(original);

        var entries = Ranks.Ranks.Values.Where(rank => rank.RankingList.ContainsKey(17))
            .Select(rank => rank.RankingList[17]).ToArray();
        Assert.Equal(count, entries.Length);
        Assert.Equal(count, entries.Distinct().Count());
        Assert.All(entries, entry =>
        {
            Assert.NotSame(original, entry);
            Assert.Equal(classId, entry.Class);
            Assert.Equal(1234u, entry.Mesh);
            Assert.Same(original.Name, entry.Name);
        });
        Assert.All(Ranks.Ranks.Values, rank => Assert.True(rank.RankingList.ContainsKey(99)));
        Assert.Equal(777u, original.TotalPoints);
        Assert.Equal(Ranks.Type.Overall_Pirate, original.Type);
        var overall = entries.Single(entry => entry.Type is Ranks.Type.Overall_Warrior or
            Ranks.Type.Overall_Archer or Ranks.Type.Overall_Taoist or Ranks.Type.Overall_Pirate);
        Assert.Equal(gear.GetScore(classId, Gear.TypeID.None), overall.TotalPoints);
    }

    [Fact]
    public void UpdateRankKeepsFirstPublicationBeforeMissingSecondRankFaultAndDoesNotRunRemovalTail()
    {
        using var scope = new NativeCombatGearQueryScope();
        var first = new Ranks.Rank(Ranks.Type.Overall_Warrior) { MaxItems = 10 };
        var other = new Ranks.Rank(Ranks.Type.Overall_Pirate) { MaxItems = 10 };
        Ranks.Ranks[first._Type] = first;
        Ranks.Ranks[other._Type] = other;
        var oldOther = new Ranks.Entry { UID = 17, Class = 7001, TotalPoints = 99 };
        other.UpdateItem(oldOther);
        var original = new Ranks.Entry { UID = 17, Class = 2001, TotalPoints = 777 };
        var gear = LegacyNativeCombatGearQueryTests.FamilyGear();

        Assert.Throws<KeyNotFoundException>(() => gear.UpdateRank(original));

        var stored = Assert.Single(first.RankingList).Value;
        Assert.NotSame(original, stored);
        Assert.Equal(250030u, stored.TotalPoints);
        Assert.Equal(Ranks.Type.Overall_Warrior, stored.Type);
        Assert.Same(oldOther, other.RankingList[17]);
        Assert.Equal(777u, original.TotalPoints);
    }
}

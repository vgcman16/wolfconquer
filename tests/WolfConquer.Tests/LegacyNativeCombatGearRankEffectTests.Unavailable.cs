using WolfConquer.Core.Data;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using Ranks = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearRankEffectTests
{
    [Theory]
    [InlineData(2001u)]
    [InlineData(4001u)]
    [InlineData(13001u)]
    [InlineData(14001u)]
    [InlineData(7001u)]
    public void IncompleteRequiredFamilyCannotPublishAWinningPrefix(uint classId)
    {
        using var scope = new RankEffectScope();
        var order = LookupOrder(classId);
        for (var next = 0; next < order.Length; next++)
        {
            var dictionary = Enum.GetValues<Ranks.Type>().ToDictionary(type => type, _ => (Ranks.Rank)null!);
            for (var earlier = 0; earlier < next; earlier++)
            {
                var rank = new Ranks.Rank(order[earlier]);
                rank.RankingList[8001] = new Ranks.Entry { UID = UserId, TotalPoints = 1 };
                dictionary[order[earlier]] = rank; // Earlier winning bonuses do not bypass later lookups.
            }
            dictionary.Remove(order[next]);
            Ranks.Ranks = dictionary;

            Assert.Throws<InvalidDataException>(() => Gear.GetValueRank(UserId, classId));

            Assert.Same(dictionary, Ranks.Ranks);
            foreach (var later in order.Skip(next + 1)) Assert.Null(dictionary[later]);
        }
    }

    [Fact]
    public void NullTailIsUnavailableEvenAfterFindingTheWinningUid()
    {
        using var scope = new RankEffectScope();
        var rank = Ranks.Ranks[Ranks.Type.Overall_Warrior];
        var winner = new Ranks.Entry { UID = UserId, TotalPoints = uint.MaxValue };
        rank.RankingList = new() { [8001] = winner, [8002] = null! };
        var sentinel = new Gear.VRANK { Magic = 123, MagicLevel = 456 };
        var result = sentinel;

        Assert.Throws<InvalidDataException>(() => result = Gear.GetValueRank(UserId, 2001));

        Assert.Same(sentinel, result);
        Assert.Equal((123, 456), ((int)result.Magic, (int)result.MagicLevel));
        Assert.Same(winner, rank.RankingList[8001]);
        Assert.Null(rank.RankingList[8002]);
        Assert.True(Ranks.Ranks.ContainsKey(Ranks.Type.Dragonhowl));
    }

    [Fact]
    public void MalformedRequiredOwnersAreUnavailableAndAreNotRepaired()
    {
        using var scope = new RankEffectScope();
        Ranks.Ranks = null!;
        Assert.Throws<InvalidDataException>(() => Gear.GetValueRank(UserId, 2001));
        Assert.Null(Ranks.Ranks);
        Ranks.Ranks = new() { [Ranks.Type.Overall_Warrior] = null! };
        Assert.Throws<InvalidDataException>(() => Gear.GetValueRank(UserId, 2001));
        Assert.Null(Ranks.Ranks[Ranks.Type.Overall_Warrior]);
        var rank = new Ranks.Rank(Ranks.Type.Overall_Warrior) { RankingList = null! };
        Ranks.Ranks[rank._Type] = rank;
        Assert.Throws<InvalidDataException>(() => Gear.GetValueRank(UserId, 2001));
        Assert.Null(rank.RankingList);
        rank.RankingList = new() { [123] = null! };
        Assert.Throws<InvalidDataException>(() => Gear.GetValueRank(UserId, 2001));
        Assert.Null(rank.RankingList[123]);
    }
}

using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using Ranks = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearRankEffectTests
{
    [Theory]
    [InlineData(2001U)]
    [InlineData(4001U)]
    [InlineData(13001U)]
    [InlineData(14001U)]
    [InlineData(7001U)]
    public void CompleteTailOfEveryRequiredFamilyIsValidatedBeforeReturningBonuses(uint classId)
    {
        using var scope = new RankEffectScope();
        scope.PlaceAll(1);
        var order = LookupOrder(classId);
        var last = Ranks.Ranks[order[^1]];
        var first = Assert.Single(last.RankingList).Value;
        last.RankingList[9000] = null!;
        var originalRoot = Ranks.Ranks;
        var originalList = last.RankingList;
        var sentinel = new Gear.VRANK { PAttack = 123, Magic = 456 };
        var result = sentinel;

        Assert.Throws<InvalidDataException>(() => result = Gear.GetValueRank(UserId, classId));

        Assert.Same(sentinel, result);
        Assert.Same(originalRoot, Ranks.Ranks);
        Assert.Same(originalList, last.RankingList);
        Assert.Same(first, last.RankingList[8001]);
        Assert.Null(last.RankingList[9000]);
    }

    [Theory]
    [InlineData(2001U)]
    [InlineData(4001U)]
    [InlineData(13001U)]
    [InlineData(14001U)]
    [InlineData(7001U)]
    public void WrongPhysicalRankFamilyCannotStandInForTheRequiredOwner(uint classId)
    {
        using var scope = new RankEffectScope();
        scope.PlaceAll(1);
        var type = LookupOrder(classId)[^1];
        var rank = Ranks.Ranks[type];
        rank._Type = (Ranks.Type)uint.MaxValue;

        Assert.Throws<InvalidDataException>(() => Gear.GetValueRank(UserId, classId));

        Assert.Same(rank, Ranks.Ranks[type]);
        Assert.Equal((Ranks.Type)uint.MaxValue, rank._Type);
        Assert.Equal(UserId, Assert.Single(rank.RankingList).Value.UID);
    }

    [Theory]
    [InlineData(2001U)]
    [InlineData(4001U)]
    [InlineData(13001U)]
    [InlineData(14001U)]
    [InlineData(7001U)]
    public void CompleteEmptyRanksAreValidNonmembershipNotUnavailableState(uint classId)
    {
        using var scope = new RankEffectScope();
        var before = Ranks.Ranks.ToArray();
        var first = Gear.GetValueRank(UserId, classId);
        var second = Gear.GetValueRank(UserId, classId);
        AssertZero(first);
        AssertZero(second);
        Assert.NotSame(first, second);
        Assert.Equal(before, Ranks.Ranks.ToArray());
        Assert.All(Ranks.Ranks.Values, rank => Assert.Empty(rank.RankingList));
    }

    [Fact]
    public void MissingSynchronizationOwnerIsUnavailableWithoutRepair()
    {
        using var scope = new RankEffectScope();
        var rank = Ranks.Ranks[Ranks.Type.Overall_Warrior];
        rank.SynRoot = null!;
        Assert.Throws<InvalidDataException>(() => Gear.GetValueRank(UserId, 2001));
        Assert.Null(rank.SynRoot);
        Assert.Empty(rank.RankingList);
    }

    [Fact]
    public void UnrelatedMalformedClassFamiliesDoNotDisableACompleteWarriorFamily()
    {
        using var scope = new RankEffectScope();
        scope.PlaceAll(1);
        Ranks.Ranks[Ranks.Type.WEEK_Pirate] = null!;
        Ranks.Ranks[Ranks.Type.Overall_Archer].RankingList = null!;
        var value = Gear.GetValueRank(UserId, 2001);
        Assert.NotNull(value);
        Assert.Equal((2d, 5d, 0d), (value.LukyStrike, value.Parry, value.CoreStrike));
        Assert.Null(Ranks.Ranks[Ranks.Type.WEEK_Pirate]);
        Assert.Null(Ranks.Ranks[Ranks.Type.Overall_Archer].RankingList);
    }

    [Fact]
    public void DetachedOrdinalResultDoesNotRereadLaterRowAliases()
    {
        using var scope = new RankEffectScope();
        scope.PlaceAll(1);
        var types = LookupOrder(2001);
        var captured = Ranks.CaptureEffectRanks(types, UserId);
        var originalType = types[0];
        types[0] = Ranks.Type.WEEK_Pirate;
        foreach (var rank in Ranks.Ranks.Values)
            foreach (var row in rank.RankingList.Values) row.UID = UserId + 1;
        Assert.Equal(1U, captured.GetMyRank(originalType));
        Assert.Throws<InvalidDataException>(() => captured.GetMyRank(Ranks.Type.WEEK_Pirate));
        AssertZero(Gear.GetValueRank(UserId, 2001));
    }

    [Fact]
    public void PhysicalFamilyCaptureDoesNotInvokeAnArmedDictionaryLookupComparer()
    {
        using var scope = new RankEffectScope();
        scope.PlaceAll(1);
        var comparer = new ArmedRankComparer();
        var root = new Dictionary<Ranks.Type, Ranks.Rank>(Ranks.Ranks, comparer);
        Ranks.Ranks = root;
        comparer.Armed = true;
        var value = Gear.GetValueRank(UserId, 2001);
        Assert.NotNull(value);
        Assert.Equal((2d, 5d), (value.LukyStrike, value.Parry));
        Assert.Equal(0, comparer.ArmedCalls);
        Assert.Same(root, Ranks.Ranks);
    }

    private sealed class ArmedRankComparer : IEqualityComparer<Ranks.Type>
    {
        internal bool Armed;
        internal int ArmedCalls;
        public bool Equals(Ranks.Type x, Ranks.Type y)
        {
            RefuseIfArmed();
            return x == y;
        }
        public int GetHashCode(Ranks.Type obj)
        {
            RefuseIfArmed();
            return (int)obj;
        }
        private void RefuseIfArmed()
        {
            if (!Armed) return;
            ArmedCalls++;
            throw new InvalidOperationException("An effect read must not invoke a dictionary lookup callback.");
        }
    }
}

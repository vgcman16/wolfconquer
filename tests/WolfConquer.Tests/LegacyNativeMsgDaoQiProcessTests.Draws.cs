using WolfConquer.Core.Data;
using Dao = WolfConquer.Core.World.LegacyNativeMsgDaoQi;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using JadeTable = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMsgDaoQiProcessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void First113DrawRecordsResultEvenIfMetadataPreventsActualBagInsertion(bool metadataExists)
    {
        using var scope = new Scope();
        if (metadataExists) Scope.Row(113);
        var random = new DrawRandom([], []);
        Gear.GetRandom = random;
        var items = new List<Dao.Item> { new() { Id = 777 } };
        var info = new Dao { Type = Dao.Types.DRAWS, Items = items };

        var frames = Dao.Process(info, scope.User);

        Assert.Equal(3, frames.Count);
        Assert.Equal(2UL, Read(frames[0], 12).Fields[4]);
        Assert.Equal(0UL, Read(frames[1], 3).Fields[3]);
        SameMessage(info, frames[2]);
        Assert.Equal(new uint[] { 777, 113 }, Read(frames[2], 4).Items);
        Assert.Same(items, info.Items);
        Assert.Equal(metadataExists, scope.User.BlackJadeBag.ContainsKey(113));
        Assert.Equal(2u, scope.User.Lottery);
        Assert.Equal(0u, scope.User.TaoistReiki);
        Assert.Equal(0u, info.Position);
        Assert.Empty(random.Bounds);
        Assert.Empty(random.RateValues);
        random.Drained(); // The special seed never emits AddBlackJade's returned ADDOBJ frame.
    }

    [Fact]
    public void FirstFreeTenSeeds113ThenConvertsNineDuplicateRuneTypesIntoUncheckedMasteryPoints()
    {
        using var scope = new Scope();
        Scope.Row(113, rune: 1);
        Scope.Row(213, rune: 1, points: 5);
        var random = new DrawRandom(Enumerable.Repeat(1, 9).ToArray(), new int[9]);
        Gear.GetRandom = random;
        scope.User.MasteryPoints = uint.MaxValue - 40;
        var info = new Dao { Type = Dao.Types.DRAWS, Position = 1 };
        var frames = Dao.Process(info, scope.User);
        Assert.Equal(3, frames.Count);
        Read(frames[0], 12);
        Read(frames[1], 3);
        Assert.Equal(new uint[] { 113 }.Concat(Enumerable.Repeat(213u, 9)), Read(frames[2], 4).Items);
        Assert.Equal(45u, info.Position);
        Assert.Equal(4u, scope.User.MasteryPoints);
        Assert.Equal(0u, scope.User.TaoistReiki);
        Assert.Equal(113u, Assert.Single(scope.User.BlackJadeBag).Value.Type);
        Assert.All(random.Bounds, bounds => Assert.Equal((0, 2), bounds));
        Assert.Equal(9, random.Bounds.Count);
        random.Drained();
    }

    [Theory]
    [InlineData(0u, 1500u, 1, false)]
    [InlineData(0u, 1500u, 1, true)]
    [InlineData(1u, 13500u, 10, false)]
    [InlineData(1u, 13500u, 10, true)]
    public void AlreadyUsedFreeFlagAppliesNativeSingleOrDiscountedTenCostBeforeAnyDraw(
        uint position, uint cost, int count, bool enough)
    {
        using var scope = new Scope();
        Scope.Row(113);
        Jade(scope.User, 113, 113);
        scope.User.Lottery = 2;
        scope.User.TaoistReiki = enough ? cost : cost - 1;
        var random = new DrawRandom(new int[enough ? count : 0], new int[enough ? count : 0]);
        Gear.GetRandom = random;
        var info = new Dao { Type = Dao.Types.DRAWS, Position = position };
        var items = info.Items;

        var frames = Dao.Process(info, scope.User);

        Assert.Equal(2u, scope.User.Lottery);
        Assert.Same(items, info.Items);
        if (enough)
        {
            Assert.Equal(2, frames.Count);
            Assert.Equal(0UL, Read(frames[0], 3).Fields[3]);
            Assert.Equal(Enumerable.Repeat(113u, count), Read(frames[1], 4).Items);
            Assert.Equal(0u, info.Position);
            Assert.Equal(0u, scope.User.TaoistReiki);
        }
        else
        {
            Assert.Empty(frames);
            Assert.Empty(items);
            Assert.Equal(position, info.Position);
            Assert.Equal(cost - 1, scope.User.TaoistReiki);
            Assert.Empty(random.Bounds);
        }
        random.Drained();
    }

    [Fact]
    public void SacredRateIsDividedByFourBeforeRetryAndDuplicateTypePointsAreAppliedOnlyOnSuccess()
    {
        using var scope = new Scope();
        Scope.Row(113, rune: 1);
        Scope.Row(213, rune: 1, points: 7, rate: 8, quality: JadeTable.Type.Sacred);
        Jade(scope.User, 113, 113);
        scope.User.Lottery = 2;
        scope.User.TaoistReiki = 1500;
        scope.User.MasteryPoints = 5;
        var random = new DrawRandom([1, 1], [2, 1]); // 8/4=2: equality fails, then1 succeeds.
        Gear.GetRandom = random;
        var info = new Dao { Type = Dao.Types.DRAWS };
        var frames = Dao.Process(info, scope.User);
        Assert.Equal(2, frames.Count);
        Read(frames[0], 3);
        Assert.Equal(new uint[] { 213 }, Read(frames[1], 4).Items);
        Assert.Equal(7u, info.Position);
        Assert.Equal(12u, scope.User.MasteryPoints);
        Assert.Single(scope.User.BlackJadeBag);
        Assert.Equal(new[] { 2, 1 }, random.RateValues);
        Assert.Equal(new[] { (0, 2), (0, 2) }, random.Bounds);
        random.Drained();
    }

    [Fact]
    public void ANewRuneTypeEmitsAddObjectBetweenBalanceAndFinalDrawAckAndKeepsItsActualBagReference()
    {
        using var scope = new Scope();
        Scope.Row(113, rune: 1);
        Scope.Row(213, rune: 2);
        Jade(scope.User, 113, 113);
        scope.User.Lottery = 2;
        scope.User.TaoistReiki = 2000;
        var random = new DrawRandom([1], [0]);
        Gear.GetRandom = random;
        var info = new Dao { Type = Dao.Types.DRAWS };
        var frames = Dao.Process(info, scope.User);
        Assert.Equal(3, frames.Count);
        Assert.Equal(500UL, Read(frames[0], 3).Fields[3]);
        Assert.Equal(new uint[] { 213 }, Read(frames[1], 7).Items);
        Assert.Equal(new uint[] { 213 }, Read(frames[2], 4).Items);
        Assert.Equal((213u, 0u), (scope.User.BlackJadeBag[213].Type, scope.User.BlackJadeBag[213].Data));
        Assert.Equal(0u, info.Position);
        Assert.Equal(0u, scope.User.MasteryPoints);
        random.Drained();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownDrawPositionUsesZeroLengthAndCostButStillPublishesFlagBalanceAndAck(bool usedFree)
    {
        using var scope = new Scope();
        scope.User.Lottery = usedFree ? 2u : 0u;
        scope.User.TaoistReiki = 77;
        scope.User.MasteryPoints = 88;
        var info = new Dao { Type = Dao.Types.DRAWS, Position = 99, Items = [new() { Id = 777 }] };
        var frames = Dao.Process(info, scope.User);
        Assert.Equal(usedFree ? 2 : 3, frames.Count);
        if (!usedFree) Read(frames[0], 12);
        Assert.Equal(77UL, Read(frames[^2], 3).Fields[3]);
        SameMessage(info, frames[^1]);
        Assert.Equal(new uint[] { 777 }, Read(frames[^1], 4).Items);
        Assert.Equal(0u, info.Position);
        Assert.Equal(88u, scope.User.MasteryPoints);
        Assert.Equal(77u, scope.User.TaoistReiki);
        Assert.Equal(2u, scope.User.Lottery);
        Assert.Empty(scope.User.BlackJadeBag);
    }

    [Fact]
    public void EmptySelectionTableFaultOccursAfterPaidCostDebitWithoutRepairOrLooping()
    {
        using var scope = new Scope();
        Jade(scope.User, 113, 113);
        scope.User.Lottery = 2;
        scope.User.TaoistReiki = 1500;
        scope.User.MasteryPoints = 23;
        var random = new DrawRandom([0], []);
        Gear.GetRandom = random;
        var info = new Dao { Type = Dao.Types.DRAWS };
        Assert.Throws<IndexOutOfRangeException>(() => Dao.Process(info, scope.User));
        Assert.Equal(0u, scope.User.TaoistReiki);
        Assert.Equal(23u, scope.User.MasteryPoints);
        Assert.Empty(info.Items);
        Assert.Empty(JadeTable.Info);
        Assert.Equal(new[] { (0, 0) }, random.Bounds);
        random.Drained();
    }

    [Fact]
    public void LaterNullDrawRowFaultRetainsFirstSeedBagAndMessageMutationButNotFinalPointsPosition()
    {
        using var scope = new Scope();
        Scope.Row(113);
        JadeTable.Info[213] = null!;
        scope.User.Lottery = 2;
        scope.User.TaoistReiki = 13500;
        scope.User.MasteryPoints = 23;
        var random = new DrawRandom([1], []);
        Gear.GetRandom = random;
        var info = new Dao { Type = Dao.Types.DRAWS, Position = 1 };
        var items = info.Items;
        Assert.Throws<NullReferenceException>(() => Dao.Process(info, scope.User));
        Assert.Equal(0u, scope.User.TaoistReiki);
        Assert.Equal(23u, scope.User.MasteryPoints);
        Assert.Equal(1u, info.Position);
        Assert.Same(items, info.Items);
        Assert.Equal(113u, Assert.Single(items).Id);
        Assert.Equal(113u, Assert.Single(scope.User.BlackJadeBag).Value.Type);
        random.Drained();
    }
}

using Dict = WolfConquer.Core.World.LegacyNativeMsgDictLottery;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using GearTable = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using JadeTable = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;
using Table = WolfConquer.Core.Data.LegacyNativeDictLotteryTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDictLotteryTests
{
    [Fact]
    public void NewJadeRewardUsesValue2AndTheRealBagAndRecordsFalseRatherThanExperience()
    {
        using var s = new Scope();
        Scope.Row(type: 2, value: 73);
        var metadata = new JadeTable.CQ { ID = 888, TypeRune = 7, Exp = 99 };
        JadeTable.Info[73] = metadata;
        var bag = s.User.BlackJadeBag;
        var info = new Dict();

        var frames = Dict.Process(info, s.User, 17, 0);

        Assert.Equal(4, frames.Count);
        Air(frames[1], 0);
        Assert.Equal(NativeCombatGearSupportFixture.Frame(7, item: 73), frames[2]);
        Ack(frames[3], 0, (41, false));
        Assert.Same(bag, s.User.BlackJadeBag);
        Assert.Equal(73u, Assert.Single(bag).Key);
        Assert.Equal(73u, bag[73].Type);
        Assert.Equal(0u, bag[73].Data);
        Assert.Same(metadata, bag[73].DBItem);
        Assert.Equal(0u, s.User.PirateExp);
    }

    [Fact]
    public void DuplicateJadeRuneConvertsToTenUncheckedExperienceAndRecordsTrue()
    {
        using var s = new Scope();
        Scope.Row(type: 2, value: 73);
        JadeTable.Info[73] = new() { TypeRune = 7 };
        JadeTable.Info[74] = new() { TypeRune = 7 };
        var retained = new Gear.BlackJade(74, 99);
        s.User.BlackJadeBag[999] = retained;
        s.User.PirateExp = uint.MaxValue - 5;
        var info = new Dict();

        var frames = Dict.Process(info, s.User, 17, 0);

        Assert.Equal(4, frames.Count);
        Pirate(frames[2], 4);
        Ack(frames[3], 0, (41, true));
        Assert.Equal(4u, s.User.PirateExp);
        Assert.Same(retained, Assert.Single(s.User.BlackJadeBag).Value);
        Assert.Equal(99u, retained.Data);
        Assert.False(s.User.BlackJadeBag.ContainsKey(73));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EligibleGearRowRecordsTrueOnlyWhenTheActualAddItemSucceeds(bool duplicate)
    {
        using var s = new Scope();
        Scope.Row(type: 3, value: 201);
        GearTable.Info[201] = new() { [1] = null! }; // Native level1 does not dereference this row.
        var retained = new Gear.Weapon { ItemID = Gear.TypeID.Bloodlust, Exp = 99 };
        if (duplicate) s.User.Items[Gear.TypeID.Dragonhowl] = retained;
        var control = Scope.EligibleGearRate();
        Assert.Equal(0, control.Next() % 10000);
        s.User.PirateExp = uint.MaxValue - 20;
        var info = new Dict();

        var frames = Dict.Process(info, s.User, 17, 0);

        Assert.Equal(4, frames.Count);
        Air(frames[1], 0);
        Ack(frames[3], 0, (41, !duplicate));
        Assert.Equal(control.Next(), Gear.GetRandom.Next()); // Exactly one native Rate draw.
        var weapon = Assert.Single(s.User.Items).Value;
        if (duplicate)
        {
            Assert.Same(retained, weapon);
            Assert.Equal(99u, weapon.Exp);
            Pirate(frames[2], 29);
            Assert.Equal(29u, s.User.PirateExp);
        }
        else
        {
            Assert.NotSame(retained, weapon);
            Assert.Equal(Gear.TypeID.Dragonhowl, weapon.ItemID);
            Assert.Equal(1u, weapon.Level);
            Assert.Equal(0u, weapon.Exp);
            Assert.Equal(0u, weapon.Equip);
            Assert.Equal(0u, weapon.Transform);
            Assert.Equal("080210111A1908C90110011800200028003000380040004800500058006000", Payload(frames[2], 2420));
            Assert.Equal(uint.MaxValue - 20, s.User.PirateExp);
        }
    }

    [Fact]
    public void RepeatedCallsUseCurrentMetadataOwnerAppendToRetainedListAndKeepPriorFrameImmutable()
    {
        using var s = new Scope();
        Scope.Row();
        var retained = new Dict.Lottery { Type = 67, Exp = true };
        var rows = new List<Dict.Lottery> { retained };
        var info = new Dict { Items2 = rows, Done = 99 };
        var first = Dict.Process(info, s.User, 17, 0);
        var firstAck = first[^1].ToArray();
        var firstAdded = info.Items2[1];
        Table.Info = new() { [123] = new() { Id = 42, Type = 1, Value2 = 3 } };
        s.User.AirPowers.Points = 405;

        var second = Dict.Process(info, s.User, 17, 0);

        Assert.Equal(3, second.Count);
        Air(second[0], 0);
        Pirate(second[1], 10);
        Ack(second[2], 0, (67, true), (41, false), (42, false));
        Assert.Same(rows, info.Items2);
        Assert.Same(retained, rows[0]);
        Assert.Same(firstAdded, rows[1]);
        Assert.NotSame(rows[1], rows[2]);
        Assert.Equal(firstAck, first[^1]);
        Ack(first[^1], 0, (67, true), (41, false));
        Assert.Equal(1u, info.Done);
        Assert.Equal(8u, s.User.Lottery);
    }

    [Fact]
    public void EmptyEligibleTableFaultsAfterPaidDebitWithoutClearingRetainedInfo()
    {
        using var s = new Scope();
        Scope.Row(type: 99);
        s.User.Lottery = 12;
        s.User.AirPowers.Points = 405;
        var retained = new Dict.Lottery { Type = 67, Exp = true };
        var info = new Dict { Done = 99, Items2 = [retained] };

        Assert.Throws<IndexOutOfRangeException>(() => Dict.Process(info, s.User, 17, 0));

        Assert.Equal(0u, s.User.AirPowers.Points);
        Assert.Equal(0u, s.User.PirateExp);
        Assert.Equal(99u, info.Done);
        Assert.Same(retained, Assert.Single(info.Items2));
        Assert.Equal(99u, Assert.Single(Table.Info).Value.Type);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullMetadataOwnerOrPredicateRowFaultOccursAfterFreeFlagBeforeReward(bool nullTable)
    {
        using var s = new Scope();
        if (nullTable) Table.Info = null!;
        else Table.Info[41] = null!;
        var info = new Dict { Done = 99 };

        Assert.Throws<NullReferenceException>(() => Dict.Process(info, s.User, 17, 0));

        Assert.Equal(8u, s.User.Lottery);
        Assert.Equal(0u, s.User.PirateExp);
        Assert.Equal(99u, info.Done);
        Assert.Empty(info.Items2);
        if (nullTable) Assert.Null(Table.Info);
        else Assert.Null(Table.Info[41]);
    }

    [Fact]
    public void NullResultListFaultPreservesAlreadyChargedPointsAndAwardedExperienceButNotDone()
    {
        using var s = new Scope();
        Scope.Row();
        s.User.Lottery = 12;
        s.User.AirPowers.Points = 405;
        var info = new Dict { Items2 = null!, Done = 99 };

        Assert.Throws<NullReferenceException>(() => Dict.Process(info, s.User, 17, 0));

        Assert.Null(info.Items2);
        Assert.Equal(99u, info.Done);
        Assert.Equal(0u, s.User.AirPowers.Points);
        Assert.Equal(7u, s.User.PirateExp);
        Assert.Equal(12u, s.User.Lottery);
    }

    [Fact]
    public void NullPowerOwnerFaultDoesNotRollBackFreeFlagSetBeforeAdmission()
    {
        using var s = new Scope();
        s.User.AirPowers = null!;
        var info = new Dict { Done = 99 };

        Assert.Throws<NullReferenceException>(() => Dict.Process(info, s.User, 17, 1));

        Assert.Equal(8u, s.User.Lottery);
        Assert.Null(s.User.AirPowers);
        Assert.Equal(99u, info.Done);
        Assert.Empty(info.Items2);
    }
}

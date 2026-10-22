using WolfConquer.Core.Data;
using Dao = WolfConquer.Core.World.LegacyNativeMsgDaoQi;
using GearTable = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using JadeTable = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeMsgDaoQiProcessTests
{
    [Fact]
    public void UnloadedGateReturnsNullBeforeDereferencingEitherArgument()
    {
        using var scope = new Scope();
        GearTable.m_Load = false;
        Assert.Null(Dao.Process(null!, null!));
        var info = new Dao { Done = 77 };
        Assert.Null(Dao.Process(info, null!));
        Assert.Equal(77u, info.Done);
        GearTable.m_Load = true;
        Assert.Throws<NullReferenceException>(() => Dao.Process(null!, scope.User));
        Assert.Throws<NullReferenceException>(() => Dao.Process(info, null!));
        Assert.Equal(77u, info.Done);
    }

    [Fact]
    public void InfoMutatesTheOriginalMessageButPreservesListsArraysAndUnrelatedFields()
    {
        using var scope = new Scope();
        var user = scope.User;
        (user.MasteryPoints, user.TaoistReiki, user.Lottery, user.PirateExp) = (11, 22, 33, 44);
        var item = new Dao.Item { Id = 99 };
        var items = new List<Dao.Item> { item };
        uint[] positions = [0, 7];
        var info = new Dao { Type = Dao.Types.INFO, ItemID = 55, Done = 66, Position = 77, Items = items, Positions = positions };
        var frames = Dao.Process(info, user);
        SameMessage(info, Assert.Single(frames));
        Assert.Equal((11u, 22u, 33u, 44u), (info.MasteryPoints, info.TaoistReiki, info.LOTTERY, info.PirateExp));
        Assert.Equal((6u, 9u, 1u, 3u), (info.Data5, info.Data6, info.Data8, info.Data10));
        Assert.Equal((55u, 66u, 77u), (info.ItemID, info.Done, info.Position));
        Assert.Same(items, info.Items);
        Assert.Same(item, info.Items[0]);
        Assert.Same(positions, info.Positions);
        item.Id = 123;
        positions[1] = 88;
        Assert.Equal(new uint[] { 99 }, Read(frames[0], 0).Items);
        Assert.Equal(new uint[] { 0, 7 }, Read(frames[0], 0).Positions);
    }

    [Fact]
    public void LoadItemAppendsEveryActualBagValueOnEveryCallWithoutClearingOrNormalizingKeys()
    {
        using var scope = new Scope();
        var first = Jade(scope.User, 900, 113);
        Jade(scope.User, 901, 213);
        Assert.True(scope.User.BlackJadeBag.TryAdd(902, first));
        var expected = scope.User.BlackJadeBag.Values.Select(jade => jade.Type).ToArray();
        var items = new List<Dao.Item> { new() { Id = 777 } };
        var info = new Dao { Type = Dao.Types.LOAD_ITEM, Items = items };
        var firstFrame = Assert.Single(Dao.Process(info, scope.User));
        Assert.Equal(new uint[] { 777 }.Concat(expected), Read(firstFrame, 1).Items);
        var secondFrame = Assert.Single(Dao.Process(info, scope.User));
        Assert.Equal(new uint[] { 777 }.Concat(expected).Concat(expected), Read(secondFrame, 1).Items);
        Assert.Same(items, info.Items);
        Assert.Same(first, scope.User.BlackJadeBag[900]);
        Assert.Same(first, scope.User.BlackJadeBag[902]);
        Assert.Equal(4, Read(firstFrame, 1).Items.Length);
    }

    [Theory]
    [InlineData(0u, 20u)]
    [InlineData(1u, 35u)]
    [InlineData(uint.MaxValue, 5u)]
    public void BuyUsesUncheckedPositionTimes15AndPublishesBalanceBeforeDoneAck(uint position, uint balance)
    {
        using var scope = new Scope();
        scope.User.TaoistReiki = 20;
        var info = new Dao { Type = Dao.Types.BUY, Position = position, TaoistReiki = 123, Done = 99 };
        var frames = Dao.Process(info, scope.User);
        Assert.Equal(2, frames.Count);
        Assert.Equal((ulong)balance, Read(frames[0], 3).Fields[3]);
        SameMessage(info, frames[1]);
        Assert.Equal(balance, scope.User.TaoistReiki);
        Assert.Equal(1u, info.Done);
        Assert.Equal(123u, info.TaoistReiki); // BUY does not refresh this field in the reused ACK object.
        Assert.Equal(position, info.Position);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(6u)]
    public void EquipStoresTheActualBagObjectAtOneBasedPositionAndIgnoresOtherRequestedItems(uint position)
    {
        using var scope = new Scope();
        var weapon = Weapon(scope.User);
        var jade = Jade(scope.User, 999, 113);
        var untouched = weapon.BlackJades.ToArray();
        var items = new List<Dao.Item> { new() { Id = 113 }, new() { Id = 213 } };
        uint[] positions = [99];
        var info = new Dao { Type = Dao.Types.EQUIP, ItemID = 201, Position = position, Items = items, Positions = positions };
        SameMessage(info, Assert.Single(Dao.Process(info, scope.User)));
        Assert.Same(jade, weapon.BlackJades[position - 1]);
        for (int index = 0; index < 6; index++) if (index != position - 1) Assert.Same(untouched[index], weapon.BlackJades[index]);
        Assert.Equal(1u, info.Done);
        Assert.Same(items, info.Items);
        Assert.Same(positions, info.Positions);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(6u)]
    public void UnEquipReplacesOnlySelectedSlotWithAFreshEmptyJadeWithoutRemovingBagOwner(uint position)
    {
        using var scope = new Scope();
        var weapon = Weapon(scope.User);
        var jade = Jade(scope.User, 999, 113);
        weapon.BlackJades[position - 1] = jade;
        var before = weapon.BlackJades.ToArray();
        var info = new Dao { Type = Dao.Types.UN_EQUIP, ItemID = 201, Position = position, Items = null! };
        SameMessage(info, Assert.Single(Dao.Process(info, scope.User)));
        Assert.NotSame(jade, weapon.BlackJades[position - 1]);
        Assert.Equal((0u, 0u), (weapon.BlackJades[position - 1].Type, weapon.BlackJades[position - 1].Data));
        Assert.Same(jade, scope.User.BlackJadeBag[999]);
        for (int index = 0; index < 6; index++) if (index != position - 1) Assert.Same(before[index], weapon.BlackJades[index]);
        Assert.Equal(1u, info.Done);
    }

    [Fact]
    public void ReplaceKeepsMissingSlotsAndSerializesSameInfoSeparatelyAfterEachSuccessfulAssignment()
    {
        using var scope = new Scope();
        var weapon = Weapon(scope.User);
        var first = Jade(scope.User, 900, 113);
        var third = Jade(scope.User, 901, 213);
        var unchanged = weapon.BlackJades[1];
        var items = new List<Dao.Item> { new() { Id = 113 }, new() { Id = 999 }, new() { Id = 213 } };
        var info = new Dao { Type = Dao.Types.REPLACE, ItemID = 201, Position = uint.MaxValue, Items = items };
        var frames = Dao.Process(info, scope.User);
        Assert.Equal(2, frames.Count);
        Assert.NotSame(frames[0], frames[1]);
        Assert.Equal(frames[0], frames[1]);
        SameMessage(info, frames[0]);
        SameMessage(info, frames[1]);
        Assert.Same(first, weapon.BlackJades[0]);
        Assert.Same(unchanged, weapon.BlackJades[1]);
        Assert.Same(third, weapon.BlackJades[2]);
        Assert.Same(items, info.Items);
        Assert.Equal(1u, info.Done);
        Assert.Equal(uint.MaxValue, info.Position); // REPLACE indexes the loop, not Position.
    }

    [Theory]
    [InlineData(113u, 213u)]
    [InlineData(899u, 999u)]
    [InlineData(uint.MaxValue, 99u)]
    public void PirateUpgradeMutatesActualTypeWithoutRekeyAndPublishesPointsBeforeUpgradeAck(uint id, uint upgraded)
    {
        using var scope = new Scope();
        Scope.Row(id, exp: 10);
        var jade = Jade(scope.User, 777, id);
        scope.User.PirateExp = 25;
        var info = new Dao { Type = Dao.Types.PIRATE_UPGRADE, ItemID = id, Done = 88 };
        var frames = Dao.Process(info, scope.User);
        Assert.Equal(2, frames.Count);
        Assert.Equal(15UL, Read(frames[0], 11).Fields[9]);
        SameMessage(info, frames[1]);
        Assert.Equal(upgraded, jade.Type);
        Assert.Equal(77u, jade.Data);
        Assert.Same(jade, scope.User.BlackJadeBag[777]);
        Assert.False(scope.User.BlackJadeBag.ContainsKey(upgraded));
        Assert.Equal(15u, scope.User.PirateExp);
        Assert.Equal(upgraded, info.PirateExp); // Native ACK carries new jade Type here, not remaining points.
        Assert.Equal(1u, info.Position);
        Assert.Equal(88u, info.Done);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("cap")]
    [InlineData("funds")]
    [InlineData("null-row")]
    [InlineData("null-table")]
    public void PirateUpgradeKeepsNativeConditionOrderAndDoesNotMutateOnRejectedOrFaultedChecks(string reason)
    {
        using var scope = new Scope();
        uint id = reason == "cap" ? 900u : 113u;
        var jade = Jade(scope.User, 777, id);
        Scope.Row(id, exp: 10);
        scope.User.PirateExp = 9;
        var info = new Dao { Type = Dao.Types.PIRATE_UPGRADE, ItemID = reason == "missing" ? 999u : id, Position = 77, PirateExp = 88 };
        if (reason is "missing" or "cap" or "null-table") JadeTable.Info = null!;
        if (reason == "null-row") JadeTable.Info[id] = null!;
        if (reason is "null-row" or "null-table")
            Assert.Throws<NullReferenceException>(() => Dao.Process(info, scope.User));
        else Assert.Empty(Dao.Process(info, scope.User));
        Assert.Equal(id, jade.Type);
        Assert.Equal(9u, scope.User.PirateExp);
        Assert.Equal((77u, 88u), (info.Position, info.PirateExp));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(-1)]
    [InlineData(256)]
    public void TaoistReikiAndDefaultBranchesReturnEmptyWithoutDereferencingNullUser(int type)
    {
        using var scope = new Scope();
        var info = new Dao { Type = (Dao.Types)type, Position = 77, Done = 88 };
        var items = info.Items;
        Assert.Empty(Dao.Process(info, null!));
        Assert.Equal((Dao.Types)type, info.Type);
        Assert.Equal((77u, 88u), (info.Position, info.Done));
        Assert.Same(items, info.Items);
    }
}

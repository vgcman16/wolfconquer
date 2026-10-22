using Dao = WolfConquer.Core.World.LegacyNativeMsgDaoQi;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMsgDaoQiProcessTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    public void MissingWeaponSkipsBadPositionAndNullRequestedItemsWithoutMutation(int type)
    {
        using var scope = new Scope();
        var info = new Dao { Type = (Dao.Types)type, ItemID = 201, Position = 0, Done = 77, Items = null! };
        Assert.Empty(Dao.Process(info, scope.User));
        Assert.Equal(77u, info.Done);
        Assert.Null(info.Items);
        Assert.Empty(scope.User.Items);
    }

    [Fact]
    public void EquipMissingJadeSkipsArrayIndexAndEmptyBagSkipsTheNullItemsPredicate()
    {
        using var scope = new Scope();
        var weapon = Weapon(scope.User);
        var before = weapon.BlackJades.ToArray();
        Jade(scope.User, 999, 113);
        var info = new Dao { Type = Dao.Types.EQUIP, ItemID = 201, Position = 0, Done = 77, Items = [new() { Id = 213 }] };
        Assert.Empty(Dao.Process(info, scope.User));
        Assert.Equal(77u, info.Done);
        Assert.Equal(before, weapon.BlackJades);
        scope.User.BlackJadeBag.Clear();
        info.Items = null!;
        Assert.Empty(Dao.Process(info, scope.User));
        Assert.Equal(77u, info.Done);
    }

    [Theory]
    [InlineData(5, 0u)]
    [InlineData(5, 7u)]
    [InlineData(6, 0u)]
    [InlineData(6, 7u)]
    public void EquipAndUnEquipPublishDoneBeforeNativeBadSlotFaultWithoutChangingAnySlot(int type, uint position)
    {
        using var scope = new Scope();
        var weapon = Weapon(scope.User);
        var before = weapon.BlackJades.ToArray();
        var jade = Jade(scope.User, 999, 113);
        var info = new Dao { Type = (Dao.Types)type, ItemID = 201, Position = position, Items = [new() { Id = 113 }] };
        Assert.Throws<IndexOutOfRangeException>(() => Dao.Process(info, scope.User));
        Assert.Equal(1u, info.Done);
        Assert.Equal(before, weapon.BlackJades);
        Assert.Same(jade, scope.User.BlackJadeBag[999]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EquipMissingFirstRequestedItemFaultsBeforeDoneAndSlotPublication(bool nullList)
    {
        using var scope = new Scope();
        var weapon = Weapon(scope.User);
        var before = weapon.BlackJades.ToArray();
        Jade(scope.User, 999, 113);
        var info = new Dao { Type = Dao.Types.EQUIP, ItemID = 201, Position = 1, Done = 77, Items = nullList ? null! : [] };
        if (nullList) Assert.Throws<NullReferenceException>(() => Dao.Process(info, scope.User));
        else Assert.Throws<ArgumentOutOfRangeException>(() => Dao.Process(info, scope.User));
        Assert.Equal(77u, info.Done);
        Assert.Equal(before, weapon.BlackJades);
    }

    [Fact]
    public void ReplaceSeventhMatchFaultKeepsSixEarlierAssignmentsAndDoneMutation()
    {
        using var scope = new Scope();
        var weapon = Weapon(scope.User);
        var jade = Jade(scope.User, 999, 113);
        var items = Enumerable.Range(0, 7).Select(_ => new Dao.Item { Id = 113 }).ToList();
        var info = new Dao { Type = Dao.Types.REPLACE, ItemID = 201, Items = items };
        Assert.Throws<IndexOutOfRangeException>(() => Dao.Process(info, scope.User));
        Assert.All(weapon.BlackJades, value => Assert.Same(jade, value));
        Assert.Equal(1u, info.Done);
        Assert.Same(items, info.Items);
        Assert.Equal(7, info.Items.Count);
    }

    [Fact]
    public void LoadItemNullBagValueFaultRetainsEarlierAppendedItemAndOriginalList()
    {
        using var scope = new Scope();
        Jade(scope.User, 11, 113);
        Jade(scope.User, 22, 213);
        var keys = scope.User.BlackJadeBag.ToArray().Select(pair => pair.Key).ToArray();
        scope.User.BlackJadeBag[keys[0]].Type = 113;
        scope.User.BlackJadeBag[keys[1]] = null!;
        Assert.Equal(113u, scope.User.BlackJadeBag.Values.First().Type); // Declared current enumeration prestate.
        var items = new List<Dao.Item> { new() { Id = 777 } };
        var info = new Dao { Type = Dao.Types.LOAD_ITEM, Items = items };
        Assert.Throws<NullReferenceException>(() => Dao.Process(info, scope.User));
        Assert.Same(items, info.Items);
        Assert.Equal(new uint[] { 777, 113 }, items.Select(item => item.Id));
        Assert.Null(scope.User.BlackJadeBag[keys[1]]);
    }

    [Fact]
    public void ReplaceNullItemsFaultsBeforeAnyAssignmentButDoesNotRepairTheList()
    {
        using var scope = new Scope();
        var weapon = Weapon(scope.User);
        var before = weapon.BlackJades.ToArray();
        var info = new Dao { Type = Dao.Types.REPLACE, ItemID = 201, Items = null!, Done = 77 };
        Assert.Throws<NullReferenceException>(() => Dao.Process(info, scope.User));
        Assert.Equal(before, weapon.BlackJades);
        Assert.Equal(77u, info.Done);
        Assert.Null(info.Items);
    }
}

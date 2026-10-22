using WolfConquer.Core.World;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredWeaponsTests
{
    [Fact]
    public Task SlotQueriesAndAppearanceUsePhysicalValuesWhileEquipUsesDictionaryKeys() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = scope.Item(Weapon.Blade, 2, Weapon.Sword); row.BitVector.Add(3);
        Assert.True(scope.Owner.ContainsWeapon(Weapon.Blade)); Assert.False(scope.Owner.ContainsWeapon(Weapon.Sword));
        Assert.Same(row, scope.Owner.TryGetItem(3)); Assert.Null(scope.Owner.TryGetItem(4));
        Assert.False(scope.Owner.Free(3)); Assert.True(scope.Owner.Free(4)); Assert.False(scope.Owner.FreeAppearance(0));
        Assert.True(scope.Owner.Show(Weapon.Blade, 7)); Assert.Equal((byte)0, row.AppearancePosition);
        row.Level = 3; Assert.True(scope.Owner.Show(Weapon.Blade, 7)); Assert.Equal((byte)7, row.AppearancePosition);
        Assert.False(scope.Owner.Show(Weapon.Sword, 9)); Assert.False(scope.Owner.FreeAppearance(7));
        Assert.True(scope.Owner.checkDuplicatedAppearance(Weapon.Sword, 99, out var duplicate)); Assert.Equal((byte)7, duplicate);
        Assert.True(scope.Owner.Hide(Weapon.Sword)); Assert.Equal((byte)0, row.AppearancePosition);
        Assert.True(scope.Owner.Hide((byte)0)); Assert.False(scope.Owner.Hide((byte)7));
        Assert.False(scope.Owner.checkDuplicatedAppearance(Weapon.Sword, 7, out duplicate)); Assert.Equal((byte)0, duplicate);
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(2, 4)]
    [InlineData(5, 9)]
    [InlineData(10, 16)]
    [InlineData(17, 25)]
    public Task DuplicationChecksEntireNativeBandAndReturnsFirstPhysicalMatch(int first, int requested) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = scope.Item(Weapon.Blade, 1, Weapon.Sword);
        row.BitVector.Add(first); row.BitVector.Add(requested);
        Assert.True(scope.Owner.checkDuplication(Weapon.Sword, requested, out var found)); Assert.Equal(first, found);
        Assert.False(scope.Owner.checkDuplication(Weapon.Blade, requested, out found)); Assert.Equal(0, found);
        Assert.False(scope.Owner.checkDuplication(Weapon.Sword, 1, out found)); Assert.Equal(0, found);
        Assert.False(scope.Owner.checkDuplication(Weapon.Sword, 26, out found)); Assert.Equal(0, found);
    });

    [Fact]
    public Task EquipAndUnequipSendExactRealFramesOnlyWhenTheBitActuallyChanges() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = scope.Item(Weapon.Blade);
        Assert.True(scope.Owner.Equip(Weapon.Blade, 2)); Assert.True(row.BitVector.Contain(2));
        Assert.Equal(Hex("0D0031090800109A0318022000"), Assert.Single(scope.Queue));
        Assert.True(scope.Owner.Equip(Weapon.Blade, 2)); Assert.Single(scope.Queue);
        Assert.False(scope.Owner.Equip(Weapon.Sword, 2)); Assert.Single(scope.Queue);
        Assert.True(scope.Owner.Unequip(2)); Assert.False(row.BitVector.Contain(2));
        Assert.Equal(Hex("0C0031090801100018022000"), scope.Queue.ElementAt(1));
        Assert.False(scope.Owner.Unequip(2)); Assert.Equal(2, scope.Queue.Count);
        Assert.True(scope.Owner.Equip(Weapon.Blade, 3, false)); Assert.Equal(2, scope.Queue.Count);
        Assert.True(row.BitVector.Contain(3));
    });

    [Fact]
    public Task SwapPublishesBothBitsAndNestedExchangeAndAllowsSameSlot() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var left = scope.Item(Weapon.Blade); var right = scope.Item(Weapon.Sword);
        left.BitVector.Add(2); right.BitVector.Add(3);
        Assert.False(scope.Owner.Swap(2, 4)); Assert.Empty(scope.Queue);
        Assert.True(scope.Owner.Swap(2, 3));
        Assert.False(left.BitVector.Contain(2)); Assert.True(left.BitVector.Contain(3));
        Assert.True(right.BitVector.Contain(2)); Assert.False(right.BitVector.Contain(3));
        Assert.Equal(Hex("1200310908021000180020003A0408021003"), Assert.Single(scope.Queue));
        Assert.True(scope.Owner.Swap(3, 3, false)); Assert.True(left.BitVector.Contain(3)); Assert.Single(scope.Queue);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task RecycleFaultOccursAfterEquipUnequipOrSwapBitMutation(int operation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var left = scope.Item(Weapon.Blade); var right = scope.Item(Weapon.Sword);
        if (operation != 0) left.BitVector.Add(2);
        right.BitVector.Add(3); scope.NullBin();
        if (operation == 0) Assert.Throws<NullReferenceException>(() => scope.Owner.Equip(Weapon.Blade, 2));
        else if (operation == 1) Assert.Throws<NullReferenceException>(() => scope.Owner.Unequip(2));
        else Assert.Throws<NullReferenceException>(() => scope.Owner.Swap(2, 3));
        scope.RestoreBin();
        Assert.Equal(operation == 0, left.BitVector.Contain(2));
        Assert.Equal(operation == 2, left.BitVector.Contain(3));
        Assert.Equal(operation == 2, right.BitVector.Contain(2));
        Assert.Equal(operation != 2, right.BitVector.Contain(3));
        Assert.Empty(scope.Queue); Assert.Equal(16, scope.Available);
    });

    [Fact]
    public Task InvalidBitmapIndexFaultsRatherThanRepairingStorage() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = scope.Item(Weapon.Blade);
        Assert.Throws<IndexOutOfRangeException>(() => scope.Owner.Equip(Weapon.Blade, 32, false));
        Assert.Throws<IndexOutOfRangeException>(() => scope.Owner.Free(-32));
        row.BitVector = null!;
        Assert.Throws<NullReferenceException>(() => scope.Owner.Free(1));
        Assert.Throws<NullReferenceException>(() => scope.Owner.TryGetItem(1));
        Assert.False(scope.Owner.checkDuplication(Weapon.Blade, 1, out var ignored)); Assert.Equal(0, ignored);
        Assert.Empty(scope.Queue);
    });
}

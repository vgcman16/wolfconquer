using WolfConquer.Core.World;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredWeaponsTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(-32)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(int.MaxValue)]
    public Task AnyRemainderInvalidFreeSlotCannotAuthorizeAnEquipOrMutateBits(int position) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = scope.Item(Weapon.Blade);
        row.BitVector.Add(3); uint bits = row.BitVector.bits[0];
        Assert.False(scope.Owner.Free(position));
        Assert.False(scope.Owner.Equip(Weapon.Blade, position, false));
        Assert.False(scope.Owner.Unequip(position, false));
        Assert.False(scope.Owner.Swap(position, 3, false)); Assert.Null(scope.Owner.TryGetItem(position));
        Assert.Equal(bits, row.BitVector.bits[0]); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task AnyRemainderUnknownRowsRefuseFreeQueriesWithoutAuthorizingDestructiveConsumers() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = scope.Item(Weapon.Blade);
        row.BitVector.Add(3); row.AppearancePosition = 17;
        scope.Owner.Objects.Add(Weapon.Sword, null!);
        Assert.False(scope.Owner.Free(3)); Assert.False(scope.Owner.Free(4));
        Assert.False(scope.Owner.FreeAppearance(17)); Assert.False(scope.Owner.FreeAppearance(18));
        Assert.False(scope.Owner.Unequip(3, false)); Assert.False(scope.Owner.Hide((byte)17));
        Assert.False(scope.Owner.Hide(Weapon.Blade)); Assert.False(scope.Owner.Equip(Weapon.Blade, 4, false));
        Assert.Same(row, scope.Owner.Objects[Weapon.Blade]); Assert.Null(scope.Owner.Objects[Weapon.Sword]);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task AnyRemainderFreeQueriesKeepPhysicalAliasAndBitmapBoundarySemantics() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = scope.Item(Weapon.Sword);
        row.BitVector.Add(31); row.AppearancePosition = byte.MaxValue;
        Assert.False(scope.Owner.Free(31)); Assert.True(scope.Owner.Free(30));
        Assert.False(scope.Owner.FreeAppearance(byte.MaxValue)); Assert.True(scope.Owner.FreeAppearance(1));
        row.BitVector = null!;
        Assert.False(scope.Owner.Free(30));
        // Appearance does not consult unrelated bitmap state.
        Assert.True(scope.Owner.FreeAppearance(1));
        Assert.Empty(scope.Queue);
    });
}

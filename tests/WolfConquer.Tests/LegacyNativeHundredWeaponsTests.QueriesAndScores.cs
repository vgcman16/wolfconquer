using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Attr = WolfConquer.Core.Data.LegacyNativeHundredWeaponsTable.AttributeType;
using HW = WolfConquer.Core.World.LegacyNativeHundredWeapons;
using Info = WolfConquer.Core.World.LegacyNativeHundredWeaponInfo;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredWeaponsTests
{
    [Fact]
    public Task ConstructorOwnsDistinctNativeCollectionsAndAcceptsNullWithoutClaimingMemberBirth() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var first = new HW(null!); var second = new HW(scope.Client);
        Assert.Null(typeof(HW).GetField("Owner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(first));
        Assert.Same(scope.Client, typeof(HW).GetField("Owner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(second));
        Assert.Empty(first.Objects); Assert.Empty(first.TODO_Spells); Assert.False(first.Unlocked);
        Assert.Equal(new byte[] { 2, 3 }, first.TriggerStamp.Keys); Assert.All(first.TriggerStamp.Values, value => Assert.Equal(0, value.Value));
        Assert.Equal(new byte[] { 2, 3 }, first.TriggerSeconds.Keys); Assert.All(first.TriggerSeconds.Values, value => Assert.Equal((byte)0, value));
        Assert.NotSame(first.Objects, second.Objects); Assert.NotSame(first.TriggerStamp, second.TriggerStamp);
        Assert.NotSame(first.TriggerSeconds, second.TriggerSeconds); Assert.NotSame(first.TODO_Spells, second.TODO_Spells);
        Assert.Same(scope.Owner, scope.Client.HundredWeapons);
        first.Objects = null!;
        Assert.Equal(0u, first.TotalScore); Assert.Equal(0u, first.PerfectionScore);
        Assert.Throws<NullReferenceException>(() => first.Valid());
    });

    [Theory]
    [InlineData(1000u, 40, 0, false)]
    [InlineData(1001u, 39, 0, false)]
    [InlineData(1001u, 40, 0, true)]
    [InlineData(1001u, 1, 1, true)]
    [InlineData(1051u, 40, 0, true)]
    [InlineData(1052u, 140, 1, false)]
    [InlineData(5001u, 140, 1, false)]
    public Task ValidUsesActualTrojanPromotionLevelAndRebirth(uint job, ushort level, byte reborn, bool expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.SetClass(job); scope.SetLevel(level); scope.Client.Player.Reborn = reborn;
        Assert.Equal(expected, scope.Owner.Valid());
        Assert.False(scope.Owner.Unlocked); // Valid does not depend on Unlocked.
    });

    [Fact]
    public Task ValidWeaponKeepsNineLiteralSubtypesAndInclusiveLevelsWithoutOwnerReads() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var owner = new HW(null!);
        Weapon[] valid = [Weapon.Blade, Weapon.Sword, Weapon.Club, Weapon.Whip, Weapon.Dagger, Weapon.Hook, Weapon.Axe, Weapon.Hammer, Weapon.Scepter];
        foreach (var subtype in valid)
        {
            Assert.False(owner.ValidWeapon(subtype, 0)); Assert.True(owner.ValidWeapon(subtype, 1));
            Assert.True(owner.ValidWeapon(subtype, 9)); Assert.False(owner.ValidWeapon(subtype, 10));
        }
        foreach (var subtype in new[] { Weapon.Boxing, Weapon.Backsword, Weapon.OceanDominator, (Weapon)ushort.MaxValue })
            Assert.False(owner.ValidWeapon(subtype, 4));
    });

    [Fact]
    public Task NewInfoKeepsNativeNullOwnersAndImplicitZeroValuesWithoutMetadataRepair() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = new Info();
        Assert.Equal(Weapon.Boxing, row.WeaponSubtype); Assert.Equal((byte)0, row.Level); Assert.Equal(0u, row.Progress);
        Assert.Equal((byte)0, row.AppearancePosition); Assert.Null(row.Attributes); Assert.Null(row.BitVector); Assert.Null(row.DBInfo);
        Assert.Throws<NullReferenceException>(() => row.Score); Assert.Throws<NullReferenceException>(() => row.DBSpell);
        Assert.Empty(scope.Metadata);
    });

    [Fact]
    public Task InfoGettersUsePhysicalSubtypeAndLevelAndLiveMetadataRatherThanDictionaryKey() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = scope.Item(Weapon.Sword, 1, Weapon.Blade);
        item.Attributes[Attr.Hitpoints] = 7;
        var meta = scope.Meta(Weapon.Blade, 1); meta.Attributes[Attr.Hitpoints] = 11;
        var magic = scope.Magic(12000, 2, 9);
        Assert.Same(meta, item.DBInfo); Assert.Same(magic, item.DBSpell); Assert.Equal(1090u, item.Score);
        meta.Attributes[Attr.Hitpoints] = -1; Assert.Equal(1030u, item.Score);
        item.Level = 2; Assert.Null(item.DBInfo); Assert.Throws<NullReferenceException>(() => item.Score);
        meta.Level = 2; Assert.Equal(2030u, item.Score);
        Assert.Throws<KeyNotFoundException>(() => item.DBSpell);
        var next = scope.Magic(12000, 3, 17); Assert.Same(next, item.DBSpell);
        scope.Metadata.Clear(); Assert.Null(item.DBInfo); Assert.Throws<NullReferenceException>(() => item.DBSpell);
    });

    [Fact]
    public Task ScorePreservesCheckedLinqSumsAndUncheckedArithmeticAroundThem() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = scope.Item(Weapon.Blade); var meta = scope.Meta(Weapon.Blade, 1);
        item.Attributes[Attr.Hitpoints] = int.MaxValue;
        Assert.Equal(2147484643u, item.Score);
        item.Attributes[Attr.PhysicalAttack] = 1;
        Assert.Throws<OverflowException>(() => item.Score);
        item.Attributes = Attributes(); meta.Attributes[Attr.Hitpoints] = int.MaxValue; meta.Attributes[Attr.PhysicalAttack] = 1;
        Assert.Throws<OverflowException>(() => item.Score);
        item.Attributes = null!; Assert.Throws<NullReferenceException>(() => item.Score);
    });

    [Fact]
    public Task TotalAndPerfectionUseRealUnsignedNarrowingAndPhysicalFirstSlot() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var first = scope.Item(Weapon.Blade); var second = scope.Item(Weapon.Sword);
        scope.Meta(Weapon.Blade, 1); scope.Meta(Weapon.Sword, 1);
        first.Attributes[Attr.Hitpoints] = second.Attributes[Attr.Hitpoints] = 858993259;
        first.BitVector.Add(1); scope.Owner.Unlocked = true;
        Assert.Equal(uint.MaxValue, first.Score); Assert.Equal(uint.MaxValue, second.Score);
        Assert.Equal(4294967294u, scope.Owner.TotalScore);
        Assert.Equal(42949672u, scope.Owner.PerfectionScore);
        scope.SetClass(1000); Assert.Equal(0u, scope.Owner.PerfectionScore);
        Assert.Equal(4294967294u, scope.Owner.TotalScore);
        scope.Owner.Unlocked = false; scope.Owner.Objects = null!;
        Assert.Equal(0u, scope.Owner.TotalScore); Assert.Equal(0u, scope.Owner.PerfectionScore);
    });
}

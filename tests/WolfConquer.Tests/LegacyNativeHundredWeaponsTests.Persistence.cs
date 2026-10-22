using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Attr = WolfConquer.Core.Data.LegacyNativeHundredWeaponsTable.AttributeType;
using HW = WolfConquer.Core.World.LegacyNativeHundredWeapons;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredWeaponsTests
{
    [Fact]
    public Task RealRecordRoundtripUsesPhysicalSubtypeRawBitsAndSignedAttributeRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.SetClass(1000); scope.Owner.Unlocked = true;
        var row = scope.Item(Weapon.Sword, 4, Weapon.Blade); row.Progress = uint.MaxValue; row.BitVector.bits[0] = 165;
        row.AppearancePosition = 7; row.Attributes = new() { { Attr.Hitpoints, -1 }, { Attr.PhysicalAttack, 12345 }, { (Attr)255, -7 } };
        const string line = "1/1/410/4/4294967295/165/7/3/1/-1/2/12345/255/-7/";
        Assert.Equal(line, scope.Owner.ToString());
        var fresh = new HW(scope.Client); fresh.Load(line);
        Assert.True(fresh.Unlocked); var loaded = Assert.Single(fresh.Objects).Value;
        Assert.True(fresh.Objects.ContainsKey(Weapon.Blade)); Assert.False(fresh.Objects.ContainsKey(Weapon.Sword));
        Assert.NotSame(row, loaded); Assert.NotSame(row.BitVector, loaded.BitVector); Assert.NotSame(row.Attributes, loaded.Attributes);
        Assert.Equal((byte)4, loaded.Level); Assert.Equal(uint.MaxValue, loaded.Progress); Assert.Equal(165u, loaded.BitVector.bits[0]);
        Assert.Equal((byte)7, loaded.AppearancePosition); Assert.Equal(-1, loaded.Attributes[Attr.Hitpoints]);
        Assert.Equal(12345, loaded.Attributes[Attr.PhysicalAttack]); Assert.Equal(-7, loaded.Attributes[(Attr)255]);
        Assert.Equal(line, fresh.ToString()); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2/1/410/1/0/0/0/0/")]
    public Task NonOneUnlockByteResetsOnlyFlagAndRetainsRowsBeforeFinalSpellValidation(string? line) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = scope.Item(Weapon.Blade); scope.Owner.Unlocked = true;
        scope.Owner.Load(line!);
        Assert.False(scope.Owner.Unlocked); Assert.Same(row, Assert.Single(scope.Owner.Objects).Value);
        Assert.Equal("0/", scope.Owner.ToString()); Assert.Empty(scope.Queue);
        scope.Owner.Objects = null!; Assert.Equal("0/", scope.Owner.ToString());
    });

    [Fact]
    public Task LoadReplacesDuplicateWeaponRowsButKeepsFirstDuplicateAttributeAndOtherOwners() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.SetClass(1000);
        var old = scope.Item(Weapon.Blade); var other = scope.Item(Weapon.Sword);
        var dictionary = scope.Owner.Objects;
        scope.Owner.Load("1/1/410/1/7/1/0/2/1/10/1/99/");
        var first = scope.Owner.Objects[Weapon.Blade]; Assert.NotSame(old, first);
        Assert.Equal(10, first.Attributes[Attr.Hitpoints]); Assert.Single(first.Attributes);
        scope.Owner.Load("1/1/410/2/8/2/3/1/2/-4/");
        var second = scope.Owner.Objects[Weapon.Blade];
        Assert.NotSame(first, second); Assert.Same(dictionary, scope.Owner.Objects); Assert.Same(other, scope.Owner.Objects[Weapon.Sword]);
        Assert.Equal((byte)2, second.Level); Assert.Equal(8u, second.Progress); Assert.Equal(2u, second.BitVector.bits[0]);
        Assert.Equal((byte)3, second.AppearancePosition); Assert.Equal(-4, second.Attributes[Attr.PhysicalAttack]);
        Assert.Equal(2, scope.Owner.Objects.Count); Assert.Equal(7u, first.Progress); Assert.Equal(10, first.Attributes[Attr.Hitpoints]);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task InvalidRowLevelIsNotClampedAndNegativeCountStillRunsFinalSpellCleanup() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.SetClass(1000);
        scope.Owner.Load("1/1/410/0/0/0/0/0/"); Assert.True(scope.Owner.Unlocked); Assert.Empty(scope.Owner.Objects);
        scope.Meta(Weapon.Blade, 1, 12000); scope.Magic(12000, 0); scope.Magic(12000, 1);
        scope.Spells.ClientSpells.TryAdd(12000, new LegacyNativeMsgSpell { ID = 12000, Level = 0 });
        scope.Owner.Load("1/-1/");
        Assert.True(scope.Owner.Unlocked); Assert.Empty(scope.Owner.Objects); Assert.Empty(scope.Spells.ClientSpells);
        Assert.Equal((ushort)2046, Opcode(Assert.Single(scope.Queue)));
    });

    [Fact]
    public Task LoadPublishesUnlockAndStoredRowBeforeLaterRealSpellOwnerFault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Meta(Weapon.Blade, 4); scope.Magic(12000, 5, 5);
        scope.Client.MySpells = null!;
        Assert.Throws<NullReferenceException>(() => scope.Owner.Load("1/1/410/4/7/1/0/1/1/9/"));
        Assert.True(scope.Owner.Unlocked); var row = Assert.Single(scope.Owner.Objects).Value;
        Assert.Equal((byte)4, row.Level); Assert.Equal(7u, row.Progress); Assert.Equal(9, row.Attributes[Attr.Hitpoints]);
        Assert.All(LegacyNativeHWRank.Ranks.Values, rank => Assert.Empty(rank.RankingList)); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task LoadAndSaveDoNotRepairNullStorageAndRetainEarlierUnlockedPublication() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var old = scope.Item(Weapon.Blade); var dictionary = scope.Owner.Objects;
        scope.Owner.Objects = null!;
        Assert.Throws<NullReferenceException>(() => scope.Owner.Load("1/1/410/1/0/0/0/0/"));
        Assert.True(scope.Owner.Unlocked); Assert.Null(scope.Owner.Objects); Assert.Same(old, dictionary[Weapon.Blade]);
        Assert.Throws<NullReferenceException>(() => scope.Owner.ToString());
        scope.Owner.Objects = dictionary; old.Attributes = null!;
        Assert.Throws<NullReferenceException>(() => scope.Owner.ToString()); Assert.Empty(scope.Queue);
    });
}

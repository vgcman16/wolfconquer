using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Sort = WolfConquer.Core.Data.LegacyNativeMagicType.MagicSort;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCanUseSpellTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task WeaponAdmissionConsumesOnlyTheExactRandomSpellBeforeTheLaterClientLookup(bool matches, bool hasSpell) => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        LegacyNativePoolMagic.WeaponSpells[410] = [100, 100];
        f.Player.RandomSpell = matches ? (ushort)100 : (ushort)99;
        f.Owner.OnSoulSpell = 999;
        f.Spell.UseSpellSoul = 1;
        if (!hasSpell) f.Native.Storage.ClientSpells.Clear();
        Assert.Equal(matches && hasSpell, f.Run(out var spell, out var row));
        Assert.Equal(matches ? (ushort)0 : (ushort)99, f.Player.RandomSpell);
        if (matches && hasSpell)
        {
            Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
            Assert.Equal((ushort)100, f.Owner.OnSoulSpell);
        }
        else
        {
            Assert.Null(spell); Assert.Null(row);
            Assert.Equal((ushort)999, f.Owner.OnSoulSpell);
        }
        Assert.Empty(f.Queue);
        Assert.Equal("", f.Log.ToString());
    });

    [Fact]
    public Task LookupUsesMutableStoredLevelAndReturnsTheSameRowsWithoutRequiringMatchingIds()
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Player.Map = 1039;
        f.Spell.ID = 777;
        f.Spell.UseSpellSoul = 4;
        f.Row.ID = 888;
        f.Row.Level = 99;
        Assert.True(f.Run(out var spell, out var row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.Equal((ushort)777, f.Owner.OnSoulSpell);
        f.Spell.Level = 3;
        Assert.False(f.Run(out spell, out row));
        Assert.Null(spell); Assert.Null(row);
        Assert.Equal((ushort)777, f.Owner.OnSoulSpell);
        f.Rows[3] = f.Row;
        f.Spell.UseSoul = (LegacyNativeMsgSpell.UseSpellSoulTyp)uint.MaxValue;
        Assert.True(f.Run(out spell, out row));
        Assert.Equal((ushort)0, f.Owner.OnSoulSpell);
    });

    [Theory]
    [InlineData((int)Sort.Attack)]
    [InlineData((int)Sort.DirectAttack)]
    public Task MissingRangeTargetClearsOutValuesBeforeSoulAndResourcesAreTouched(int sortValue) => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Row.Type = (Sort)sortValue;
        f.Row.UseStamina = 10;
        f.Spell.UseSpellSoul = 2;
        f.Owner.OnSoulSpell = 999;
        Assert.Null(f.Owner.Map);
        Assert.False(f.Run(out var spell, out var row));
        Assert.Null(spell); Assert.Null(row);
        Assert.Equal((ushort)999, f.Owner.OnSoulSpell);
        Assert.Equal((ushort)100, f.Player.Stamina);
        Assert.Empty(f.Queue);
    });

    [Theory]
    [InlineData(0)]
    [InlineData((int)Sort.Recruit)]
    [InlineData((int)Sort.Bomb)]
    public Task OtherSortsDoNotAccessAMissingViewOrRangeEvenWithZeroRange(int sortValue) => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Row.Type = (Sort)sortValue;
        f.Player.Map = 1039;
        f.Player.View = null!;
        f.Owner.Rune = null;
        Assert.True(f.Run(out var spell, out var row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.Null(f.Player.View);
        Assert.Empty(f.Queue);
    });

    [Theory]
    [InlineData((int)Sort.Attack)]
    [InlineData((int)Sort.DirectAttack)]
    public Task ActualRawMapRangeAdmissionThenUsesTheSameRowsAndResourceOwners(int sortValue) => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Owner.Map = new LegacyNativeGameMap(72, 72, 1002) { View = new LegacyNativeMapView(72, 72) };
        f.Player.X = f.Player.Y = 35;
        var target = new RangeTarget();
        Assert.True(f.Owner.Map.View.EnterMap(target));
        f.Row.Type = (Sort)sortValue;
        f.Row.Range = 1;
        f.Row.UseStamina = 1;
        Assert.True(f.Run(out var spell, out var row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.Equal((ushort)99, f.Player.Stamina);
        Assert.Equal(Update(17, LegacyMsgUpdateDataType.Stamina, 99), Assert.Single(f.Queue));
        f.Row.Range = 0;
        Assert.False(f.Run(out spell, out row));
        Assert.Null(spell); Assert.Null(row);
        Assert.Equal((ushort)99, f.Player.Stamina);
        Assert.Single(f.Queue);
    });

    [Theory]
    [InlineData("weapon-owner")]
    [InlineData("weapon-list")]
    [InlineData("spell-owner")]
    [InlineData("stored-row")]
    [InlineData("database-row")]
    public Task NativeCatchLogsAndClearsOutValuesWithoutRepairingMissingOwners(string missing) => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        if (missing == "weapon-owner") LegacyNativePoolMagic.WeaponSpells = null!;
        else if (missing == "weapon-list") LegacyNativePoolMagic.WeaponSpells[410] = null!;
        else if (missing == "spell-owner") f.Owner.MySpells = null;
        else if (missing == "stored-row") f.Native.Storage.ClientSpells[100] = null!;
        else f.Rows[2] = null!;
        Assert.False(f.Run(out var spell, out var row));
        Assert.Null(spell); Assert.Null(row);
        Assert.Contains("NullReferenceException", f.Log.ToString());
        Assert.Empty(f.Queue);
    });
}

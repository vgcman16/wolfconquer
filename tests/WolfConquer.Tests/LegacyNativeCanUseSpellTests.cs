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
    public Task WeaponAuthorizationIsConsumedOnlyByAcceptedAdmission(bool matches, bool hasSpell)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        LegacyNativePoolMagic.WeaponSpells[410] = [100, 100];
        f.Player.RandomSpell = matches ? (ushort)100 : (ushort)99;
        f.Owner.OnSoulSpell = 999;
        f.Spell.UseSpellSoul = 1;
        if (!hasSpell) f.Native.Storage.ClientSpells.Clear();
        var before = Capture(f);
        Assert.Equal(matches && hasSpell, f.Run(out var spell, out var row));
        if (matches && hasSpell)
        {
            Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
            Assert.Equal((ushort)100, f.Owner.OnSoulSpell);
            Assert.Equal((ushort)0, f.Player.RandomSpell);
        }
        else
        {
            Assert.Null(spell); Assert.Null(row);
            Unchanged(f, before);
        }
        Assert.Empty(f.Queue);
        Assert.Equal("", f.Log.ToString());
    });

    [Fact]
    public Task LookupUsesStoredLevelAndTheGenuineSixThousandNineMetadataAlias()
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Native.Storage.ClientSpells.Clear();
        f.Attack.SpellID = f.Spell.ID = 6009;
        f.Native.Storage.ClientSpells[6009] = f.Spell;
        f.Row.ID = 6010; // MagicType.Load: Add(6009, base[6010]).
        f.Player.Map = 1039;
        f.Spell.UseSpellSoul = 4;
        Assert.True(f.Run(out var spell, out var row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.Equal((ushort)6009, f.Owner.OnSoulSpell);
        f.Spell.Level = 3;
        var before = Capture(f);
        Assert.False(f.Run(out spell, out row));
        Assert.Null(spell); Assert.Null(row); Unchanged(f, before);
        f.Row.Level = 3;
        f.Rows[3] = f.Row;
        f.Spell.UseSoul = (LegacyNativeMsgSpell.UseSpellSoulTyp)uint.MaxValue;
        Assert.True(f.Run(out spell, out row));
        Assert.Equal((ushort)0, f.Owner.OnSoulSpell);
    });

    [Theory]
    [InlineData((int)Sort.Attack)]
    [InlineData((int)Sort.DirectAttack)]
    public Task MissingRangeTargetRejectsBeforeAnyAdmissionStateChanges(int sortValue)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Row.Type = (Sort)sortValue;
        f.Row.UseStamina = 10;
        f.Spell.UseSpellSoul = 2;
        f.Owner.OnSoulSpell = 999;
        var before = Capture(f);
        Assert.False(f.Run(out var spell, out var row));
        Assert.Null(spell); Assert.Null(row); Unchanged(f, before);
    });

    [Theory]
    [InlineData(0)]
    [InlineData((int)Sort.Recruit)]
    [InlineData((int)Sort.Bomb)]
    public Task OtherSortsDoNotRequireRangeViewOrZeroCostRuneState(int sortValue)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Row.Type = (Sort)sortValue;
        f.Player.View = null!;
        f.Owner.Rune = null;
        Assert.True(f.Run(out var spell, out var row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.Null(f.Player.View); Assert.Empty(f.Queue);
    });

    [Theory]
    [InlineData((int)Sort.Attack)]
    [InlineData((int)Sort.DirectAttack)]
    public Task ActualMapAndSobNpcRangeGateRetainsSuccessfulStaminaWire(int sortValue)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Owner.Map = new LegacyNativeGameMap(72, 72, 1002) { View = new LegacyNativeMapView(72, 72) };
        f.Player.X = f.Player.Y = 35;
        var target = new LegacyNativeSobNpcRuntime
        {
            UID = 123, X = 36, Y = 35, Map = 1002, ObjType = LegacyNativeMapObjectType.SobNpc
        };
        Assert.True(f.Owner.Map.View.EnterMap(target));
        f.Row.Type = (Sort)sortValue;
        f.Row.Range = 1;
        f.Row.UseStamina = 1;
        Assert.True(f.Run(out var spell, out var row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.Equal((ushort)99, f.Player.Stamina);
        Assert.Equal(Update(17, LegacyMsgUpdateDataType.Stamina, 99), Assert.Single(f.Queue));
        f.Row.Range = 0;
        var before = Capture(f);
        Assert.False(f.Run(out spell, out row));
        Assert.Null(spell); Assert.Null(row); Unchanged(f, before);
    });

    [Theory]
    [InlineData("weapon-owner")]
    [InlineData("weapon-list")]
    [InlineData("spell-owner")]
    [InlineData("stored-row")]
    [InlineData("database-row")]
    public Task MissingRequiredLookupStateRejectsCleanlyWithoutRepairOrWrites(string missing)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        if (missing == "weapon-owner") LegacyNativePoolMagic.WeaponSpells = null!;
        else if (missing == "weapon-list") LegacyNativePoolMagic.WeaponSpells[410] = null!;
        else if (missing == "spell-owner") f.Owner.MySpells = null;
        else if (missing == "stored-row") f.Native.Storage.ClientSpells[100] = null!;
        else f.Rows[2] = null!;
        var before = Capture(f);
        Assert.False(f.Run(out var spell, out var row));
        Assert.Null(spell); Assert.Null(row); Unchanged(f, before);
        Assert.Equal("", f.Log.ToString());
    });
}

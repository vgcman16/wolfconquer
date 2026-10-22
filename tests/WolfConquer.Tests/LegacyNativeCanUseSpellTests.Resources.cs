using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCanUseSpellTests
{
    [Fact]
    public Task InsufficientStaminaRetainsOutRowsAndTheEarlierSoulSelectionWithoutDebitingAnything() => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Row.UseStamina = 10;
        f.Player.Stamina = 9;
        f.Spell.UseSpellSoul = 1;
        Assert.False(f.Run(out var spell, out var row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.Equal((ushort)100, f.Owner.OnSoulSpell);
        Assert.Equal((ushort)9, f.Player.Stamina);
        Assert.Equal((ushort)100, f.Player.Mana);
        Assert.Empty(f.Queue);
        Assert.Equal("", f.Log.ToString());
    });

    [Theory]
    [InlineData(false, 10u)]
    [InlineData(true, 4u)]
    public Task ScurvyBombUsesDictionaryContainmentForItsSixStaminaSurcharge(bool registered, uint expected) => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Player.Stamina = 20;
        f.Row.UseStamina = 10;
        if (registered) f.Player.BitVector.TryAdd((int)Flag.ScurvyBomb, 5, false, 0);
        else f.Player.BitVector.Add((int)Flag.ScurvyBomb);
        Assert.True(f.Run(out var spell, out var row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.Equal((ushort)expected, f.Player.Stamina);
        Assert.Equal(Update(17, LegacyMsgUpdateDataType.Stamina, expected), Assert.Single(f.Queue));
    });

    [Fact]
    public Task MissingRuneIsStillReadForZeroManaAfterStaminaDebitAndSend() => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Row.UseStamina = 10;
        f.Owner.Rune = null;
        Assert.Equal((ushort)0, f.Row.UseMana);
        Assert.False(f.Run(out var spell, out var row));
        Assert.Null(spell); Assert.Null(row);
        Assert.Equal((ushort)90, f.Player.Stamina);
        Assert.Equal(Update(17, LegacyMsgUpdateDataType.Stamina, 90), Assert.Single(f.Queue));
        Assert.Contains("NullReferenceException", f.Log.ToString());
    });

    [Fact]
    public Task TrainingMapSkipsBothCostsAndRuneQueryButNotSoulOrCooldownGates() => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Player.Map = 1039;
        f.Player.Stamina = 0;
        f.Player.Mana = 0;
        f.Row.UseStamina = byte.MaxValue;
        f.Row.UseMana = ushort.MaxValue;
        f.Owner.Rune = null;
        f.Spell.UseSpellSoul = 3;
        Assert.True(f.Run(out var spell, out var row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.Equal((ushort)100, f.Owner.OnSoulSpell);
        Assert.Equal((ushort)0, f.Player.Stamina);
        Assert.Equal((ushort)0, f.Player.Mana);
        Assert.Empty(f.Queue);
        f.Row.ColdTime = 1000;
        f.Spell.ColdTime = DateTime.Now.AddMinutes(10);
        Assert.False(f.Run(out spell, out row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
    });

    [Theory]
    [InlineData((byte)1, 91)]
    [InlineData((byte)2, 86)]
    [InlineData((byte)3, 76)]
    [InlineData((byte)4, 61)]
    [InlineData((byte)7, 26)]
    [InlineData((byte)8, 16)]
    [InlineData((byte)9, 11)]
    [InlineData((byte)10, 0)]
    [InlineData((byte)11, -10)]
    public Task MpMasterKeepsSpecialPercentagesTruncationAndAboveHundredBehavior(byte level, int adjustedCost) => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.MpRune(level);
        f.Row.UseMana = 101;
        f.Player.Mana = 101;
        Assert.True(f.Run(out var spell, out var row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.Equal((ushort)(adjustedCost > 0 ? 101 - adjustedCost : 101), f.Player.Mana);
        Assert.Empty(f.Queue); // Native Mana's full setter sends only when FullLoading.
    });

    [Fact]
    public Task InsufficientManaDoesNotUndoEarlierStaminaAndRetainsBothOutRows() => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Row.UseStamina = 3;
        f.Row.UseMana = 101;
        Assert.False(f.Run(out var spell, out var row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.Equal((ushort)97, f.Player.Stamina);
        Assert.Equal((ushort)100, f.Player.Mana);
        Assert.Equal(Update(17, LegacyMsgUpdateDataType.Stamina, 97), Assert.Single(f.Queue));
    });

    [Fact]
    public Task FullLoadingManaSetterAndVerifiedPreserveEverySameValueSendAndResourceOrder() => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Player.Mana = 55;
        Assert.Empty(f.Queue);
        f.Owner.FullLoading = true;
        f.Player.Mana = 55;
        f.Player.Mana = 55;
        Assert.Equal(2, f.Queue.Count);
        Assert.All(f.Queue, frame => Assert.Equal(Update(17, LegacyMsgUpdateDataType.Mana, 55), frame));
        f.Queue.Clear();
        f.Row.UseStamina = 3;
        f.Row.UseMana = 5;
        Assert.True(f.Run(out _, out _));
        Assert.Equal(new[] { Update(17, LegacyMsgUpdateDataType.Stamina, 97), Update(17, LegacyMsgUpdateDataType.Mana, 50) },
            f.Queue.ToArray());
        Assert.Equal((ushort)50, f.Player.Mana);
    });
}

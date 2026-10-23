using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Fact]
    public Task AttackDeltasUseSameUnroundedOriginalDamageAndUnusedTargetMayBeNull() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Flag(Flag.Stigma); s.Client.Status.PhysicalPercent = 13;
        s.Player.doFocus = true; s.Player.IntensifyDamage = 250;
        Assert.Equal(2932u, s.Client.AjustAttack(1001, null));
        s.Player.OnAttackPotion = true; Assert.Equal(2932u, s.Client.AjustAttack(1001, s.Player));
        s.Player.BitVector.TryRemove((int)Flag.Stigma);
        Assert.Equal(2932u, s.Client.AjustAttack(1001, null)); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task AttackFocusBelowOneHundredKeepsNegativeDeltaAndPhysicalArithmeticWrap() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.doFocus = true; s.Player.IntensifyDamage = 50;
        Assert.Equal(500u, s.Client.AjustAttack(1001, null));
        s.Player.OnAttackPotion = true; s.Player.IntensifyDamage = 200; s.Client.Status.PhysicalPercent = 100;
        Assert.Equal(4294967293u, s.Client.AjustAttack(uint.MaxValue, null));
        Assert.Empty(s.Queue);
    });

    [Fact]
    public Task DefenseStacksIndependentOriginalBaseDeltasThenHookMoonUsesIntegerProduct() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Client.Status.Defence = 1001; s.Client.Gems[7] = 7;
        s.Flag(Flag.MagicShield); s.Flag(Flag.DragonSwing); s.Flag(Flag.DivineGuard);
        Assert.Equal(3423u, s.Client.AjustDefense);
        s.Flag(Flag.HookMoon); s.Player.HookMoonAttackedPower = 125;
        Assert.Equal(4278u, s.Client.AjustDefense);
        s.Player.OnDefensePotion = true; Assert.Equal(4278u, s.Client.AjustDefense);
        s.Player.BitVector.TryRemove((int)Flag.MagicShield); Assert.Equal(4278u, s.Client.AjustDefense);
        Assert.Empty(s.Queue);
    });

    [Fact]
    public Task HookMoonZeroAndOverflowAreNotRepairedAndMissingGemArrayFaultsFirst() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Client.Status.Defence = uint.MaxValue; s.Flag(Flag.HookMoon);
        Assert.Equal(0u, s.Client.AjustDefense);
        s.Player.HookMoonAttackedPower = 100;
        Assert.Equal(42949671u, s.Client.AjustDefense);
        s.Client.Gems = null!; s.Client.Player = null!;
        Assert.Throws<NullReferenceException>(() => { _ = s.Client.AjustDefense; });
    });

    [Theory]
    [InlineData(0, 10000)] [InlineData(1, 10000)] [InlineData(2, 5000)] [InlineData(255, 5000)]
    public Task DefenseTwoUsesLiteralRebornThreshold(byte reborn, int expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.Reborn = reborn; Assert.Equal(expected, s.Client.GetDefense2());
    });
}

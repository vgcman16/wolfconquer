using WolfConquer.Core.World;
using Key = WolfConquer.Core.World.LegacyNativeRoleStatus.StatuTyp;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Theory]
    [InlineData(0, 0, 101u)]
    [InlineData(0, 1, 101u)]
    [InlineData(0, 2, 112u)]
    [InlineData(1, 0, 102u)]
    [InlineData(1, 1, 102u)]
    [InlineData(1, 2, 114u)]
    [InlineData(2, 0, 103u)]
    [InlineData(2, 1, 103u)]
    [InlineData(2, 2, 116u)]
    [InlineData(3, 0, 104u)]
    [InlineData(3, 1, 104u)]
    [InlineData(3, 2, 118u)]
    [InlineData(4, 0, 105u)]
    [InlineData(4, 1, 105u)]
    [InlineData(4, 2, 120u)]
    [InlineData(5, 0, 106u)]
    [InlineData(5, 1, 106u)]
    [InlineData(5, 2, 122u)]
    [InlineData(6, 0, 107u)]
    [InlineData(6, 1, 107u)]
    [InlineData(6, 2, 124u)]
    [InlineData(7, 0, 108u)]
    [InlineData(7, 1, 108u)]
    [InlineData(7, 2, 126u)]
    [InlineData(8, 0, 109u)]
    [InlineData(8, 1, 109u)]
    [InlineData(8, 2, 128u)]
    [InlineData(9, 0, 110u)]
    [InlineData(9, 1, 110u)]
    [InlineData(9, 2, 130u)]
    [InlineData(10, 0, 111u)]
    [InlineData(10, 1, 111u)]
    [InlineData(10, 2, 132u)]
    [InlineData(11, 0, 112u)]
    [InlineData(11, 1, 112u)]
    [InlineData(11, 2, 134u)]
    public Task EveryExtraStatusUsesItsExactKeyAndNativeExpiredPolarity(int index, int state, uint expected)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.Map = 10137;
        var keys = new[] { Key.IncreasePStrike, Key.IncreaseMStrike, Key.IncreaseImunity, Key.IncreaseBreack,
            Key.IncreaseAntiBreack, Key.IncreaseFinalMAttack, Key.IncreaseFinalMDamage, Key.IncreaseFinalPAttack,
            Key.IncreaseFinalPDamage, Key.IncreaseMAttack, Key.IncreaseMaxHp, Key.IncreasePAttack };
        for (int i = 0; i < keys.Length; i++) s.Client.ExtraStatus![keys[i]] = Status((uint)(11 + i), true);
        s.Client.Status.CriticalStrike = 101; s.Client.Status.SkillCStrike = 102; s.Client.Status.Immunity = 103;
        s.Client.Status.Breakthrough = 104; s.Client.Status.Counteraction = 105; s.Client.Status.MagicDamageIncrease = 106;
        s.Client.Status.MagicDamageDecrease = 107; s.Client.Status.PhysicalDamageIncrease = 108;
        s.Client.Status.PhysicalDamageDecrease = 109; s.Client.Status.MagicAttack = 110; s.Client.Status.MaxHitpoints = 111;
        if (state == 0) s.Client.ExtraStatus!.TryRemove(keys[index], out _);
        if (state == 1) s.Client.ExtraStatus![keys[index]].Stamp = DateTime.Now.AddDays(1);
        uint value = index switch
        {
            0 => s.Client.AjustCriticalStrike(), 1 => s.Client.AjustMCriticalStrike(), 2 => s.Client.AjustImunity(),
            3 => s.Client.AjustBreakthrough(), 4 => s.Client.AjustAntiBreack(), 5 => s.Client.AjustMagicDamageIncrease(),
            6 => s.Client.AjustMagicDamageDecrease(), 7 => s.Client.AjustPhysicalDamageIncrease(),
            8 => s.Client.AjustPhysicalDamageDecrease(), 9 => s.Client.AjustMagicAttack(),
            10 => s.Client.AjustMaxHitpoints(), 11 => s.Client.AjustMaxAttack(112), _ => throw new ArgumentOutOfRangeException()
        };
        Assert.Equal(expected, value); Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(1002u)] [InlineData(10136u)] [InlineData(10138u)]
    public Task MapSpecificBonusesDoNotReadMissingStatusOwnerOutside10137(uint map) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.Map = map; s.Client.ExtraStatus = null;
        s.Client.Status.SkillCStrike = 23; s.Client.Status.Breakthrough = 31;
        Assert.Equal(23u, s.Client.AjustMCriticalStrike()); Assert.Equal(31u, s.Client.AjustBreakthrough());
        s.Client.Player = null!;
        Assert.Equal(23u, s.Client.AjustMCriticalStrike()); Assert.Equal(31u, s.Client.AjustBreakthrough());
    });

    [Fact]
    public Task MissingPhysicalExtraStatusOwnerFaultsAndMapGateDoesNotRepairIt() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Client.ExtraStatus = null; s.Player.Map = 10137;
        Assert.Throws<NullReferenceException>(() => s.Client.AjustCriticalStrike());
        Assert.Throws<NullReferenceException>(() => s.Client.AjustMCriticalStrike());
        Assert.Throws<NullReferenceException>(() => s.Client.AjustBreakthrough());
        Assert.Throws<NullReferenceException>(() => s.Client.AjustMaxAttack(100));
        Assert.Empty(s.Queue);
        var partial = new LegacyNativeGameClientRuntime(); Assert.Null(partial.ExtraStatus);
    });

    [Fact]
    public Task StatusUsesRawPowerAndNullConversionsStillFault() => NativePacketTestThread.Run(() =>
    {
        var expired = Status(uint.MaxValue, true); var future = Status(17, false);
        Assert.True(expired); Assert.False(future);
        Assert.Equal(uint.MaxValue, (uint)expired); Assert.Equal(17u, future.GetPower);
        LegacyNativeRoleStatus missing = null!;
        Assert.Throws<NullReferenceException>(() => { bool value = missing; });
        Assert.Throws<NullReferenceException>(() => { uint value = missing; });
        Assert.Equal(12, Enum.GetValues<Key>().Length);
        Assert.Equal(59, (byte)Key.IncreasePStrike); Assert.Equal(70, (byte)Key.IncreaseFinalMAttack);
    });

    [Fact]
    public Task ExtraPowerAdditionWrapsInsteadOfSaturating() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Client.Status.CriticalStrike = uint.MaxValue;
        s.Client.ExtraStatus![Key.IncreasePStrike] = Status(7, true);
        s.Client.ExtraStatus[Key.IncreasePAttack] = Status(9, true);
        Assert.Equal(6u, s.Client.AjustCriticalStrike()); Assert.Equal(8u, s.Client.AjustMaxAttack(uint.MaxValue));
    });
}

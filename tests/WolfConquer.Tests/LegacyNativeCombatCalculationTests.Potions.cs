using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Theory]
    [InlineData(false, 7)] [InlineData(true, 45)]
    [InlineData(false, int.MaxValue)] [InlineData(true, int.MaxValue)]
    public Task PotionWritersCommitTimestampAndRealFlagBeforeTruthfulDurationChat(bool defense, int minutes)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); DateTime before = DateTime.Now;
        if (defense) s.Player.ActiveDefensePotion(minutes); else s.Player.ActiveAttackPotion(minutes);
        DateTime after = DateTime.Now;
        var flag = defense ? Flag.MagicShield : Flag.Stigma;
        Assert.True(defense ? s.Player.OnDefensePotion : s.Player.OnAttackPotion);
        DateTime stamp = defense ? s.Player.OnDefensePotionStamp : s.Player.OnAttackPotionStamp;
        Assert.InRange(stamp, before.AddMinutes(minutes), after.AddMinutes(minutes));
        var entry = s.Player.BitVector.ArrayFlags[(int)flag];
        Assert.Equal(60, entry.Secounds); Assert.True(entry.RemoveOnDead); Assert.Equal(0, entry.InvokerSecouds);
        Assert.Equal(2, s.Queue.Count); byte[] status = s.Queue.Dequeue();
        Assert.Equal((ushort)2176, BitConverter.ToUInt16(status, 2));
        var talk = Talk(s.Queue.Dequeue());
        Assert.Equal((uint)LegacyNativeMsgMessage.ChatMode.Talk, talk.ChatType);
        Assert.Equal((uint)LegacyNativeMsgMessage.MsgColor.red, talk.Color);
        Assert.Equal(new[] { "SYSTEM", "ALL", "", defense ? $"Your defense will increase during the next {minutes} minutes." :
            $"Your attack will increase during the next {minutes} minutes.", "", "", "" }, talk.Content);
        Assert.False(defense ? s.Player.OnAttackPotion : s.Player.OnDefensePotion);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ExistingPotionFlagIsNotRefreshedButTimestampAndChatStillUpdate(bool defense) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var flag = defense ? Flag.MagicShield : Flag.Stigma;
        s.Player.BitVector.TryAdd((int)flag, 17, false, 3);
        var entry = s.Player.BitVector.ArrayFlags[(int)flag]; entry.Timer = DateTime.Now.AddDays(-1);
        DateTime timer = entry.Timer;
        if (defense) s.Player.ActiveDefensePotion(12); else s.Player.ActiveAttackPotion(12);
        Assert.Same(entry, s.Player.BitVector.ArrayFlags[(int)flag]); Assert.Equal(timer, entry.Timer);
        Assert.Equal(17, entry.Secounds); Assert.False(entry.RemoveOnDead);
        _ = Talk(Assert.Single(s.Queue));
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task InvalidNegativeDurationRefusesWithoutBooleanFlagOrChatMutation(bool defense) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); DateTime old = new(2001, 2, 3);
        s.Player.OnAttackPotionStamp = old; s.Player.OnDefensePotionStamp = old;
        if (defense) s.Player.ActiveDefensePotion(int.MinValue); else s.Player.ActiveAttackPotion(int.MinValue);
        Assert.False(s.Player.OnDefensePotion); Assert.False(s.Player.OnAttackPotion);
        Assert.Equal(old, defense ? s.Player.OnDefensePotionStamp : s.Player.OnAttackPotionStamp);
        Assert.Empty(s.Player.BitVector.ArrayFlags); Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task RecycleFaultRefusesBeforeTimestampBooleanFlagOrChat(bool defense) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.NullBin();
        DateTime attackStamp = s.Player.OnAttackPotionStamp, defenseStamp = s.Player.OnDefensePotionStamp;
        if (defense) s.Player.ActiveDefensePotion(4); else s.Player.ActiveAttackPotion(4);
        Assert.False(s.Player.OnDefensePotion); Assert.False(s.Player.OnAttackPotion);
        Assert.Equal(attackStamp, s.Player.OnAttackPotionStamp); Assert.Equal(defenseStamp, s.Player.OnDefensePotionStamp);
        Assert.False(s.Player.ContainFlag(defense ? Flag.MagicShield : Flag.Stigma)); Assert.Empty(s.Queue);
    });
}

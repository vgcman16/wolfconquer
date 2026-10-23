using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    [Theory]
    [InlineData(-1.0, false, false)] [InlineData(0.0, false, false)] [InlineData(double.NaN, false, false)]
    [InlineData(100.0, true, true)] [InlineData(double.PositiveInfinity, true, true)]
    public Task WholeToxinEraserUsesAttackedOwnerChanceAndCalculateBaseRngNotPool(double chance, bool admitted, bool draw) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Client.PerfectionStatus.ToxinEraser = chance;
        LegacyNativeAttackBase.MyRandom = new(53); var reference = new LegacyNativeSafeRandom(53); if (draw) reference.Next();
        LegacyNativePoolRandom.GetRandom = new(59); var poolReference = new LegacyNativeSafeRandom(59);
        Assert.Equal(admitted, LegacyNativePerfectionEffects.ToxinEraser(s.Player));
        if (admitted) Assert.Equal(Refine(17, 17, 1), Assert.Single(s.Queue)); else Assert.Empty(s.Queue);
        Assert.Equal(reference.Next(), LegacyNativeAttackBase.MyRandom.Next()); Assert.Equal(poolReference.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Fact]
    public Task WholeInvisibleArrowReadsAttackerChanceSendsToTargetViewAndDoesNotAlterRefSpellObject() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var queue = new Queue<byte[]>(); var target = s.Peer(queue); s.Client.PerfectionStatus.InvisibleArrow = 100;
        var original = new LegacyNativeMsgSpellAnimation.SpellObj(21, 123, LegacyNativeMsgAttackPacket.AttackEffect.CriticalStrike); var current = original;
        LegacyNativePerfectionEffects.InvisibleArrow(s.Player, target.Player, ref current);
        Assert.Same(original, current); Assert.Equal(123u, current.Damage); Assert.Equal(LegacyNativeMsgAttackPacket.AttackEffect.CriticalStrike, current.Effect);
        Assert.Equal(Refine(17, 17, 13), Assert.Single(queue)); Assert.Empty(s.Queue);
        s.Client.PerfectionStatus.InvisibleArrow = 0; current = null!;
        LegacyNativePerfectionEffects.InvisibleArrow(s.Player, null!, ref current); Assert.Null(current); Assert.Single(queue);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task WholeKillingFlashUsesActualPoolRateAndCreatesOnlySourceXpListWhenItWins(bool succeeds) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); int seed = SeedFor(10, succeeds); LegacyNativePoolRandom.GetRandom = new(seed); var reference = new LegacyNativeSafeRandom(seed); reference.Next();
        LegacyNativeAttackBase.MyRandom = new(67); var baseReference = new LegacyNativeSafeRandom(67);
        Proc("KillingFlash", s.Client); byte[][] frames = s.Queue.ToArray(); Assert.Equal(Refine(17, 17, 9), frames[0]);
        Assert.Equal(succeeds, s.Player.ContainFlag(Flag.XPList));
        if (succeeds) { Assert.Equal(3, frames.Length); Assert.Equal(NativeAddFlagTestFixture.Status(Flag.XPList), frames[1]); Assert.Equal(Scalar(17, LegacyNativeMsgUpdate.DataType.XPList, 1), frames[2]); }
        else Assert.Single(frames);
        Assert.Equal(reference.Next(), LegacyNativePoolRandom.GetRandom.Next()); Assert.Equal(baseReference.Next(), LegacyNativeAttackBase.MyRandom.Next());
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task KillingFlashDoesNotConsumePoolRateWhenAnExistingXpListOrActiveXpShortCircuits(bool xpList) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.BitVector.TryAdd((int)(xpList ? Flag.XPList : Flag.Cyclone), 9, false, 0);
        LegacyNativePoolRandom.GetRandom = new(71); var reference = new LegacyNativeSafeRandom(71);
        Proc("KillingFlash", s.Client); Assert.Equal(Refine(17, 17, 9), Assert.Single(s.Queue));
        Assert.Equal(reference.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task DivineGuardAlwaysCommitsTenMinuteStampAndSendsEffectThenGenuineFlagOnRateSuccess(bool succeeds) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); int seed = SeedFor(5, succeeds); LegacyNativePoolRandom.GetRandom = new(seed); var reference = new LegacyNativeSafeRandom(seed); reference.Next();
        DateTime before = DateTime.Now.AddMinutes(10); Proc("DivineGuard", s.Client); DateTime after = DateTime.Now.AddMinutes(10);
        Assert.InRange(s.Player.LastDivineGuard, before, after); Assert.Equal(succeeds, s.Player.ContainFlag(Flag.DivineGuard));
        byte[][] frames = s.Queue.ToArray(); Assert.Equal(Refine(17, 17, 11), frames[0]);
        if (succeeds)
        {
            Assert.Equal(3, frames.Length); Assert.Equal(NativeAddFlagTestFixture.Status(Flag.DivineGuard), frames[1]); Assert.Equal(Guard(17), frames[2]);
            var row = s.Player.BitVector.ArrayFlags[(int)Flag.DivineGuard]; Assert.Equal(15, row.Secounds); Assert.True(row.RemoveOnDead);
        }
        else Assert.Single(frames);
        Assert.Equal(reference.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Fact]
    public Task DivineGuardExistingFlagStillResetsStampButSkipsRateAndRetainsPriorRegistration() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.BitVector.TryAdd((int)Flag.DivineGuard, 9, false, 0); var row = s.Player.BitVector.ArrayFlags[(int)Flag.DivineGuard];
        s.Player.LastDivineGuard = DateTime.Now.AddDays(1); LegacyNativePoolRandom.GetRandom = new(73); var reference = new LegacyNativeSafeRandom(73);
        DateTime before = DateTime.Now.AddMinutes(10); Proc("DivineGuard", s.Client); DateTime after = DateTime.Now.AddMinutes(10);
        Assert.InRange(s.Player.LastDivineGuard, before, after); Assert.Same(row, s.Player.BitVector.ArrayFlags[(int)Flag.DivineGuard]); Assert.Equal(9, row.Secounds);
        Assert.Equal(Refine(17, 17, 11), Assert.Single(s.Queue)); Assert.Equal(reference.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Fact]
    public Task DivineGuardViewFaultRetainsEarlierStampAndDoesNotReachRateOrGrantFlag() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.View = null!; LegacyNativePoolRandom.GetRandom = new(79); var reference = new LegacyNativeSafeRandom(79);
        DateTime before = DateTime.Now.AddMinutes(10); Assert.Throws<NullReferenceException>(() => Proc("DivineGuard", s.Client)); DateTime after = DateTime.Now.AddMinutes(10);
        Assert.InRange(s.Player.LastDivineGuard, before, after); Assert.False(s.Player.ContainFlag(Flag.DivineGuard)); Assert.Empty(s.Queue);
        Assert.Equal(reference.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });
}

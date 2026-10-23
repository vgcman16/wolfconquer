using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public Task WholeWeaponShieldAndStigmaEffectsUseActualFlagsLiteralDurationAndNoAdmissionOwner(bool stigma, bool existing) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); Flag flag = stigma ? Flag.Stigma : Flag.MagicShield; ushort id = stigma ? (ushort)1095 : (ushort)1020;
        LegacyNativeStatusFlagsBigVector32.Flag? old = null;
        if (existing) { s.Player.BitVector.TryAdd((int)flag, 9, false, 0); old = s.Player.BitVector.ArrayFlags[(int)flag]; }
        s.Client.MySpells = null; s.Client.Equipment = null; s.Client.Rune = null;
        if (stigma) LegacyNativeEffectStigma.Execute(null!, s.Client, s.Packet, null!);
        else LegacyNativeEffectShield.Execute(null!, s.Client, s.Packet, null!);
        byte[][] frames = s.Queue.ToArray(); Assert.Equal(existing ? new ushort[] { 2239 } : new ushort[] { 2176, 2239 }, frames.Select(Opcode));
        byte[][] targets = existing ? [] : [LegacyNativeSpellAnimationTests.Target(17, 0, true, 0)];
        Assert.Equal(LegacyNativeSpellAnimationTests.Frame(LegacyNativeSpellAnimationTests.Payload(17, 0, 32, 42, id, 4, 0, 0, 0, targets)), frames.Last());
        var current = s.Player.BitVector.ArrayFlags[(int)flag];
        if (existing) { Assert.Same(old, current); Assert.Equal(9, current.Secounds); Assert.False(current.RemoveOnDead); }
        else { Assert.Equal(stigma ? 90 : 40, current.Secounds); Assert.True(current.RemoveOnDead); Assert.Equal(NativeAddFlagTestFixture.Status(flag), frames[0]); }
        Assert.Equal((ushort)100, s.Player.Stamina); Assert.False(s.Client.FullLoading);
    });

    [Fact]
    public Task WeaponEffectRejectsMissingAnimationPoolBeforeFlagOrCallerPublication() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        Assert.True(s.Player.BitVector.TryAdd((int)Flag.TeamLeader, 31, true, 3));
        var retained = s.Player.BitVector.ArrayFlags[(int)Flag.TeamLeader];
        PrepareEffectResultCallerPacket(s); var before = CaptureEffectResultState(s, retained);
        LegacyNativePoolClients.GamePoll = null;
        LegacyNativeEffectShield.Execute(null!, s.Client, s.Packet, null!);
        Assert.False(s.Player.ContainFlag(Flag.MagicShield));
        AssertEffectResultUnchanged(s, retained, before);
    });

    [Theory]
    [InlineData(0, true)] [InlineData(99, true)] [InlineData(100, false)] [InlineData(150, false)]
    public Task WholeLightOfStaminaRestoresOnlyBelowHundredAndUsesActualStatusPacket(ushort stamina, bool restored) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.Stamina = stamina;
        Proc("LightOfStamina", s.Client);
        Assert.Equal(restored ? (ushort)100 : stamina, s.Player.Stamina);
        byte[][] frames = s.Queue.ToArray();
        if (restored) { Assert.Equal(2, frames.Length); Assert.Equal(Refine(17, 17, 7), frames[0]); Assert.Equal(Scalar(17, LegacyNativeMsgUpdate.DataType.Stamina, 100), frames[1]); }
        else Assert.Empty(frames);
    });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public Task WholeMirrorOfSinGrantsXpAvailabilityOnlyWithoutActiveXpOrExistingXpList(int mode) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        if (mode != 0) s.Player.BitVector.TryAdd((int)(mode == 1 ? Flag.Cyclone : Flag.XPList), 9, false, 0);
        Proc("MirrorOfSin", s.Player);
        byte[][] frames = s.Queue.ToArray(); Assert.Equal(Refine(17, 17, 10), frames[0]);
        if (mode == 0)
        {
            Assert.Equal(3, frames.Length); Assert.Equal(NativeAddFlagTestFixture.Status(Flag.XPList), frames[1]);
            Assert.Equal(Scalar(17, LegacyNativeMsgUpdate.DataType.XPList, 1), frames[2]);
            var row = s.Player.BitVector.ArrayFlags[(int)Flag.XPList]; Assert.Equal(20, row.Secounds); Assert.True(row.RemoveOnDead);
        }
        else { Assert.Single(frames); if (mode == 2) Assert.Equal(9, s.Player.BitVector.ArrayFlags[(int)Flag.XPList].Secounds); }
    });

    [Fact]
    public Task MirrorKeepsEarlierRefinementFrameWhenActualVectorReadFailsWithoutInventingARecoveryFlag() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.BitVector = null!;
        Assert.Throws<NullReferenceException>(() => Proc("MirrorOfSin", s.Player));
        Assert.Equal(Refine(17, 17, 10), Assert.Single(s.Queue)); Assert.Null(s.Player.BitVector);
    });

    [Fact]
    public Task CalmWindSendsTheRealEffectAndOriginalDrainingTouchReallyHasNoBodyOrOwnerRead() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); Proc("CalmWind", s.Client); Assert.Equal(Refine(17, 17, 4), Assert.Single(s.Queue));
        Proc("DrainingTouch", null!); Assert.Single(s.Queue); Assert.Empty(s.Player.BitVector.GetFlags());
    });
}

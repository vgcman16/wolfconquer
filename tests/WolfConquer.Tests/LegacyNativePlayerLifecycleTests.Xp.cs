using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerLifecycleTests
{
    [Theory]
    [InlineData(0)] [InlineData(19)] [InlineData(20)] [InlineData(21)] [InlineData(65535)]
    public Task WholeXpCounterCommitsExactUInt16AndOnlyTwentyAddsRealGoal(ushort value) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.XPCount = value; Assert.Equal(value, s.Player.XPCount);
        Assert.Equal(value == 20, s.Client.HeroRewards!.ContainGoal(103)); Assert.Empty(s.Queue);
        if (value == 20)
        {
            var item = s.Client.HeroRewards.ArrayGoals[103]; item.Claim = 4; s.Player.XPCount = 20;
            Assert.Same(item, s.Client.HeroRewards.ArrayGoals[103]); Assert.Equal(4, item.Claim);
        }
    });

    [Fact]
    public Task XpCounterMissingRewardOwnerFaultsAfterCommitAndOtherValuesDoNotReadIt() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Client.HeroRewards = null; s.Player.XPCount = 19;
        Assert.Throws<NullReferenceException>(() => s.Player.XPCount = 20); Assert.Equal(20, s.Player.XPCount);
        s.Player.XPCount = 21; Assert.Equal(21, s.Player.XPCount); Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(17, 5, 22)] [InlineData(17, -20, -3)] [InlineData(int.MaxValue, 1, int.MinValue)]
    public Task DurationUsesRealSelectedFlagAndKeepsSignedExtensionAndOverflow(int before, short delta, int expected)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Flag(Flag.Cyclone, before); s.Flag(Flag.Superman, 99);
        var entry = s.Player.BitVector.ArrayFlags[(int)Flag.Cyclone]; DateTime timer = entry.Timer;
        s.Player.IncreaseXPDuration(delta); Assert.Equal(expected, entry.Secounds); Assert.Equal(timer, entry.Timer);
        Assert.Equal(99, s.Player.BitVector.ArrayFlags[(int)Flag.Superman].Secounds); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task DurationAbsentSkillDoesNothingButPhysicalNormalRowStillGetsExtended() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.IncreaseXPDuration(4); Assert.Empty(s.Player.BitVector.ArrayFlags);
        s.Flag(Flag.Normal, 8); s.Player.IncreaseXPDuration(4);
        Assert.Equal(12, s.Player.BitVector.ArrayFlags[(int)Flag.Normal].Secounds); Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(0x17, true)] [InlineData(0x12, true)] [InlineData(0x96, false)] [InlineData(0x70, false)]
    public Task UpdateXpSkillExtendsOnlyCycloneAndSupermanWithActualUsedFlag(int flagValue, bool changed)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var flag = (Flag)flagValue; s.Flag(flag);
        s.Player.UseXPSpell = flag; var entry = s.Player.BitVector.ArrayFlags[flagValue];
        entry.Timer = DateTime.Now.AddDays(-1); DateTime timer = entry.Timer;
        s.Player.UpdateXpSkill(); Assert.Equal(changed ? 18 : 17, entry.Secounds); Assert.Equal(timer, entry.Timer);
        s.Player.BitVector.TryRemove(flagValue); s.Player.UpdateXpSkill(); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task UpdateXpSkillUsesNativeTwentySecondCeilingResetAndDoesNotInventMissingFlag() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.UseXPSpell = Flag.Cyclone; s.Player.UpdateXpSkill(); Assert.Empty(s.Player.BitVector.ArrayFlags);
        s.Flag(Flag.Cyclone, 30); var entry = s.Player.BitVector.ArrayFlags[(int)Flag.Cyclone];
        entry.Timer = DateTime.Now.AddMinutes(1); DateTime before = DateTime.Now;
        s.Player.UpdateXpSkill(); Assert.Equal(20, entry.Secounds); Assert.InRange(entry.Timer, before, DateTime.Now);
    });

    [Fact]
    public Task ScreenXpSkillUsesWholeInteractionOnActualTargetOnlyAndNormalDoesNotReadTarget() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.SendScrennXPSkill(null!); Assert.Empty(s.Queue);
        var targetQueue = new Queue<byte[]>(); var peer = NativeSpellTestFixture.CreateOwner(targetQueue);
        s.Flag(Flag.FatalStrike); s.Player.X = 300; s.Player.Y = 400; s.Player.KillCounter = 65535;
        s.Player.SendScrennXPSkill(peer.Player); Assert.Empty(s.Queue);
        byte[] frame = Assert.Single(targetQueue); Assert.Equal((ushort)2135, BitConverter.ToUInt16(frame, 2));
        using var packet = new WolfConquer.Core.Networking.LegacyNativePacket(frame);
        var query = packet.ProtoBufferDeserialize(new LegacyNativeInteractQuery());
        Assert.Equal(s.Player.UID, query.UID); Assert.Equal(0u, query.OpponentUID); Assert.Equal((ushort)14, query.AtkType);
        Assert.Equal(1, query.KilledMonster); Assert.Equal((ushort)65535, query.SpellLevel);
        Assert.Equal((ushort)300, query.X); Assert.Equal((ushort)400, query.Y); Assert.Equal(0, query.Damage);
        Assert.Throws<NullReferenceException>(() => s.Player.SendScrennXPSkill(null!));
    });
}

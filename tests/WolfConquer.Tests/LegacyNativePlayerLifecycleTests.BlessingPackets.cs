using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerLifecycleTests
{
    [Theory]
    [InlineData(0u, false)] [InlineData(601u, true)] [InlineData(1039u, true)] [InlineData(1002u, false)]
    public Task WholeBlessingPacketPublishesFlagThenScalarTrainingAndBlessEffect(uint map, bool training)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.Map = map; s.Player.HeavenBlessing = 10; DateTime before = DateTime.Now;
        s.Player.CreateHeavenBlessPacket(s.Packet, true); DateTime after = DateTime.Now;
        Assert.InRange(s.Player.ReceivePointsOnlineTraining, before.AddMinutes(1), after.AddMinutes(1));
        Assert.InRange(s.Player.OnlineTrainingTime, before.AddMinutes(10), after.AddMinutes(10));
        var flag = s.Player.BitVector.ArrayFlags[(int)Flag.HeavenBlessing];
        Assert.Equal(2592000, flag.Secounds); Assert.False(flag.RemoveOnDead);
        Assert.Equal(training ? 5 : 4, s.Queue.Count); var frames = s.Queue.ToArray();
        Assert.Equal((ushort)2176, BitConverter.ToUInt16(frames[0], 2));
        Assert.Equal(Update(17, 17, 10), frames[1]); Assert.Equal(Update(17, 31), frames[2]);
        if (training) Assert.Equal(Update(17, 31, 1), frames[3]);
        Assert.Equal("1100F407110000000A010500626C657373", Convert.ToHexString(frames[^1]));
    });

    [Fact]
    public Task ExistingBlessFlagAndFalseResetKeepActualRowAndTrainingStamps() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Flag(Flag.HeavenBlessing); s.Player.HeavenBlessing = 10;
        var entry = s.Player.BitVector.ArrayFlags[(int)Flag.HeavenBlessing];
        var first = new DateTime(2001, 2, 3); var second = new DateTime(2004, 5, 6);
        s.Player.ReceivePointsOnlineTraining = first; s.Player.OnlineTrainingTime = second;
        s.Player.CreateHeavenBlessPacket(s.Packet, false);
        Assert.Same(entry, s.Player.BitVector.ArrayFlags[(int)Flag.HeavenBlessing]); Assert.Equal(17, entry.Secounds);
        Assert.Equal(first, s.Player.ReceivePointsOnlineTraining); Assert.Equal(second, s.Player.OnlineTrainingTime);
        Assert.Equal(3, s.Queue.Count);
    });

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(int.MinValue)]
    public Task NonpositiveBlessingDoesNotReadNullStreamOrMutateTimersOrFlags(int blessing) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.HeavenBlessing = blessing;
        s.Player.CreateHeavenBlessPacket(null!, true);
        Assert.Equal(default, s.Player.ReceivePointsOnlineTraining); Assert.Equal(default, s.Player.OnlineTrainingTime);
        Assert.Empty(s.Player.BitVector.ArrayFlags); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task BlessPacketNullStreamRetainsTimerAndFlagPublicationBeforeScalarFault() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.HeavenBlessing = 10; DateTime before = DateTime.Now;
        Assert.Throws<NullReferenceException>(() => s.Player.CreateHeavenBlessPacket(null!, true));
        Assert.True(s.Player.ReceivePointsOnlineTraining >= before.AddMinutes(1));
        Assert.True(s.Player.OnlineTrainingTime >= before.AddMinutes(10));
        Assert.True(s.Player.ContainFlag(Flag.HeavenBlessing)); Assert.Single(s.Queue);
    });
}

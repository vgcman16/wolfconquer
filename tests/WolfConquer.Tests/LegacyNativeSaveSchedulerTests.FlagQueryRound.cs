using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSaveSchedulerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task UnavailableParticipantFlagGraphCannotFinishOrFreezeTheRealFootballRound(bool validCarrier) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var map = s.Map();
        var secondQueue = new Queue<byte[]>(); var second = NativeSpellTestFixture.CreateOwner(secondQueue);
        Scope.Admit(map, second, 18);
        if (validCarrier) Assert.True(s.Client.Player.BitVector.TryAdd((int)Flag.lianhuaran04, 50, true, 0));
        var vector = LegacyNativeRankFlagCorruption.Apply(second, "word-no-registration");
        var football = new LegacyNativeMsgFootball(LegacyNativeTournamentType.FootBall) { Map = map };
        Assert.False(map.View!.Contain(3333333, 85, 85));
        Assert.Throws<InvalidDataException>(() => football.CheckBall());
        Assert.False(football.OnFinishRound); Assert.Equal(default, football.TeleportedTime);
        Assert.False(s.Client.Player.BitVector.ArrayFlags.ContainsKey((int)Flag.Freeze));
        Assert.False(second.Player.BitVector.ArrayFlags.ContainsKey((int)Flag.Freeze));
        Assert.Equal(validCarrier, s.Client.Player.ContainFlag(Flag.lianhuaran04));
        Assert.Empty(s.Queue); Assert.Empty(secondQueue); vector.AssertUnchanged();
    });
}

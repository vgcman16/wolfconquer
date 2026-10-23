using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeReceivePlayerNpcTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PoleSingletonNullFaultKeepsNativeWinnerBeforeRankingPublication(bool createWinner)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var war = Scope.War();
        var oldWinner = war.Winner;
        Assert.True(war.ScoreList.TryAdd(9, null!));
        var recycler = (System.Collections.Concurrent.ConcurrentQueue<WolfConquer.Core.Networking.LegacyNativePacket>)
            typeof(WolfConquer.Core.Networking.LegacyNativePacketRecycle).GetField("bin",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
        var beforePackets = recycler.ToArray();
        DateTime before = DateTime.Now;
        Assert.Throws<NullReferenceException>(() => war.ShuffleGuildScores(createWinner));
        DateTime after = DateTime.Now;
        Assert.Same(oldWinner, war.Winner);
        Assert.Equal(beforePackets, recycler.ToArray());
        Assert.InRange(war.StampShuffleScore, before.AddSeconds(5), after.AddSeconds(5));
        Assert.Null(Assert.Single(war.ScoreList).Value);
        Assert.Equal(default, war.StampRound);
        Assert.Empty(scope.Queue);
    });
}

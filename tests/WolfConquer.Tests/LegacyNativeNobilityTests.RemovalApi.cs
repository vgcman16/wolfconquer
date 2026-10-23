using Ranking = WolfConquer.Core.World.LegacyNativeNobility.NobilityRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNobilityTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task RemovalUsesOneLookupButAlwaysRefreshesEveryRealRemainingRow(bool present)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.PrepareRecycled(2);
        var probes = new RemovalHashProbes();
        var ranking = new Ranking { ClientPoll = new(probes), KingDonation = 11, PrinceDonation = 22, DukeDonation = 33 };
        var first = Row(1, 400_000_000, 1); var second = Row(2, 300_000_000, 2);
        var removed = Row(42, 500_000_000, 0);
        if (present) ranking.ClientPoll.Add(42, removed);
        ranking.ClientPoll.Add(1, first); ranking.ClientPoll.Add(2, second);
        var physical = ranking.ClientPoll;
        uint removalKey = present ? 42u : 99u;
        probes.Keys.Clear();
        ranking.RemoveAndUpdateTheRest(removalKey);
        Assert.Equal(1, probes.Keys.Count(key => key == removalKey));
        Assert.Same(physical, ranking.ClientPoll); Assert.Equal(new uint[] { 1, 2 }, ranking.ClientPoll.Keys);
        Assert.Same(first, ranking.ClientPoll[1]); Assert.Same(second, ranking.ClientPoll[2]);
        Assert.Equal(0, first.Position); Assert.Equal(1, second.Position); Assert.Equal(0, removed.Position);
        Assert.Equal(11UL, ranking.KingDonation); Assert.Equal(22UL, ranking.PrinceDonation);
        Assert.Equal(33UL, ranking.DukeDonation); Assert.Empty(scope.Recycled); Assert.Empty(scope.Native.Queue);
    });
}

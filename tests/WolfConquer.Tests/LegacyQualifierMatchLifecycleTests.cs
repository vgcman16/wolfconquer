using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyQualifierMatchLifecycleTests
{
    private static readonly DateTimeOffset EndedAtUtc = DateTimeOffset.UtcNow.AddHours(1);

    [Fact]
    public void ArenaEndedMatchExpiresStrictlyAfterFourSeconds()
    {
        var store = new ArenaSessionStateStore();
        var first = CreateConnection();
        var second = CreateConnection();
        ActivateArenaMatch(store, first, second);

        Assert.True(store.TryEndActiveMatchAsLoser(second, out _, EndedAtUtc));
        Assert.True(store.IsQualifierFighter(first));
        Assert.Equal(0, store.RemoveExpiredEndedMatches(EndedAtUtc.AddSeconds(4)));
        Assert.True(store.IsQualifierFighter(second));

        Assert.Equal(1, store.RemoveExpiredEndedMatches(EndedAtUtc.AddSeconds(4).AddTicks(1)));
        Assert.False(store.IsQualifierFighter(first));
        Assert.Empty(store.GetActiveMatches());
    }

    [Fact]
    public void TeamArenaEndedMatchExpiresStrictlyAfterFourSeconds()
    {
        var store = new TeamArenaSessionStateStore();
        var first = CreateConnection();
        var second = CreateConnection();
        ActivateTeamArenaMatch(store, first, second);

        Assert.True(store.TryEndActiveMatchAsLoser(second, out _, EndedAtUtc));
        Assert.True(store.IsQualifierTeam(first));
        Assert.Equal(0, store.RemoveExpiredEndedMatches(EndedAtUtc.AddSeconds(4)));
        Assert.True(store.IsQualifierTeam(second));

        Assert.Equal(1, store.RemoveExpiredEndedMatches(EndedAtUtc.AddSeconds(4).AddTicks(1)));
        Assert.False(store.IsQualifierTeam(first));
        Assert.Empty(store.GetActiveMatches());
    }

    [Fact]
    public void ElitePkEndedMatchExpiresStrictlyAfterFourSeconds()
    {
        var store = new LegacyElitePkSessionStateStore();
        var first = CreateConnection();
        var second = CreateConnection();
        store.ActivateMatch(first, second, dynamicId: 4_001);

        Assert.True(store.TryEndActiveMatchAsLoser(second, EndedAtUtc));
        Assert.True(store.IsQualifierFighter(first));
        Assert.Equal(0, store.RemoveExpiredEndedMatches(EndedAtUtc.AddSeconds(4)));
        Assert.True(store.IsQualifierFighter(second));

        Assert.Equal(1, store.RemoveExpiredEndedMatches(EndedAtUtc.AddSeconds(4).AddTicks(1)));
        Assert.False(store.IsQualifierFighter(first));
    }

    [Fact]
    public void TeamPkEndedMatchExpiresStrictlyAfterFourSeconds()
    {
        var store = new LegacyTeamPkSessionStateStore();
        var first = CreateConnection();
        var second = CreateConnection();
        store.ActivateMatch(first, second, dynamicId: 4_002);

        Assert.True(store.TryEndActiveMatchAsLoser(second, EndedAtUtc));
        Assert.True(store.IsQualifierTeam(first));
        Assert.Equal(0, store.RemoveExpiredEndedMatches(EndedAtUtc.AddSeconds(4)));
        Assert.True(store.IsQualifierTeam(second));

        Assert.Equal(1, store.RemoveExpiredEndedMatches(EndedAtUtc.AddSeconds(4).AddTicks(1)));
        Assert.False(store.IsQualifierTeam(first));
        Assert.Empty(store.GetActiveMatches());
    }

    private static void ActivateArenaMatch(
        ArenaSessionStateStore store,
        ConquerTcpConnection first,
        ConquerTcpConnection second)
    {
        Assert.False(store.RegisterWaiting(first, arenaPoints: 4_500, out _));
        Assert.True(store.RegisterWaiting(second, arenaPoints: 4_300, out _));
        Assert.False(store.TryAcceptCountdown(first, out _));
        Assert.True(store.TryAcceptCountdown(second, out _));
    }

    private static void ActivateTeamArenaMatch(
        TeamArenaSessionStateStore store,
        ConquerTcpConnection first,
        ConquerTcpConnection second)
    {
        Assert.False(store.RegisterWaiting(first, arenaPoints: 4_500, out _));
        Assert.True(store.RegisterWaiting(second, arenaPoints: 4_300, out _));
        Assert.False(store.TryAcceptCountdown(first, out _));
        Assert.True(store.TryAcceptCountdown(second, out _));
    }

    private static ConquerTcpConnection CreateConnection()
    {
        return new ConquerTcpConnection(null, null);
    }
}

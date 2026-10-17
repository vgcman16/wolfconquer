using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class TeamArenaSessionStateStoreTests
{
    [Fact]
    public void RegisterWaitingStoresWaitingStatusWithoutPairUntilSecondTeam()
    {
        var store = new TeamArenaSessionStateStore();
        var connection = CreateConnection();

        var matched = store.RegisterWaiting(connection, arenaPoints: 4_500, out var pair);

        Assert.False(matched);
        Assert.Equal(default, pair);
        Assert.Equal(LegacyTeamArenaInfoStatus.WaitingForOpponent, store.GetStatus(connection));
    }

    [Fact]
    public void RegisterWaitingPairsTopTwoWaitingTeamsByArenaPoints()
    {
        var store = new TeamArenaSessionStateStore();
        var low = CreateConnection();
        var high = CreateConnection();
        var middle = CreateConnection();
        Assert.False(store.RegisterWaiting(low, arenaPoints: 3_000, out _));
        Assert.True(store.RegisterWaiting(high, arenaPoints: 5_000, out var firstPair));
        Assert.Same(high, firstPair.First);
        Assert.Same(low, firstPair.Second);
        Assert.Equal(LegacyTeamArenaInfoStatus.WaitingInactive, store.GetStatus(high));
        Assert.Equal(LegacyTeamArenaInfoStatus.WaitingInactive, store.GetStatus(low));

        Assert.False(store.RegisterWaiting(middle, arenaPoints: 4_000, out var secondPair));

        Assert.Equal(default, secondPair);
        Assert.Equal(LegacyTeamArenaInfoStatus.WaitingForOpponent, store.GetStatus(middle));
    }

    [Fact]
    public void SetStatusRemovesConnectionFromWaitingRegistration()
    {
        var store = new TeamArenaSessionStateStore();
        var first = CreateConnection();
        var second = CreateConnection();
        store.RegisterWaiting(first, arenaPoints: 4_500, out _);

        store.SetStatus(first, LegacyTeamArenaInfoStatus.NotSignedUp);
        var matched = store.RegisterWaiting(second, arenaPoints: 4_300, out var pair);

        Assert.False(matched);
        Assert.Equal(default, pair);
        Assert.Equal(LegacyTeamArenaInfoStatus.NotSignedUp, store.GetStatus(first));
        Assert.Equal(LegacyTeamArenaInfoStatus.WaitingForOpponent, store.GetStatus(second));
    }

    [Fact]
    public void TryAcceptCountdownReturnsPairOnlyAfterBothLeadersAccept()
    {
        var store = new TeamArenaSessionStateStore();
        var first = CreateConnection();
        var second = CreateConnection();
        Assert.False(store.RegisterWaiting(first, arenaPoints: 4_500, out _));
        Assert.True(store.RegisterWaiting(second, arenaPoints: 4_300, out var matchedPair));
        Assert.Same(first, matchedPair.First);
        Assert.Same(second, matchedPair.Second);

        Assert.False(store.TryAcceptCountdown(first, out var firstAcceptPair));
        Assert.Equal(default, firstAcceptPair);
        Assert.True(store.TryAcceptCountdown(second, out var secondAcceptPair));

        Assert.Same(first, secondAcceptPair.First);
        Assert.Same(second, secondAcceptPair.Second);
        Assert.Equal(LegacyTeamArenaInfoStatus.WaitingInactive, store.GetStatus(first));
        Assert.Equal(LegacyTeamArenaInfoStatus.WaitingInactive, store.GetStatus(second));
        Assert.False(store.TryAcceptCountdown(first, out var alreadyStartedPair));
        Assert.Equal(default, alreadyStartedPair);
    }

    [Fact]
    public void GetActiveMatchesReturnsAcceptedMatches()
    {
        var store = new TeamArenaSessionStateStore();
        var first = CreateConnection();
        var second = CreateConnection();
        Assert.False(store.RegisterWaiting(first, arenaPoints: 4_500, out _));
        Assert.True(store.RegisterWaiting(second, arenaPoints: 4_300, out _));

        Assert.Empty(store.GetActiveMatches());
        Assert.False(store.TryAcceptCountdown(first, out _));
        Assert.Empty(store.GetActiveMatches());
        Assert.True(store.TryAcceptCountdown(second, out _));

        var match = Assert.Single(store.GetActiveMatches());
        Assert.Same(first, match.First);
        Assert.Same(second, match.Second);
    }

    [Fact]
    public void WatcherStateTracksCheerOnceAndRemove()
    {
        var store = new TeamArenaSessionStateStore();
        var first = CreateConnection();
        var second = CreateConnection();
        var watcher = CreateConnection();
        Assert.False(store.RegisterWaiting(first, arenaPoints: 4_500, out _));
        Assert.True(store.RegisterWaiting(second, arenaPoints: 4_300, out _));
        Assert.False(store.TryAcceptCountdown(first, out _));
        Assert.True(store.TryAcceptCountdown(second, out var pair));

        Assert.True(store.TryAddWatcher(watcher, pair, out var watchUpdate));
        Assert.Same(watcher, Assert.Single(watchUpdate.Watchers));
        Assert.True(store.TryCheer(watcher, firstTeam: true, out var cheerUpdate));
        Assert.Equal(1U, cheerUpdate.FirstCheers);
        Assert.Equal(0U, cheerUpdate.SecondCheers);
        Assert.True(store.TryGetWatcherUpdate(pair, out var scoreUpdate));
        Assert.Same(watcher, Assert.Single(scoreUpdate.Watchers));
        Assert.Equal(1U, scoreUpdate.FirstCheers);
        Assert.True(store.TryAddDamage(pair, firstTeam: true, damage: 1_234, out var firstDamageUpdate));
        Assert.Equal(1_234U, firstDamageUpdate.FirstDamage);
        Assert.Equal(0U, firstDamageUpdate.SecondDamage);
        Assert.True(store.TryAddDamage(pair, firstTeam: false, damage: 345, out var secondDamageUpdate));
        Assert.Equal(1_234U, secondDamageUpdate.FirstDamage);
        Assert.Equal(345U, secondDamageUpdate.SecondDamage);
        Assert.True(store.TryGetScoreUpdate(pair, out var currentScore));
        Assert.Same(watcher, Assert.Single(currentScore.Watchers));
        Assert.Equal(1_234U, currentScore.FirstDamage);
        Assert.Equal(345U, currentScore.SecondDamage);
        store.AddMatchWin(first, points: 40);
        store.AddMatchLoss(second, points: 40);
        var firstRuntime = store.GetRuntimeStatistics(first);
        Assert.Equal(40, firstRuntime.ArenaPointDelta);
        Assert.Equal(1U, firstRuntime.TotalWinBonus);
        Assert.Equal(1U, firstRuntime.TodayWinBonus);
        Assert.Equal(1U, firstRuntime.TodayBattlesBonus);
        var secondRuntime = store.GetRuntimeStatistics(second);
        Assert.Equal(-40, secondRuntime.ArenaPointDelta);
        Assert.Equal(1U, secondRuntime.TotalLoseBonus);
        Assert.Equal(1U, secondRuntime.TodayBattlesBonus);
        Assert.False(store.TryCheer(watcher, firstTeam: true, out var duplicateCheer));
        Assert.Equal(1U, duplicateCheer.FirstCheers);
        Assert.True(store.TryRemoveWatcher(watcher, out var leaveUpdate));
        Assert.Empty(leaveUpdate.Watchers);
    }

    [Fact]
    public void TryAddWatcherRejectsTeamArenaFightersAndExistingWatchers()
    {
        var store = new TeamArenaSessionStateStore();
        var first = CreateConnection();
        var second = CreateConnection();
        var watcher = CreateConnection();
        Assert.False(store.RegisterWaiting(first, arenaPoints: 4_500, out _));
        Assert.True(store.RegisterWaiting(second, arenaPoints: 4_300, out _));
        Assert.False(store.TryAcceptCountdown(first, out _));
        Assert.True(store.TryAcceptCountdown(second, out var pair));

        Assert.False(store.TryAddWatcher(first, pair, out var fighterUpdate));
        Assert.Equal(default, fighterUpdate);

        Assert.True(store.TryAddWatcher(watcher, pair, out var watchUpdate));
        Assert.Same(watcher, Assert.Single(watchUpdate.Watchers));

        Assert.False(store.TryAddWatcher(watcher, pair, out var duplicateUpdate));
        Assert.Equal(default, duplicateUpdate);
        Assert.True(store.TryGetWatcherUpdate(pair, out var currentUpdate));
        Assert.Same(watcher, Assert.Single(currentUpdate.Watchers));
    }

    [Fact]
    public void TryCheerRequiresWatcherToCheerWatchedTeamArenaMatch()
    {
        var store = new TeamArenaSessionStateStore();
        var first = CreateConnection();
        var second = CreateConnection();
        var third = CreateConnection();
        var fourth = CreateConnection();
        var watcher = CreateConnection();
        Assert.False(store.RegisterWaiting(first, arenaPoints: 4_500, out _));
        Assert.True(store.RegisterWaiting(second, arenaPoints: 4_300, out _));
        Assert.False(store.TryAcceptCountdown(first, out _));
        Assert.True(store.TryAcceptCountdown(second, out var watchedPair));
        Assert.False(store.RegisterWaiting(third, arenaPoints: 4_200, out _));
        Assert.True(store.RegisterWaiting(fourth, arenaPoints: 4_100, out _));
        Assert.False(store.TryAcceptCountdown(third, out _));
        Assert.True(store.TryAcceptCountdown(fourth, out var otherPair));
        Assert.True(store.TryAddWatcher(watcher, watchedPair, out _));

        Assert.False(store.TryCheer(watcher, otherPair, firstTeam: true, out var wrongMatchUpdate));
        Assert.Equal(default, wrongMatchUpdate);
        Assert.True(store.TryGetWatcherUpdate(watchedPair, out var watchedUpdate));
        Assert.Equal(0U, watchedUpdate.FirstCheers);
        Assert.Equal(0U, watchedUpdate.SecondCheers);

        Assert.True(store.TryCheer(watcher, watchedPair, firstTeam: true, out var cheerUpdate));
        Assert.Equal(1U, cheerUpdate.FirstCheers);
        Assert.Equal(0U, cheerUpdate.SecondCheers);
    }

    [Fact]
    public void SetStatusNotSignedUpCancelsPendingCountdownMatch()
    {
        var store = new TeamArenaSessionStateStore();
        var first = CreateConnection();
        var second = CreateConnection();
        Assert.False(store.RegisterWaiting(first, arenaPoints: 4_500, out _));
        Assert.True(store.RegisterWaiting(second, arenaPoints: 4_300, out _));

        store.SetStatus(first, LegacyTeamArenaInfoStatus.NotSignedUp);

        Assert.False(store.TryAcceptCountdown(second, out var pair));
        Assert.Equal(default, pair);
        Assert.Equal(LegacyTeamArenaInfoStatus.NotSignedUp, store.GetStatus(first));
        Assert.Equal(LegacyTeamArenaInfoStatus.WaitingInactive, store.GetStatus(second));
    }

    [Fact]
    public void TryCompleteActiveMatchAsLoserResetsBothSides()
    {
        var store = new TeamArenaSessionStateStore();
        var first = CreateConnection();
        var second = CreateConnection();
        Assert.False(store.RegisterWaiting(first, arenaPoints: 4_500, out _));
        Assert.True(store.RegisterWaiting(second, arenaPoints: 4_300, out _));
        Assert.False(store.TryAcceptCountdown(first, out _));
        Assert.True(store.TryAcceptCountdown(second, out _));

        var completed = store.TryCompleteActiveMatchAsLoser(second, out var result);

        Assert.True(completed);
        Assert.Same(first, result.Winner);
        Assert.Same(second, result.Loser);
        Assert.Empty(store.GetActiveMatches());
        Assert.Equal(LegacyTeamArenaInfoStatus.NotSignedUp, store.GetStatus(first));
        Assert.Equal(LegacyTeamArenaInfoStatus.NotSignedUp, store.GetStatus(second));
        Assert.False(store.TryCompleteActiveMatchAsLoser(second, out var completedAgain));
        Assert.Equal(default, completedAgain);
    }

    private static ConquerTcpConnection CreateConnection()
    {
        return new ConquerTcpConnection(null, null);
    }
}

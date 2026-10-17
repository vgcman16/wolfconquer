using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class ArenaSessionStateStoreTests
{
    [Fact]
    public void GetRuntimeStatisticsReturnsDefaultForUnknownConnection()
    {
        var store = new ArenaSessionStateStore();
        var connection = CreateConnection();

        var runtime = store.GetRuntimeStatistics(connection);

        Assert.Equal(default, runtime);
    }

    [Fact]
    public void AddMatchWinTracksRegularArenaWinnerDeltas()
    {
        var store = new ArenaSessionStateStore();
        var winner = CreateConnection();

        store.AddMatchWin(winner, points: 40);

        var runtime = store.GetRuntimeStatistics(winner);
        Assert.Equal(40, runtime.ArenaPointDelta);
        Assert.Equal(1U, runtime.TodayWinBonus);
        Assert.Equal(1U, runtime.TotalWinBonus);
        Assert.Equal(0U, runtime.TodayBattlesBonus);
        Assert.Equal(0U, runtime.TotalLoseBonus);
    }

    [Fact]
    public void AddMatchLossTracksRegularArenaLoserDeltasWithConditionalPointSubtract()
    {
        var store = new ArenaSessionStateStore();
        var loserWithEnoughPoints = CreateConnection();
        var loserWithoutEnoughPoints = CreateConnection();

        store.AddMatchLoss(loserWithEnoughPoints, points: 40, subtractPoints: true);
        store.AddMatchLoss(loserWithoutEnoughPoints, points: 40, subtractPoints: false);

        var subtractedRuntime = store.GetRuntimeStatistics(loserWithEnoughPoints);
        Assert.Equal(-40, subtractedRuntime.ArenaPointDelta);
        Assert.Equal(0U, subtractedRuntime.TodayWinBonus);
        Assert.Equal(0U, subtractedRuntime.TotalWinBonus);
        Assert.Equal(1U, subtractedRuntime.TodayBattlesBonus);
        Assert.Equal(1U, subtractedRuntime.TotalLoseBonus);

        var retainedRuntime = store.GetRuntimeStatistics(loserWithoutEnoughPoints);
        Assert.Equal(0, retainedRuntime.ArenaPointDelta);
        Assert.Equal(0U, retainedRuntime.TodayWinBonus);
        Assert.Equal(0U, retainedRuntime.TotalWinBonus);
        Assert.Equal(1U, retainedRuntime.TodayBattlesBonus);
        Assert.Equal(1U, retainedRuntime.TotalLoseBonus);
    }

    [Fact]
    public void TryAddDamageTracksActiveMatchDamageByAttacker()
    {
        var store = new ArenaSessionStateStore();
        var first = CreateConnection();
        var second = CreateConnection();
        Assert.False(store.RegisterWaiting(first, arenaPoints: 4_500, out _));
        Assert.True(store.RegisterWaiting(second, arenaPoints: 4_300, out _));
        Assert.False(store.TryAcceptCountdown(first, out _));
        Assert.True(store.TryAcceptCountdown(second, out var pair));

        Assert.True(store.TryAddDamage(first, second, damage: 1_234, out var firstUpdate));
        Assert.Same(first, firstUpdate.Match.First);
        Assert.Same(second, firstUpdate.Match.Second);
        Assert.Equal(1_234U, firstUpdate.FirstDamage);
        Assert.Equal(0U, firstUpdate.SecondDamage);

        Assert.True(store.TryAddDamage(second, first, damage: 345, out var secondUpdate));
        Assert.Equal(1_234U, secondUpdate.FirstDamage);
        Assert.Equal(345U, secondUpdate.SecondDamage);
        Assert.True(store.TryGetScoreUpdate(pair, out var currentScore));
        Assert.Equal(1_234U, currentScore.FirstDamage);
        Assert.Equal(345U, currentScore.SecondDamage);
    }

    [Fact]
    public void TryDeclinePendingMatchPreventsImport()
    {
        var store = new ArenaSessionStateStore();
        var first = CreateConnection();
        var second = CreateConnection();
        Assert.False(store.RegisterWaiting(first, arenaPoints: 4_500, out _));
        Assert.True(store.RegisterWaiting(second, arenaPoints: 4_300, out _));

        Assert.True(store.TryDeclinePendingMatch(first));

        Assert.Equal(MsgArenaInfoStatus.WaitingForOpponent, store.GetStatus(first));
        Assert.False(store.TryAcceptCountdown(second, out var pair));
        Assert.Equal(default, pair);
        Assert.True(store.TryCompleteDeclinedPendingMatch(second, out var completed));
        Assert.Same(second, completed.Winner);
        Assert.Same(first, completed.Loser);
        Assert.Equal(MsgArenaInfoStatus.NotSignedUp, store.GetStatus(first));
        Assert.Equal(MsgArenaInfoStatus.NotSignedUp, store.GetStatus(second));
        Assert.Empty(store.GetActiveMatches());
    }

    [Fact]
    public void WatcherStateTracksRegularArenaCheerOnceAndRemove()
    {
        var store = new ArenaSessionStateStore();
        var first = CreateConnection();
        var second = CreateConnection();
        var watcher = CreateConnection();
        Assert.False(store.RegisterWaiting(first, arenaPoints: 4_500, out _));
        Assert.True(store.RegisterWaiting(second, arenaPoints: 4_300, out _));
        Assert.False(store.TryAcceptCountdown(first, out _));
        Assert.True(store.TryAcceptCountdown(second, out var pair));

        Assert.True(store.TryAddWatcher(watcher, pair, out var watchUpdate));
        Assert.Same(watcher, Assert.Single(watchUpdate.Watchers));
        Assert.Equal(0U, watchUpdate.FirstCheers);
        Assert.Equal(0U, watchUpdate.SecondCheers);

        Assert.True(store.TryCheer(watcher, firstFighter: true, out var cheerUpdate));
        Assert.Same(watcher, Assert.Single(cheerUpdate.Watchers));
        Assert.Equal(1U, cheerUpdate.FirstCheers);
        Assert.Equal(0U, cheerUpdate.SecondCheers);
        Assert.True(store.TryGetWatcherUpdate(pair, out var currentUpdate));
        Assert.Equal(1U, currentUpdate.FirstCheers);

        Assert.False(store.TryCheer(watcher, firstFighter: false, out var duplicateCheer));
        Assert.Equal(1U, duplicateCheer.FirstCheers);
        Assert.Equal(0U, duplicateCheer.SecondCheers);
        Assert.True(store.TryRemoveWatcher(watcher, out var leaveUpdate));
        Assert.Empty(leaveUpdate.Watchers);
    }

    [Fact]
    public void TryAddWatcherRejectsRegularArenaFightersAndExistingWatchers()
    {
        var store = new ArenaSessionStateStore();
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
    public void TryCheerRequiresWatcherToCheerWatchedRegularArenaMatch()
    {
        var store = new ArenaSessionStateStore();
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

        Assert.False(store.TryCheer(watcher, otherPair, firstFighter: true, out var wrongMatchUpdate));
        Assert.Equal(default, wrongMatchUpdate);
        Assert.True(store.TryGetWatcherUpdate(watchedPair, out var watchedUpdate));
        Assert.Equal(0U, watchedUpdate.FirstCheers);
        Assert.Equal(0U, watchedUpdate.SecondCheers);

        Assert.True(store.TryCheer(watcher, watchedPair, firstFighter: true, out var cheerUpdate));
        Assert.Equal(1U, cheerUpdate.FirstCheers);
        Assert.Equal(0U, cheerUpdate.SecondCheers);
    }

    [Fact]
    public void TryAddDamageIgnoresConnectionsOutsideActiveMatch()
    {
        var store = new ArenaSessionStateStore();
        var first = CreateConnection();
        var second = CreateConnection();
        var outsider = CreateConnection();
        Assert.False(store.RegisterWaiting(first, arenaPoints: 4_500, out _));
        Assert.True(store.RegisterWaiting(second, arenaPoints: 4_300, out _));
        Assert.False(store.TryAcceptCountdown(first, out _));
        Assert.True(store.TryAcceptCountdown(second, out var pair));

        Assert.False(store.TryAddDamage(first, outsider, damage: 1, out var outsiderUpdate));
        Assert.Equal(default, outsiderUpdate);
        Assert.False(store.TryAddDamage(first, first, damage: 1, out var selfUpdate));
        Assert.Equal(default, selfUpdate);
        Assert.True(store.TryGetScoreUpdate(pair, out var currentScore));
        Assert.Equal(0U, currentScore.FirstDamage);
        Assert.Equal(0U, currentScore.SecondDamage);
    }

    [Fact]
    public void TryGetScoreUpdateReturnsFalseAfterActiveMatchCompletes()
    {
        var store = new ArenaSessionStateStore();
        var first = CreateConnection();
        var second = CreateConnection();
        Assert.False(store.RegisterWaiting(first, arenaPoints: 4_500, out _));
        Assert.True(store.RegisterWaiting(second, arenaPoints: 4_300, out _));
        Assert.False(store.TryAcceptCountdown(first, out _));
        Assert.True(store.TryAcceptCountdown(second, out var pair));
        Assert.True(store.TryAddDamage(first, second, damage: 1_234, out _));

        Assert.True(store.TryCompleteActiveMatchAsLoser(second, out _));

        Assert.False(store.TryGetScoreUpdate(pair, out var update));
        Assert.Equal(default, update);
    }

    private static ConquerTcpConnection CreateConnection()
    {
        return new ConquerTcpConnection(null, null);
    }
}

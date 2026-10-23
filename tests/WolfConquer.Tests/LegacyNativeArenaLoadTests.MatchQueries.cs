using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeArenaLoadTests
{
    [Fact]
    public Task WinnerAndLoserReturnActualPhysicalOwnersAndUseTheSourceFirstPlayerFallback() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var first = scope.Client; var second = scope.Peer(18);
        var match = new LegacyNativeArenaMatchRuntime(first, second, 1);
        Assert.Same(first, match.Winner()); Assert.Same(first, match.Loser());
        second.ArenaStatistic!.QualifierStatus = LegacyNativeArenaUser.MatchStatus.Winner;
        Assert.Same(second, match.Winner()); Assert.Same(first, match.Loser());
        first.ArenaStatistic!.QualifierStatus = LegacyNativeArenaUser.MatchStatus.Loser;
        Assert.Same(second, match.Winner()); Assert.Same(first, match.Loser());
        first.ArenaStatistic.QualifierStatus = LegacyNativeArenaUser.MatchStatus.None;
        second.ArenaStatistic.QualifierStatus = (LegacyNativeArenaUser.MatchStatus)255;
        Assert.Same(second, match.Winner()); // Native predicate accepts any nonzero/nonloser enum.
        Assert.Same(first, match.Loser());
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task MultipleWinnerOrLoserMatchesRetainSingleOrDefaultFailure(bool winner) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var first = scope.Client; var second = scope.Peer(18);
        var match = new LegacyNativeArenaMatchRuntime(first, second, 1);
        var status = winner ? LegacyNativeArenaUser.MatchStatus.Winner : LegacyNativeArenaUser.MatchStatus.Loser;
        first.ArenaStatistic!.QualifierStatus = status; second.ArenaStatistic!.QualifierStatus = status;
        if (winner) Assert.Throws<InvalidOperationException>(() => match.Winner());
        else Assert.Throws<InvalidOperationException>(() => match.Loser());
        Assert.Same(match, first.ArenaMatch); Assert.Same(match, second.ArenaMatch);
    });

    [Fact]
    public Task QueryMethodsReadTheCurrentMutablePlayersAndDoNotRepairNullStatistics() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var second = scope.Peer(18);
        var match = new LegacyNativeArenaMatchRuntime(scope.Client, second, 1);
        match.Players = new[] { second };
        Assert.Same(second, match.Winner()); Assert.Same(second, match.Loser());
        second.ArenaStatistic = null;
        Assert.Throws<NullReferenceException>(() => match.Winner());
        Assert.Throws<NullReferenceException>(() => match.Loser());
        Assert.Null(second.ArenaStatistic);
        match.Players = Array.Empty<LegacyNativeGameClientRuntime>();
        Assert.Throws<IndexOutOfRangeException>(() => match.Winner());
        Assert.Throws<IndexOutOfRangeException>(() => match.Loser());
    });

    [Fact]
    public Task OpponentQueryUsesActualPlayerUidAndAllowsANonparticipantToSelectTheFirstDifferentOwner() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var first = scope.Client; var second = scope.Peer(18);
        var match = new LegacyNativeArenaMatchRuntime(first, second, 1);
        first.ArenaStatistic!.UID = 777; second.ArenaStatistic!.UID = 888;
        Assert.True(match.TryGetOpponent(17, out var fromFirst)); Assert.Same(second, fromFirst);
        Assert.True(match.TryGetOpponent(18, out var fromSecond)); Assert.Same(first, fromSecond);
        Assert.True(match.TryGetOpponent(999, out var intruder)); Assert.Same(first, intruder);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task OpponentQueryDoesNotDeduplicateEqualUidsAndReturnsNullOnNoDifferentPlayer() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var second = scope.Peer(17);
        var match = new LegacyNativeArenaMatchRuntime(scope.Client, second, 1);
        Assert.False(match.TryGetOpponent(17, out var sameUid)); Assert.Null(sameUid);
        match.Players = Array.Empty<LegacyNativeGameClientRuntime>();
        Assert.False(match.TryGetOpponent(17, out var empty)); Assert.Null(empty);
        match.Players = new LegacyNativeGameClientRuntime[] { null! };
        Assert.Throws<NullReferenceException>(() => match.TryGetOpponent(17, out _));
    });
}

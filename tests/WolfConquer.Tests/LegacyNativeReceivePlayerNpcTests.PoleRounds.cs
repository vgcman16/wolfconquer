using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeReceivePlayerNpcTests
{
    [Fact]
    public Task PoleZeroHpScoreClosesEntireRealRoundInNativePacketOrder() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Guild();
        var pole = Scope.Pole(0, 900);
        var war = Scope.War(pole);
        war.ScoreList.TryAdd(8, Row(8, 1, "Other"));
        var before = DateTime.Now;
        war.UpdateScore(scope.Client.Player, 55);
        var after = DateTime.Now;
        Assert.Equal(LegacyNativeProcesType.Alive, war.Proces);
        Assert.Empty(war.ScoreList);
        Assert.Equal("WolfGuild", war.Winner.Name);
        Assert.Equal(55u, war.Winner.Score);
        Assert.Equal("WolfGuild", pole.Name);
        Assert.Equal(900, pole.HitPoints);
        Assert.InRange(war.StampRound, before.AddSeconds(3), after.AddSeconds(3));
        Assert.Equal(new ushort[] { 2179, 2179, 2179, 2176, 2166 }, scope.Queue.Select(Opcode));
        var packets = scope.Queue.ToArray();
        Assert.Equal("No 1. WolfGuild (55)", Message(packets[0]));
        Assert.Equal("No 2. Other (1)", Message(packets[1]));
        Assert.Equal("Congratulations to WolfGuild, they've won the First PoleDomination round with a score of 55", Message(packets[2]));
        var spawn = Proto<LegacyNativeSobNpcRuntime.SobNpcSpawnProto>(packets[4]);
        Assert.Equal(830u, spawn.UID);
        Assert.Equal(900u, spawn.Hitpoints);
        Assert.Equal(900u, spawn.MaxHitpoints);
        Assert.Equal("WolfGuild", spawn.Name);
    });

    [Fact]
    public Task PoleFinishRoundKeepsWinnerRowAliasAfterClearingScoreList() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var pole = Scope.Pole(0);
        var war = Scope.War(pole);
        var first = Row(1, 30, "First");
        var best = Row(2, 90, "Best");
        war.ScoreList.TryAdd(1, first);
        war.ScoreList.TryAdd(2, best);
        war.RewardLeader.Add(77);
        war.FinishRound();
        Assert.Same(best, war.Winner);
        Assert.Empty(war.ScoreList);
        Assert.Equal(new uint[] { 77 }, war.RewardLeader);
        best.Score = 91;
        Assert.Equal(91u, war.Winner.Score);
        Assert.Equal("Best", pole.Name);
    });

    [Fact]
    public Task PoleFinishRoundCanUseStaleWinnerWithNoScoresEvenWhenDead() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var war = Scope.War();
        war.Proces = LegacyNativeProcesType.Dead;
        var winner = Row(1, 88, "Previous");
        war.Winner = winner;
        war.FinishRound();
        Assert.Same(winner, war.Winner);
        Assert.Equal("Previous", war.Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole].Name);
        Assert.Equal(LegacyNativeProcesType.Dead, war.Proces);
        Assert.Equal(new ushort[] { 2179, 2176, 2166 }, scope.Queue.Select(Opcode));
        Assert.Equal(default, war.StampShuffleScore);
    });

    [Fact]
    public Task PoleRoundFaultAfterScoreRankingDoesNotClearOrResetPole() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Guild();
        var pole = Scope.Pole(0);
        var war = Scope.War(pole);
        LegacyNativePoolClients.GamePoll = null;
        Assert.Throws<NullReferenceException>(() => war.UpdateScore(scope.Client.Player, 44));
        Assert.Same(war.ScoreList[7], war.Winner);
        Assert.Equal(44u, war.Winner.Score);
        Assert.Equal("OldPole", pole.Name);
        Assert.Equal(0, pole.HitPoints);
        Assert.Equal(default, war.StampRound);
        Assert.NotEqual(default, war.StampShuffleScore);
    });

    [Fact]
    public Task PoleRoundMissingFurnitureFaultOccursAfterRealRankBroadcastAndWinner() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var war = new LegacyNativePoleDomination { Proces = LegacyNativeProcesType.Alive };
        var row = Row(9, 123);
        war.ScoreList.TryAdd(9, row);
        Assert.Throws<KeyNotFoundException>(() => war.FinishRound());
        Assert.Same(row, war.Winner);
        Assert.Single(war.ScoreList);
        Assert.Equal("No 1. Guild9 (123)", Message(Assert.Single(scope.Queue)));
        Assert.Equal(default, war.StampRound);
    });

    [Fact]
    public Task PoleStartResetsHpBeforeClearAndPublishesRealAnnouncement() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var pole = Scope.Pole(2, 800);
        var war = Scope.War(pole);
        war.Proces = LegacyNativeProcesType.Dead;
        var winner = war.Winner;
        war.ScoreList.TryAdd(7, Row(7, 30));
        war.RewardLeader.Add(5);
        war.SendInvitation = true;
        war.Start();
        Assert.Equal(800, pole.HitPoints);
        Assert.Equal(LegacyNativeProcesType.Alive, war.Proces);
        Assert.Empty(war.ScoreList);
        Assert.Same(winner, war.Winner);
        Assert.True(war.SendInvitation);
        Assert.Equal(new uint[] { 5 }, war.RewardLeader);
        Assert.Equal(new ushort[] { 2176, 2166, 2179 }, scope.Queue.Select(Opcode));
        Assert.Equal("First PoleDomination war in Skay City has started!.", Message(scope.Queue.Last()));
    });

    [Fact]
    public Task PoleStartFaultKeepsAlreadyAliveStateAndUnclearedScores() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var war = new LegacyNativePoleDomination();
        war.Furnitures.Add(LegacyNativeSobNpcRuntime.StaticMesh.Pole, null!);
        war.ScoreList.TryAdd(7, Row(7, 3));
        Assert.Throws<NullReferenceException>(() => war.Start());
        Assert.Equal(LegacyNativeProcesType.Alive, war.Proces);
        Assert.Single(war.ScoreList);
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData("None", 77u, false)]
    [InlineData("Named", 100u, false)]
    [InlineData("Named", 77u, true)]
    [InlineData(null, 77u, true)]
    public Task PoleCompletionUsesNativeNameAndScoreConjunction(string? name, uint score, bool won) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var war = Scope.War();
        var winner = Row(7, score);
        winner.Name = name;
        winner.LeaderReward = 9;
        war.Winner = winner;
        war.RewardLeader.Add(77);
        war.SendInvitation = true;
        // Shuffle(false) must not change the previous winner.
        war.ScoreList.TryAdd(8, Row(8, 1000, "NewHighest"));
        war.CompletePoleDomination();
        Assert.Same(winner, war.Winner);
        Assert.False(war.SendInvitation);
        Assert.Equal(LegacyNativeProcesType.Dead, war.Proces);
        Assert.Empty(war.ScoreList);
        Assert.Empty(war.RewardLeader);
        Assert.Equal(1, winner.LeaderReward);
        Assert.Equal(2, scope.Queue.Count);
        Assert.Equal("No 1. NewHighest (1000)", Message(scope.Queue.First()));
        Assert.Equal(won
            ? "Congratulations to " + name + ", they've won the  First PoleDomination  with a score of " + score
            : "First PoleDomination Has Endd Now  with no winner.", Message(scope.Queue.Last()));
    });

    [Fact]
    public Task PoleCompletionFaultCommitsDeadAndClearBeforeGlobalSendButRetainsRewards() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var war = Scope.War();
        war.Winner = Row(7, 88, "Winner");
        war.Winner.LeaderReward = 8;
        war.RewardLeader.Add(77);
        war.SendInvitation = true;
        LegacyNativePoolClients.GamePoll = null;
        Assert.Throws<NullReferenceException>(() => war.CompletePoleDomination());
        Assert.False(war.SendInvitation);
        Assert.Equal(LegacyNativeProcesType.Dead, war.Proces);
        Assert.Empty(war.ScoreList);
        Assert.Equal(new uint[] { 77 }, war.RewardLeader);
        Assert.Equal(8, war.Winner.LeaderReward);
    });
}

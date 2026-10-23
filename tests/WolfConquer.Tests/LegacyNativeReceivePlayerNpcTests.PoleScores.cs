using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeReceivePlayerNpcTests
{
    [Fact]
    public void PoleOwnerWholeConstructorRetainsSourceDefaults()
    {
        var war = new LegacyNativePoleDomination();
        Assert.Equal(LegacyNativeProcesType.Dead, war.Proces);
        Assert.False(war.SendInvitation);
        Assert.Empty(war.ScoreList);
        Assert.Empty(war.Furnitures);
        Assert.Empty(war.RewardLeader);
        Assert.Equal(default, war.StampRound);
        Assert.Equal(default, war.StampShuffleScore);
        Assert.Equal("None", war.Winner.Name);
        Assert.Equal(100u, war.Winner.Score);
        Assert.Equal(0u, war.Winner.GuildID);
        Assert.Equal(1, war.Winner.LeaderReward);
        Assert.Null(new LegacyNativePoleDomination.PoleDomination().Name);
    }

    [Theory]
    [InlineData((int)LegacyNativeProcesType.Dead)]
    [InlineData((int)LegacyNativeProcesType.Idle)]
    public Task PoleScoreNonAliveReturnsBeforeFurnitureLookup(int state) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Guild();
        var war = new LegacyNativePoleDomination { Proces = (LegacyNativeProcesType)state };
        war.UpdateScore(scope.Client.Player, 250);
        Assert.Empty(war.ScoreList);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task PoleScoreNoGuildReturnsEvenWithAliveMissingFurniture() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var war = new LegacyNativePoleDomination { Proces = LegacyNativeProcesType.Alive };
        war.UpdateScore(scope.Client.Player, 250);
        Assert.Empty(war.ScoreList);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task PoleScoreCreatesActualGuildIdentityThenAccumulatesSameRow() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Guild();
        var war = Scope.War();
        war.UpdateScore(scope.Client.Player, 250);
        var row = Assert.Single(war.ScoreList).Value;
        Assert.Equal(7u, row.GuildID);
        Assert.Equal("WolfGuild", row.Name);
        Assert.Equal(250u, row.Score);
        scope.Client.Player.MyGuild!.GuildName = "Renamed";
        war.UpdateScore(scope.Client.Player, 13);
        Assert.Same(row, war.ScoreList[7]);
        Assert.Equal(263u, row.Score);
        Assert.Equal("WolfGuild", row.Name);
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(0u, 0u, 0u)]
    [InlineData(uint.MaxValue, 2u, 1u)]
    [InlineData(500u, 1000u, 1500u)]
    public Task PoleScoreRetainsUnsignedWrapAndNoHpClamp(uint initial, uint damage, uint expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Guild();
        var war = Scope.War();
        var row = Row(7, initial);
        war.ScoreList.TryAdd(7, row);
        war.UpdateScore(scope.Client.Player, damage);
        Assert.Equal(expected, row.Score);
        Assert.Equal(100, war.Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole].HitPoints);
    });

    [Fact]
    public Task PoleScoreInsertionUsesPlayerKeyButNativeRowUsesGuildInfoKey() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Guild(7, 17);
        var war = Scope.War();
        war.UpdateScore(scope.Client.Player, 25);
        Assert.Equal(17u, war.ScoreList[7].GuildID);
        Assert.Throws<KeyNotFoundException>(() => war.UpdateScore(scope.Client.Player, 10));
        Assert.Equal(25u, war.ScoreList[7].Score);
        Assert.False(war.ScoreList.ContainsKey(17));
    });

    [Fact]
    public Task PoleScoreExistingMismatchedKeysAccumulatesGuildInfoRowNotPlayerRow() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Guild(7, 17);
        var war = Scope.War();
        war.ScoreList.TryAdd(7, Row(7, 10));
        war.ScoreList.TryAdd(17, Row(17, 20));
        war.UpdateScore(scope.Client.Player, 5);
        Assert.Equal(10u, war.ScoreList[7].Score);
        Assert.Equal(25u, war.ScoreList[17].Score);
    });

    [Fact]
    public Task PoleScoreCommitsNewRowBeforeMissingFurnitureFault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Guild();
        var war = new LegacyNativePoleDomination { Proces = LegacyNativeProcesType.Alive };
        Assert.Throws<KeyNotFoundException>(() => war.UpdateScore(scope.Client.Player, 31));
        Assert.Equal(31u, war.ScoreList[7].Score);
        Assert.Equal(default, war.StampRound);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task PoleScoreNegativePoleHpIsNotAZeroRoundTrigger() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Guild();
        var pole = Scope.Pole(-1);
        var war = Scope.War(pole);
        war.UpdateScore(scope.Client.Player, 55);
        Assert.Equal(55u, war.ScoreList[7].Score);
        Assert.Equal(-1, pole.HitPoints);
        Assert.Equal(default, war.StampRound);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task PoleShufflePublishesOnlyTopFiveRealRowsWithoutReplacingWinnerByDefault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var war = Scope.War();
        var oldWinner = war.Winner;
        for (uint i = 1; i <= 7; i++) war.ScoreList.TryAdd(i, Row(i, i * 10));
        var before = DateTime.Now;
        war.ShuffleGuildScores();
        var after = DateTime.Now;
        Assert.Same(oldWinner, war.Winner);
        Assert.InRange(war.StampShuffleScore, before.AddSeconds(5), after.AddSeconds(5));
        Assert.Equal(5, scope.Queue.Count);
        var packets = scope.Queue.ToArray();
        Assert.Equal(new[] { "No 1. Guild7 (70)", "No 2. Guild6 (60)", "No 3. Guild5 (50)", "No 4. Guild4 (40)", "No 5. Guild3 (30)" },
            packets.Select(Message));
        Assert.Equal((uint)LegacyNativeMsgMessage.ChatMode.FirstRightCorner, Talk(packets[0]).ChatType);
        Assert.All(packets.Skip(1), p => Assert.Equal((uint)LegacyNativeMsgMessage.ChatMode.ContinueRightCorner, Talk(p).ChatType));
        Assert.All(packets, p => Assert.Equal(16776960u, Talk(p).Color));
    });

    [Fact]
    public Task PoleShuffleTiesPreserveActualDictionarySnapshotOrderAndWinnerAlias() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var war = Scope.War();
        war.ScoreList.TryAdd(11, Row(11, 99));
        war.ScoreList.TryAdd(29, Row(29, 99));
        var snapshot = war.ScoreList.Values.ToArray();
        war.ShuffleGuildScores(true);
        Assert.Same(snapshot[0], war.Winner);
        Assert.Equal(snapshot.Select((r, i) => "No " + (i + 1) + ". " + r.Name + " (99)"), scope.Queue.Select(Message));
        snapshot[0].Score = 777;
        Assert.Equal(777u, war.Winner.Score);
    });

    [Fact]
    public void PoleShuffleDeadIsAnExactNoOpEvenWithNullScoreList()
    {
        var war = new LegacyNativePoleDomination { ScoreList = null! };
        war.ShuffleGuildScores(true);
        Assert.Equal(default, war.StampShuffleScore);
        Assert.Equal(100u, war.Winner.Score);
    }

    [Fact]
    public Task PoleShuffleWinnerAndStampCommitBeforeRealPoolFault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var war = Scope.War();
        var row = Row(9, 99);
        war.ScoreList.TryAdd(9, row);
        LegacyNativePoolClients.GamePoll = null;
        Assert.Throws<NullReferenceException>(() => war.ShuffleGuildScores(true));
        Assert.Same(row, war.Winner);
        Assert.NotEqual(default, war.StampShuffleScore);
        Assert.Single(war.ScoreList);
    });
}

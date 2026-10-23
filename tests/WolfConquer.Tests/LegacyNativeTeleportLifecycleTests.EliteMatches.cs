using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeleportLifecycleTests
{
    [Fact]
    public Task EliteAddPublishesRealPlayerAndStatsAndReplacementRetainsNativeCacheShape()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); scope.Map(700);
            var group = new LegacyNativeEliteGroupRuntime(LegacyNativeEliteTournamentGroupType.EPK_Lvl130Plus);
            var match = new LegacyNativeEliteGroupRuntime.Match(group);
            Assert.Same(match, match.AddPlayer(scope.Client, LegacyNativeEliteGroupRuntime.FighterStats.StatusFlag.Qualified));
            Assert.Same(scope.Client, Assert.Single(match.PlayersArray));
            Assert.Same(match, scope.Client.ElitePkMatch);
            Assert.Same(scope.Client.ElitePKStats, Assert.Single(match.GetMatchStats));
            Assert.True(scope.Client.ElitePKStats!.Advanced);
            var old = scope.Client.ElitePKStats;
            match.AddPlayer(scope.Client);
            Assert.Equal(1, match.Count); Assert.NotSame(old, scope.Client.ElitePKStats);
            Assert.Same(scope.Client.ElitePKStats, Assert.Single(match.GetMatchStats));
            group.State = LegacyNativeMsgElitePKBrackets.GuiTyp.GUI_Top8Qualifier;
            var replacement = new LegacyNativeEliteGroupRuntime.FighterStats(17, "Replacement", 0, 0, 0);
            scope.Client.ElitePKStats = replacement;
            Assert.Same(replacement, Assert.Single(match.GetMatchStats));
        });
    }

    [Fact]
    public Task EliteAddRetainsSourcePmNameAndPublishesStatsNormally()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); scope.Map(700);
            var match = new LegacyNativeEliteGroupRuntime.Match(null);
            scope.Client.Player.Name = "MrRayzo[PM]";
            Assert.Same(match, match.AddPlayer(scope.Client));
            Assert.Same(match, scope.Client.ElitePkMatch);
            Assert.Equal("MrRayzo[PM]", scope.Client.ElitePKStats!.Name);
            Assert.Same(scope.Client.ElitePKStats, match.MatchStats[scope.Client.Player.UID]);
        });
    }

    [Fact]
    public Task EliteSingleMatchQualifiesAndLatchesWholeNativeFinishState()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); scope.Map(700);
            var match = new LegacyNativeEliteGroupRuntime.Match(null).AddPlayer(scope.Client);
            match.CheckFinish();
            Assert.True(match.IsFinishd()); Assert.Equal(LegacyNativeEliteGroupRuntime.Match.StatusFlag.OK, match.Flag);
            Assert.Equal(LegacyNativeEliteGroupRuntime.FighterStats.StatusFlag.Qualified, scope.Client.ElitePKStats!.Flag);
            Assert.True(scope.Client.ElitePKStats.Advanced);
            Assert.Null(match.GetOpponent(17));
            Assert.True((bool)typeof(LegacyNativeEliteGroupRuntime.Match).GetField("Done", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(match)!);
        });
    }

    [Fact]
    public Task EliteThreePlayerWaitingOpponentAndSwitchFollowRealFlagsAndStatsAliasing()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); scope.Map(700);
            var two = scope.Peer(18, new()); var three = scope.Peer(19, new());
            var match = new LegacyNativeEliteGroupRuntime.Match(null);
            match.AddPlayer(scope.Client).AddPlayer(two).AddPlayer(three);
            Assert.Throws<NullReferenceException>(() => match.GetOpponent(18));
            match.AddWaiting();
            Assert.Same(scope.Client, match.PlayerWaiting);
            Assert.Equal(LegacyNativeEliteGroupRuntime.FighterStats.StatusFlag.Waiting, scope.Client.ElitePKStats!.Flag);
            Assert.Same(three, match.GetOpponent(18));
            var waitingStats = scope.Client.ElitePKStats;
            two.ElitePKStats!.Flag = LegacyNativeEliteGroupRuntime.FighterStats.StatusFlag.Lost;
            match.Switch();
            Assert.Equal(LegacyNativeEliteGroupRuntime.Match.StatusFlag.SwitchOut, match.Flag);
            Assert.NotSame(waitingStats, scope.Client.ElitePKStats); Assert.Same(waitingStats, match.MatchStats[17]);
            Assert.Same(three, match.GetOpponent(17));
            Assert.False(match.IsFinishd()); Assert.Same(match, scope.Client.ElitePkMatch);
            scope.Client.ElitePKStats!.Flag = LegacyNativeEliteGroupRuntime.FighterStats.StatusFlag.Lost;
            match.Switch(); Assert.Equal(LegacyNativeEliteGroupRuntime.Match.StatusFlag.OK, match.Flag);
        });
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public Task EliteNonTripleSwitchDoesNotSynthesizeWaitingPlayer(int count)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); scope.Map(700); var match = new LegacyNativeEliteGroupRuntime.Match(null);
            for (uint i = 1; i <= count; i++) match.AddPlayer(scope.Peer(i, new()));
            match.Switch(); match.AddWaiting();
            Assert.Null(match.PlayerWaiting); Assert.Equal(LegacyNativeEliteGroupRuntime.Match.StatusFlag.AcceptingWagers, match.Flag);
        });
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(5, 3)]
    [InlineData(8, 4)]
    public Task EliteDoublePairingUsesRealMatchConstructorsAndNativePartialLastPair(int players, int matches)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); scope.Map(700);
            var group = new LegacyNativeEliteGroupRuntime(LegacyNativeEliteTournamentGroupType.EPK_Lvl130Plus);
            for (uint uid = 1; uid <= players; uid++) group.Players.Add(uid, scope.Peer(uid, new()));
            var rows = new LegacyNativeDbSafeDictionary<uint, LegacyNativeEliteGroupRuntime.Match>();
            group.CreateDoubleMatchs(rows);
            Assert.Equal(matches, rows.Count); Assert.Equal((uint)matches, group.MatchIndex);
            Assert.Equal(players, rows.Values.Sum(row => row.Count));
            Assert.All(rows.Values, row => Assert.InRange(row.Count, 1, 2));
            Assert.Equal(400001u, rows.GetValues()[0].ID);
            Assert.Equal(10000001u + (uint)matches, LegacyNativeGameMap.DinamicIDS.Count);
            Assert.All(group.Players.Values, player => Assert.NotNull(player.ElitePkMatch));
        });
    }

    [Theory]
    [InlineData(8, 8, 0)]
    [InlineData(16, 8, 0)]
    [InlineData(17, 8, 1)]
    [InlineData(24, 8, 8)]
    public Task EliteTriplePairingRetainsAllPlayersAndRealWaitingAdmission(int players, int matches, int triples)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); scope.Map(700);
            var group = new LegacyNativeEliteGroupRuntime(LegacyNativeEliteTournamentGroupType.EPK_Lvl130Plus);
            for (uint uid = 1; uid <= players; uid++) group.Players.Add(uid, scope.Peer(uid, new()));
            var rows = new LegacyNativeDbSafeDictionary<uint, LegacyNativeEliteGroupRuntime.Match>();
            group.CreateTripleMatchs(rows);
            Assert.Equal(matches, rows.Count); Assert.Equal(players, rows.Values.Sum(row => row.Count));
            Assert.Equal(triples, rows.Values.Count(row => row.Count == 3));
            Assert.Equal(triples, rows.Values.Count(row => row.PlayerWaiting != null));
        });
    }
}

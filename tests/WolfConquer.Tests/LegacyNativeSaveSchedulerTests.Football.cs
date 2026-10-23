using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSaveSchedulerTests
{
    [Fact]
    public Task GenuineFootballBirthRetainsNpcInitializerAndSuppliedRawTournamentType() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var f = new LegacyNativeMsgFootball((LegacyNativeTournamentType)255);
        Assert.Equal((LegacyNativeTournamentType)255, f.Type); Assert.Equal(LegacyNativeProcesType.Dead, f.Process);
        Assert.Null(f.Team1); Assert.Null(f.Team2); Assert.Null(f.Map); Assert.False(f.OnFinishRound);
        Assert.Equal(default, f.TeleportedTime); Assert.Equal(0, f.Secounds);
        Assert.NotNull(Field<LegacyNativeSobNpcRuntime>(f, "npc"));
        Assert.NotSame(Field<LegacyNativeSobNpcRuntime>(f, "npc"),
            Field<LegacyNativeSobNpcRuntime>(new LegacyNativeMsgFootball(LegacyNativeTournamentType.FootBall), "npc"));
    });

    [Fact]
    public Task WholeRoundResetBroadcastsRealChatThenAddsTenSecondDeathRemovableFreeze() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var map = s.Map();
        var secondQueue = new Queue<byte[]>(); var second = NativeSpellTestFixture.CreateOwner(secondQueue);
        Scope.Admit(map, second, 18);
        var f = new LegacyNativeMsgFootball(LegacyNativeTournamentType.FootBall) { Map = map };
        DateTime before = DateTime.Now; f.FinishRound(); DateTime after = DateTime.Now;
        Assert.True(f.OnFinishRound); Assert.InRange(f.TeleportedTime, before.AddSeconds(2), after.AddSeconds(2));
        Assert.Equal("The next round will start in 8 seconds.", Talk(s.Queue.Peek()).Content[3]);
        Assert.Equal("The next round will start in 8 seconds.", Talk(secondQueue.Peek()).Content[3]);
        foreach (var client in new[] { s.Client, second })
        {
            var row = client.Player.BitVector.ArrayFlags[(int)LegacyNativeMsgUpdate.Flags.Freeze];
            Assert.Equal(10, row.Secounds); Assert.True(row.RemoveOnDead);
            Assert.Equal(0, row.InvokerSecouds);
        }
    });

    [Fact]
    public Task RoundResetKeepsAnAlreadyPresentFreezeWithoutRefreshingIt() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var map = s.Map();
        s.Client.Player.BitVector.TryAdd((int)LegacyNativeMsgUpdate.Flags.Freeze, 77, false, 3);
        var row = s.Client.Player.BitVector.ArrayFlags[(int)LegacyNativeMsgUpdate.Flags.Freeze];
        var f = new LegacyNativeMsgFootball(LegacyNativeTournamentType.FootBall) { Map = map };
        f.FinishRound();
        Assert.Same(row, s.Client.Player.BitVector.ArrayFlags[(int)LegacyNativeMsgUpdate.Flags.Freeze]);
        Assert.Equal(77, row.Secounds); Assert.False(row.RemoveOnDead); Assert.Equal(3, row.InvokerSecouds);
    });

    [Fact]
    public Task RoundFaultAfterStateCommitIsNotConvertedIntoAbsentMapSuccess() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var f = new LegacyNativeMsgFootball(LegacyNativeTournamentType.FootBall);
        DateTime before = DateTime.Now;
        Assert.Throws<NullReferenceException>(() => f.FinishRound());
        Assert.True(f.OnFinishRound); Assert.InRange(f.TeleportedTime, before.AddSeconds(2), DateTime.Now.AddSeconds(2));
        Assert.Empty(s.Queue);
    });

    [Fact]
    public Task NpcLifecycleUsesRealViewMembershipCollisionAndNearbySpawnEffectRemovalPackets() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var map = s.Map();
        var f = new LegacyNativeMsgFootball(LegacyNativeTournamentType.FootBall) { Map = map };
        f.AddNpc();
        Assert.True(map.View!.Contain(3333333, 85, 85));
        var npc = Field<LegacyNativeSobNpcRuntime>(f, "npc");
        Assert.Equal(3333333u, npc.UID); Assert.Equal((ushort)85, npc.X); Assert.Equal((ushort)85, npc.Y);
        Assert.Equal(LegacyNativeNpcType.Talker, npc.Type); Assert.Equal((ushort)480, (ushort)npc.Mesh);
        Assert.Equal(LegacyNativeMapFlagType.Npc, map.cells[85, 85]);
        Assert.NotEmpty(s.Queue);
        int count = s.Queue.Count; f.AddNpc(); Assert.Equal(count, s.Queue.Count);
        s.Queue.Clear(); f.RemoveNpc();
        Assert.False(map.View.Contain(3333333, 85, 85));
        Assert.Equal(LegacyNativeMapFlagType.Valid, map.cells[85, 85]);
        Assert.Equal(new ushort[] { 2036, 2046 }, s.Queue.Select(Opcode));
    });

    [Fact]
    public Task BallCheckDistinguishesNpcPresenceCarrierAndActualRoundRestart() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var map = s.Map();
        var f = new LegacyNativeMsgFootball(LegacyNativeTournamentType.FootBall) { Map = map };
        f.AddNpc(); s.Queue.Clear(); f.CheckBall(); Assert.False(f.OnFinishRound); Assert.Empty(s.Queue);
        f.RemoveNpc(); s.Queue.Clear();
        s.Client.Player.BitVector.TryAdd((int)LegacyNativeMsgUpdate.Flags.lianhuaran04, 50, true, 0);
        f.CheckBall(); Assert.False(f.OnFinishRound); Assert.Empty(s.Queue);
        s.Client.Player.BitVector.TryRemove((int)LegacyNativeMsgUpdate.Flags.lianhuaran04);
        f.CheckBall(); Assert.True(f.OnFinishRound); Assert.NotEmpty(s.Queue);
    });

    [Fact]
    public Task TeamCountUsesAllRealMapMembersAndNativeTeamNumberEquality() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var map = s.Map();
        var second = NativeSpellTestFixture.CreateOwner(new()); Scope.Admit(map, second, 18);
        var f = new LegacyNativeMsgFootball(LegacyNativeTournamentType.FootBall) { Map = map };
        s.Client.Player.FootballTeamID = 1; second.Player.FootballTeamID = 2;
        var team = new LegacyNativeMsgFootball.Team(f, LegacyNativeMsgFootball.Team.TeamTypes.Argentina, 1);
        Assert.Equal(1, team.Count); Assert.Same(f, team.Football); Assert.Equal(0, team.Score);
        second.Player.FootballTeamID = 1; Assert.Equal(2, team.Count);
        Assert.Same(map.Values, f.Players());
        Assert.True(f.InTournament(s.Client)); s.Client.Player.Map = 1002; Assert.False(f.InTournament(s.Client));
    });
}

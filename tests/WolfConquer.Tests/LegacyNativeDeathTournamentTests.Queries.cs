using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDeathTournamentTests
{
    [Fact]
    public Task DynamicQueriesRetainNullMapGuardButMapPlayersFaults() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var killer = new LegacyNativeMsgTheKillerOfElite((LegacyNativeTournamentType)0);
        var extreme = new LegacyNativeMsgExtremePk((LegacyNativeTournamentType)0);
        var last = new LegacyNativeMsgLastManStand((LegacyNativeTournamentType)0);
        Assert.False(killer.InTournament(null!)); Assert.False(extreme.InTournament(null!)); Assert.False(last.InTournament(null!));
        Assert.Throws<NullReferenceException>(() => killer.MapPlayers());
        Assert.Throws<NullReferenceException>(() => extreme.MapPlayers());
        Assert.Throws<NullReferenceException>(() => last.MapPlayers());
        killer.Map = extreme.Map = last.Map = scope.Map();
        Assert.Throws<NullReferenceException>(() => killer.InTournament(null!));
        Assert.Throws<NullReferenceException>(() => extreme.InTournament(null!));
        Assert.Throws<NullReferenceException>(() => last.InTournament(null!));
    });
    [Fact]
    public Task DynamicQueriesUseActualMapMembershipAndBothIdentities() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map();
        var killer = new LegacyNativeMsgTheKillerOfElite((LegacyNativeTournamentType)0) { Map = map, DinamicID = 9 };
        var extreme = new LegacyNativeMsgExtremePk((LegacyNativeTournamentType)0) { Map = map, DinamicID = 9 };
        var last = new LegacyNativeMsgLastManStand((LegacyNativeTournamentType)0) { Map = map, DinamicMap = 9 };
        scope.Other.Player.DynamicID = 10;
        Assert.Same(scope.Client, Assert.Single(killer.MapPlayers())); Assert.Same(scope.Client, Assert.Single(extreme.MapPlayers()));
        Assert.Same(scope.Client, Assert.Single(last.MapPlayers()));
        scope.Other.Player.DynamicID = 9; scope.Other.Player.Map = 1002;
        Assert.False(extreme.InTournament(scope.Other)); Assert.Single(extreme.MapPlayers());
        scope.Client.Player.Map = 1002; Assert.Empty(killer.MapPlayers()); Assert.Empty(last.MapPlayers());
    });
    [Fact]
    public Task TeamIdentityIgnoresDynamicProcessAndTeamMembership() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var owner = new LegacyNativeMsgTeamDeathMatch((LegacyNativeTournamentType)0);
        scope.Client.Player.Map = 1082; scope.Client.Player.DynamicID = uint.MaxValue;
        Assert.True(owner.InTournament(scope.Client)); Assert.Null(owner.Teams);
        scope.Client.Player.Map = 1505; Assert.False(owner.InTournament(scope.Client));
        Assert.Throws<NullReferenceException>(() => owner.InTournament(null!));
    });
    [Fact]
    public Task TeamScoreIsUncheckedAndReturnsAccumulatedFaultPrefix() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var owner = new LegacyNativeMsgTeamDeathMatch((LegacyNativeTournamentType)0);
        scope.Client.Player.TeamDeathMacthKills = uint.MaxValue; scope.Other.Player.TeamDeathMacthKills = 3;
        Assert.Equal(0u, owner.GetTeamScore(null)); Assert.Equal(0u, owner.GetTeamScore(new()));
        var rows = new Dictionary<uint, LegacyNativeGameClientRuntime> { [17] = scope.Client, [31] = scope.Other };
        Assert.Equal(2u, owner.GetTeamScore(rows));
        rows = new() { [17] = scope.Client, [99] = null!, [31] = scope.Other };
        Assert.Equal(uint.MaxValue, owner.GetTeamScore(rows));
        rows = new() { [99] = null!, [17] = scope.Client }; Assert.Equal(0u, owner.GetTeamScore(rows));
    });
    [Fact]
    public Task DynamicMapSendAndTeamMapSendRetainDifferentFilters() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(); scope.Publish();
        var extreme = new LegacyNativeMsgExtremePk((LegacyNativeTournamentType)0) { Map = map, DinamicID = 9 };
        scope.Other.Player.DynamicID = 8;
        extreme.SendMapPacket(new LegacyNativeMsgMessage("dynamic", LegacyNativeMsgMessage.MsgColor.red, LegacyNativeMsgMessage.ChatMode.Talk).GetArray(scope.Packet));
        Assert.Equal("dynamic", Text(Assert.Single(scope.Queue))); Assert.Empty(scope.OtherQueue);
        scope.Queue.Clear(); scope.Client.Player.Map = scope.Other.Player.Map = 1082;
        new LegacyNativeMsgTeamDeathMatch((LegacyNativeTournamentType)0)
            .SendMapPacket(new LegacyNativeMsgMessage("map-only", LegacyNativeMsgMessage.MsgColor.red, LegacyNativeMsgMessage.ChatMode.Talk).GetArray(scope.Packet));
        Assert.Equal("map-only", Text(Assert.Single(scope.Queue))); Assert.Equal("map-only", Text(Assert.Single(scope.OtherQueue)));
    });
}

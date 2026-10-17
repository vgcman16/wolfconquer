using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeamSupportTargetResolverTests
{
    [Fact]
    public void ResolveActiveTeammatesWithoutTeamReturnsEmpty()
    {
        var connection = CreateConnection();
        var session = CreateSession(1_293_600);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = CreateContext(connection, session, sessions, new TeamSessionStateStore());

        var teammates = LegacyTeamSupportTargetResolver.ResolveActiveTeammates(context, session);

        Assert.Empty(teammates);
    }

    [Fact]
    public void ResolveActiveTeammatesReturnsActiveNearbyTeammate()
    {
        var leaderConnection = CreateConnection();
        var teammateConnection = CreateConnection();
        var leader = CreateSession(1_293_600, "Leader", x: 830, y: 584);
        var teammate = CreateSession(1_293_601, "Teammate", x: 831, y: 584);
        var teamStates = new TeamSessionStateStore();
        teamStates.CreateTeam(leaderConnection, leader.Character.Uid);
        Assert.True(teamStates.TryAddMember(leaderConnection, teammateConnection, teammate.Character.Uid));
        var sessions = new WorldSessionStore();
        sessions.Set(leaderConnection, leader);
        sessions.Set(teammateConnection, teammate);
        var context = CreateContext(leaderConnection, leader, sessions, teamStates);

        var resolved = Assert.Single(LegacyTeamSupportTargetResolver.ResolveActiveTeammates(context, leader));

        Assert.Same(teammateConnection, resolved.Connection);
        Assert.Same(teammate, resolved.Session);
    }

    [Fact]
    public void ResolveActiveTeammatesSkipsMissingOfflineTeammate()
    {
        var leaderConnection = CreateConnection();
        var offlineConnection = CreateConnection();
        var leader = CreateSession(1_293_600, "Leader");
        var teamStates = new TeamSessionStateStore();
        teamStates.CreateTeam(leaderConnection, leader.Character.Uid);
        Assert.True(teamStates.TryAddMember(leaderConnection, offlineConnection, 1_293_601));
        var sessions = new WorldSessionStore();
        sessions.Set(leaderConnection, leader);
        var context = CreateContext(leaderConnection, leader, sessions, teamStates);

        var teammates = LegacyTeamSupportTargetResolver.ResolveActiveTeammates(context, leader);

        Assert.Empty(teammates);
    }

    [Fact]
    public void ResolveActiveTeammatesSkipsDifferentMapTeammate()
    {
        var leaderConnection = CreateConnection();
        var teammateConnection = CreateConnection();
        var leader = CreateSession(1_293_600, "Leader", mapId: 1015);
        var teammate = CreateSession(1_293_601, "Teammate", mapId: 1002);
        var teamStates = new TeamSessionStateStore();
        teamStates.CreateTeam(leaderConnection, leader.Character.Uid);
        Assert.True(teamStates.TryAddMember(leaderConnection, teammateConnection, teammate.Character.Uid));
        var sessions = new WorldSessionStore();
        sessions.Set(leaderConnection, leader);
        sessions.Set(teammateConnection, teammate);
        var context = CreateContext(leaderConnection, leader, sessions, teamStates);

        var teammates = LegacyTeamSupportTargetResolver.ResolveActiveTeammates(context, leader);

        Assert.Empty(teammates);
    }

    private static WorldPacketHandlerContext CreateContext(
        ConquerTcpConnection connection,
        WorldSession session,
        WorldSessionStore sessions,
        TeamSessionStateStore teamStates)
    {
        return new WorldPacketHandlerContext(
            connection,
            TextWriter.Null,
            session,
            Sessions: sessions,
            TeamStates: teamStates);
    }

    private static WorldSession CreateSession(
        long uid,
        string name = "Caster",
        long mapId = 1015,
        long x = 830,
        long y = 584)
    {
        return new WorldSession(
            AccountId: uid,
            Username: name.ToLowerInvariant(),
            CreateCharacter(uid, name, mapId, x, y),
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }

    private static CharacterSnapshot CreateCharacter(long uid, string name, long mapId, long x, long y)
    {
        return new CharacterSnapshot(
            Uid: uid,
            Name: name,
            Body: 1_003,
            Face: 0,
            Hair: 0,
            Class: 142,
            FirstClass: 142,
            SecondClass: 0,
            Reborn: 0,
            Level: 120,
            Experience: 0,
            MapId: mapId,
            X: x,
            Y: y,
            PreviousMapId: mapId,
            PreviousMapX: x,
            PreviousMapY: y,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 300,
            AttributePoints: 0,
            MinHitPoints: 5_000,
            MinMana: 500,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://character",
            Attributes: new Dictionary<string, string>(),
            PaidNobility: new Dictionary<string, string>());
    }

    private static ConquerTcpConnection CreateConnection()
    {
        return new ConquerTcpConnection(null, null);
    }
}

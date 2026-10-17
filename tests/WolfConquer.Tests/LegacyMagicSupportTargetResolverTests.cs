using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMagicSupportTargetResolverTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1_293_600)]
    public void TryResolveVisiblePlayerTargetSkipsNoTargetAndSelf(uint targetUid)
    {
        var casterConnection = CreateConnection();
        var caster = CreateSession(1_293_600);
        var sessions = new WorldSessionStore();
        sessions.Set(casterConnection, caster);
        var context = CreateContext(casterConnection, caster, sessions);

        var resolved = LegacyMagicSupportTargetResolver.TryResolveVisiblePlayerTarget(
            context,
            caster,
            targetUid,
            out var target);

        Assert.False(resolved);
        Assert.Null(target);
    }

    [Fact]
    public void TryResolveVisiblePlayerTargetReturnsActiveVisibleTarget()
    {
        var casterConnection = CreateConnection();
        var targetConnection = CreateConnection();
        var caster = CreateSession(1_293_600, "Caster", x: 830, y: 584);
        var targetSession = CreateSession(1_293_601, "Target", x: 848, y: 584);
        var sessions = new WorldSessionStore();
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, targetSession);
        var context = CreateContext(casterConnection, caster, sessions);

        var resolved = LegacyMagicSupportTargetResolver.TryResolveVisiblePlayerTarget(
            context,
            caster,
            (uint)targetSession.Character.Uid,
            out var target);

        Assert.True(resolved);
        Assert.Same(targetConnection, target.ActiveSession.Connection);
        Assert.Same(targetSession, target.ActiveSession.Session);
        Assert.False(target.IsTeamMember);
    }

    [Fact]
    public void TryResolveVisiblePlayerTargetSkipsOutOfRangeTarget()
    {
        var casterConnection = CreateConnection();
        var targetConnection = CreateConnection();
        var caster = CreateSession(1_293_600, "Caster", x: 830, y: 584);
        var targetSession = CreateSession(1_293_601, "Target", x: 849, y: 584);
        var sessions = new WorldSessionStore();
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, targetSession);
        var context = CreateContext(casterConnection, caster, sessions);

        var resolved = LegacyMagicSupportTargetResolver.TryResolveVisiblePlayerTarget(
            context,
            caster,
            (uint)targetSession.Character.Uid,
            out var target);

        Assert.False(resolved);
        Assert.Null(target);
    }

    [Fact]
    public void TryResolveVisiblePlayerTargetSkipsOfflineOrMissingTarget()
    {
        var casterConnection = CreateConnection();
        var caster = CreateSession(1_293_600);
        var sessions = new WorldSessionStore();
        sessions.Set(casterConnection, caster);
        var context = CreateContext(casterConnection, caster, sessions);

        var resolved = LegacyMagicSupportTargetResolver.TryResolveVisiblePlayerTarget(
            context,
            caster,
            targetUid: 1_293_601,
            out var target);

        Assert.False(resolved);
        Assert.Null(target);
    }

    [Fact]
    public void TryResolveVisiblePlayerTargetFlagsCurrentTeammate()
    {
        var casterConnection = CreateConnection();
        var teammateConnection = CreateConnection();
        var caster = CreateSession(1_293_600, "Caster", x: 830, y: 584);
        var teammate = CreateSession(1_293_601, "Teammate", x: 831, y: 584);
        var sessions = new WorldSessionStore();
        sessions.Set(casterConnection, caster);
        sessions.Set(teammateConnection, teammate);
        var teamStates = new TeamSessionStateStore();
        teamStates.CreateTeam(casterConnection, caster.Character.Uid);
        Assert.True(teamStates.TryAddMember(casterConnection, teammateConnection, teammate.Character.Uid));
        var context = CreateContext(casterConnection, caster, sessions, teamStates);

        var resolved = LegacyMagicSupportTargetResolver.TryResolveVisiblePlayerTarget(
            context,
            caster,
            (uint)teammate.Character.Uid,
            out var target);

        Assert.True(resolved);
        Assert.Same(teammateConnection, target.ActiveSession.Connection);
        Assert.True(target.IsTeamMember);
    }

    private static WorldPacketHandlerContext CreateContext(
        ConquerTcpConnection connection,
        WorldSession session,
        WorldSessionStore sessions,
        TeamSessionStateStore? teamStates = null)
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

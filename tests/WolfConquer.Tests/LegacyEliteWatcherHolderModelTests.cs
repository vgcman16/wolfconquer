using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeTournamentWatcherHolderKeepsTypedReferenceUntilExplicitLeave(bool teamElite)
    {
        var fixture = new TournamentWatcherHolderFixture();
        var group = fixture.CreateGroup(teamElite);
        Assert.True(fixture.Set(fixture.Client, group));
        fixture.AssertGroup(fixture.Client, group);

        // IsWatching reads the holder, not coordinates or a fabricated Character attribute.
        var copied = fixture.Current with
        {
            Character = fixture.Current.Character with { MapId = 1_000, X = 50, Y = 50 },
        };
        fixture.Sessions.Set(fixture.Connection, copied);
        fixture.AssertGroup(fixture.Client, group);
        Assert.DoesNotContain("ElitePkWatchingGroup", copied.Character.Attributes.Keys);
        Assert.DoesNotContain("TeamElitePkWatchingGroup", copied.Character.Attributes.Keys);

        Assert.True(fixture.Clear(fixture.Client, group));
        fixture.AssertEmpty(fixture.Client);
        Assert.True(fixture.Clear(fixture.Client, group)); // Native terminal null assignment is idempotent.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeTournamentWatcherHolderFollowsClientNotRecordOrSameConnectionRebind(bool teamElite)
    {
        var fixture = new TournamentWatcherHolderFixture();
        var group = fixture.CreateGroup(teamElite);
        Assert.True(fixture.Set(fixture.Client, group));
        var copied = fixture.Current with
        {
            Character = fixture.Current.Character with { Name = "same-client-copy" },
        };
        fixture.Sessions.Set(fixture.Connection, copied);
        Assert.Same(copied, fixture.Current);
        fixture.AssertGroup(fixture.Client, group);

        Assert.True(fixture.Sessions.TryRemove(fixture.Connection, out var retired));
        Assert.Same(copied, retired);
        var replacement = CreateSession(mapId: 700);
        Assert.Equal(copied.Character.Uid, replacement.Character.Uid);
        fixture.Sessions.Set(fixture.Connection, replacement);
        Assert.False(fixture.Holders.TryRead(fixture.Client, out var stale));
        Assert.False(stale.IsWatching);
        Assert.False(fixture.Set(fixture.Client, fixture.CreateGroup(teamElite)));
        Assert.False(fixture.Clear(fixture.Client, group));

        Assert.True(fixture.Holders.TryCaptureClient(fixture.Connection, out var captured));
        var currentClient = Assert.IsType<LegacyTournamentWatcherClient>(captured);
        fixture.AssertEmpty(currentClient);
        var newGroup = fixture.CreateGroup(teamElite);
        Assert.True(fixture.Set(currentClient, newGroup));
        Assert.False(fixture.Clear(fixture.Client, group));
        fixture.AssertGroup(currentClient, newGroup);
        Assert.Same(replacement, fixture.Current);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeTournamentWatcherHolderUsesReferencesButTerminalClearIsNotGroupCas(bool teamElite)
    {
        var fixture = new TournamentWatcherHolderFixture();
        var group = fixture.CreateGroup(teamElite);
        Assert.True(fixture.Set(fixture.Client, group));

        var otherConnection = new ConquerTcpConnection(null, null);
        var otherSession = CreateSession(mapId: 700);
        Assert.Equal(fixture.Current.Character.Uid, otherSession.Character.Uid);
        fixture.Sessions.Set(otherConnection, otherSession);
        Assert.True(fixture.Holders.TryCaptureClient(otherConnection, out var captured));
        fixture.AssertEmpty(Assert.IsType<LegacyTournamentWatcherClient>(captured));

        var equalIdsButDifferentGroup = fixture.CreateGroup(teamElite);
        Assert.NotSame(group, equalIdsButDifferentGroup);
        Assert.True(fixture.Set(fixture.Client, equalIdsButDifferentGroup));
        fixture.AssertGroup(fixture.Client, equalIdsButDifferentGroup);
        // A's stale Watchers entry can later invoke A.DoLeaveWatching on this SAME live client.
        // Native terminal field=null clears B too; only replacement CLIENT identity is fenced.
        Assert.True(fixture.Clear(fixture.Client, group));
        fixture.AssertEmpty(fixture.Client);
    }

    [Fact]
    public void NativeTournamentWatcherFamiliesAreIndependentDirectFieldsOnOriginalClient()
    {
        var fixture = new TournamentWatcherHolderFixture();
        var elite = new LegacyElitePkWatchingGroup(matchId: 123, dynamicId: 456);
        var team = new LegacyTeamElitePkWatchingGroup(matchId: 123, dynamicId: 456);
        Assert.True(fixture.Holders.TrySetElitePkWatchingGroup(fixture.Client, elite));
        Assert.True(fixture.Holders.TrySetTeamElitePkWatchingGroup(fixture.Client, team));
        Assert.True(fixture.Holders.TryRead(fixture.Client, out var both));
        Assert.Same(elite, both.ElitePkWatchingGroup);
        Assert.Same(team, both.TeamElitePkWatchingGroup);
        var foreignHolders = new LegacyTournamentWatcherHolders(fixture.Sessions);
        Assert.False(foreignHolders.TrySetTeamElitePkWatchingGroup(fixture.Client, null));

        Assert.True(fixture.Holders.TrySetElitePkWatchingGroup(fixture.Client, null));
        fixture.AssertGroup(fixture.Client, team);
        Assert.True(fixture.Holders.TrySetTeamElitePkWatchingGroup(fixture.Client, null));
        fixture.AssertEmpty(fixture.Client);
    }

    [Fact]
    public void NativeTournamentWatcherHolderRequiresRealRegistrationNotNoStoreOrUidFallback()
    {
        var sessions = new WorldSessionStore();
        var holders = new LegacyTournamentWatcherHolders(sessions);
        var connection = new ConquerTcpConnection(null, null);
        Assert.False(holders.TryCaptureClient(connection, out var client));
        Assert.Null(client);
        Assert.Throws<ArgumentNullException>(() => new LegacyTournamentWatcherHolders(null!));
    }

    private sealed class TournamentWatcherHolderFixture
    {
        public WorldSessionStore Sessions { get; } = new();
        public ConquerTcpConnection Connection { get; } = new(null, null);
        public LegacyTournamentWatcherHolders Holders { get; }
        public LegacyTournamentWatcherClient Client { get; }

        public TournamentWatcherHolderFixture()
        {
            var session = CreateSession(mapId: 700);
            Sessions.Set(Connection, session);
            Holders = new(Sessions);
            Assert.Same(session, Current);
            Assert.True(Holders.TryCaptureClient(Connection, out var client));
            Client = Assert.IsType<LegacyTournamentWatcherClient>(client);
            AssertEmpty(Client);
        }

        public WorldSession Current => Assert.IsType<WorldSession>(
            Sessions.TryGet(Connection, out var current) ? current : null);

        public object CreateGroup(bool teamElite) => teamElite
            ? new LegacyTeamElitePkWatchingGroup(matchId: 123, dynamicId: 456)
            : new LegacyElitePkWatchingGroup(matchId: 123, dynamicId: 456);

        public bool Set(LegacyTournamentWatcherClient client, object group) => group switch
        {
            LegacyElitePkWatchingGroup elite => Holders.TrySetElitePkWatchingGroup(client, elite),
            LegacyTeamElitePkWatchingGroup team => Holders.TrySetTeamElitePkWatchingGroup(client, team),
            _ => throw new ArgumentException("Typed native watcher group required.", nameof(group)),
        };

        public bool Clear(LegacyTournamentWatcherClient client, object group) => group switch
        {
            LegacyElitePkWatchingGroup elite => Holders.TrySetElitePkWatchingGroup(client, null),
            LegacyTeamElitePkWatchingGroup team => Holders.TrySetTeamElitePkWatchingGroup(client, null),
            _ => throw new ArgumentException("Typed native watcher group required.", nameof(group)),
        };

        public void AssertGroup(LegacyTournamentWatcherClient client, object group)
        {
            Assert.True(Holders.TryRead(client, out var snapshot));
            Assert.True(snapshot.IsWatching);
            if (group is LegacyElitePkWatchingGroup elite)
            {
                Assert.Same(elite, snapshot.ElitePkWatchingGroup);
                Assert.Null(snapshot.TeamElitePkWatchingGroup);
                Assert.Equal(123U, elite.MatchId);
                Assert.Equal(456U, elite.DynamicId);
            }
            else
            {
                var team = Assert.IsType<LegacyTeamElitePkWatchingGroup>(group);
                Assert.Same(team, snapshot.TeamElitePkWatchingGroup);
                Assert.Null(snapshot.ElitePkWatchingGroup);
                Assert.Equal(123U, team.MatchId);
                Assert.Equal(456U, team.DynamicId);
            }
        }

        public void AssertEmpty(LegacyTournamentWatcherClient client)
        {
            Assert.True(Holders.TryRead(client, out var snapshot));
            Assert.False(snapshot.IsWatching);
            Assert.Null(snapshot.ElitePkWatchingGroup);
            Assert.Null(snapshot.TeamElitePkWatchingGroup);
        }
    }
}

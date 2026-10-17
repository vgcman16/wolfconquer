using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class TeamSessionStateStoreTests
{
    [Fact]
    public void CreateTeamInitializesLegacyDefaultState()
    {
        var store = new TeamSessionStateStore();
        var connection = CreateConnection();

        store.CreateTeam(connection, 1_293_600);

        Assert.True(store.HasTeam(connection));
        Assert.Equal(1u, store.GetMemberCount(connection));
        Assert.Equal(1_293_600, store.GetLeaderUid(connection));
        Assert.Equal(1u, store.GetTeamUid(connection));
        Assert.True(store.IsLeader(connection, 1_293_600));
        Assert.False(store.IsFull(connection));
        Assert.Collection(
            store.GetConnections(connection),
            teamConnection => Assert.Same(connection, teamConnection));
        Assert.False(store.GetForbidJoin(connection));
        Assert.True(store.GetPickupMoney(connection));
        Assert.False(store.GetPickupItems(connection));
    }

    [Fact]
    public void DismissTeamClearsCurrentState()
    {
        var store = new TeamSessionStateStore();
        var connection = CreateConnection();
        store.CreateTeam(connection, 1_293_600);

        store.DismissTeam(connection);

        Assert.False(store.HasTeam(connection));
        Assert.Equal(0u, store.GetMemberCount(connection));
        Assert.Null(store.GetLeaderUid(connection));
        Assert.Equal(0u, store.GetTeamUid(connection));
        Assert.Empty(store.GetConnections(connection));
        Assert.False(store.IsLeader(connection, 1_293_600));
    }

    [Fact]
    public void ExitTeamClearsLoneLeaderState()
    {
        var store = new TeamSessionStateStore();
        var connection = CreateConnection();
        store.CreateTeam(connection, 1_293_600);

        store.ExitTeam(connection);

        Assert.False(store.HasTeam(connection));
        Assert.Equal(0u, store.GetMemberCount(connection));
        Assert.Null(store.GetLeaderUid(connection));
        Assert.Equal(0u, store.GetTeamUid(connection));
        Assert.Empty(store.GetConnections(connection));
        Assert.False(store.IsLeader(connection, 1_293_600));
    }

    [Fact]
    public void IsLeaderComparesRequestedCharacterUid()
    {
        var store = new TeamSessionStateStore();
        var connection = CreateConnection();
        store.CreateTeam(connection, 1_293_600);

        Assert.True(store.IsLeader(connection, 1_293_600));
        Assert.False(store.IsLeader(connection, 1_293_601));
    }

    [Fact]
    public void TeamOptionTogglesUpdateExistingState()
    {
        var store = new TeamSessionStateStore();
        var connection = CreateConnection();
        store.CreateTeam(connection, 1_293_600);

        store.SetForbidJoin(connection, true);
        store.SetPickupMoney(connection, false);
        store.SetPickupItems(connection, true);

        Assert.True(store.GetForbidJoin(connection));
        Assert.False(store.GetPickupMoney(connection));
        Assert.True(store.GetPickupItems(connection));

        store.SetForbidJoin(connection, false);
        store.SetPickupMoney(connection, true);
        store.SetPickupItems(connection, false);

        Assert.False(store.GetForbidJoin(connection));
        Assert.True(store.GetPickupMoney(connection));
        Assert.False(store.GetPickupItems(connection));
    }

    [Fact]
    public void IsTeamMemberChecksCurrentTeamUidMembership()
    {
        var store = new TeamSessionStateStore();
        var leader = CreateConnection();
        var member = CreateConnection();
        store.CreateTeam(leader, 1_293_600);
        Assert.True(store.TryAddMember(leader, member, 1_293_601));

        Assert.True(store.IsTeamMember(leader, 1_293_600));
        Assert.True(store.IsTeamMember(member, 1_293_600));
        Assert.True(store.IsTeamMember(leader, 1_293_601));
        Assert.False(store.IsTeamMember(member, 1_293_602));
        Assert.False(store.IsTeamMember(CreateConnection(), 1_293_600));
    }

    [Fact]
    public void AllowsProtectedFloorPickupMatchesLegacyTeamLootDefaults()
    {
        var store = new TeamSessionStateStore();
        var leader = CreateConnection();
        var member = CreateConnection();
        store.CreateTeam(leader, 1_293_600);
        Assert.True(store.TryAddMember(leader, member, 1_293_601));

        Assert.True(store.AllowsProtectedFloorPickup(member, ownerUid: 1_293_600, FloorItemRuntimePickupKind.Money));
        Assert.True(store.AllowsProtectedFloorPickup(member, ownerUid: 4_000_000, FloorItemRuntimePickupKind.Money));
        Assert.False(store.AllowsProtectedFloorPickup(member, ownerUid: 1_293_600, FloorItemRuntimePickupKind.Item));
        Assert.False(store.AllowsProtectedFloorPickup(CreateConnection(), ownerUid: 1_293_600, FloorItemRuntimePickupKind.Money));
    }

    [Fact]
    public void AllowsProtectedFloorPickupRequiresPickupItemsAndOwnerMembershipForNonMoney()
    {
        var store = new TeamSessionStateStore();
        var leader = CreateConnection();
        var member = CreateConnection();
        store.CreateTeam(leader, 1_293_600);
        Assert.True(store.TryAddMember(leader, member, 1_293_601));

        store.SetPickupItems(member, true);

        Assert.True(store.AllowsProtectedFloorPickup(member, ownerUid: 1_293_600, FloorItemRuntimePickupKind.Item));
        Assert.True(store.AllowsProtectedFloorPickup(member, ownerUid: 1_293_600, FloorItemRuntimePickupKind.ConquerPoints));
        Assert.False(store.AllowsProtectedFloorPickup(member, ownerUid: 4_000_000, FloorItemRuntimePickupKind.Item));
    }

    [Fact]
    public void TryAddMemberJoinsLeaderTeamAndSharesTeamState()
    {
        var store = new TeamSessionStateStore();
        var leader = CreateConnection();
        var member = CreateConnection();
        store.CreateTeam(leader, 1_293_600);

        var teamUid = store.GetTeamUid(leader);
        var added = store.TryAddMember(leader, member, 1_293_601);

        Assert.True(added);
        Assert.True(store.HasTeam(member));
        Assert.Equal(2u, store.GetMemberCount(leader));
        Assert.Equal(2u, store.GetMemberCount(member));
        Assert.Equal(1_293_600, store.GetLeaderUid(leader));
        Assert.Equal(1_293_600, store.GetLeaderUid(member));
        Assert.Equal(teamUid, store.GetTeamUid(member));
        Assert.True(store.IsLeader(leader, 1_293_600));
        Assert.False(store.IsLeader(member, 1_293_601));
        Assert.False(store.IsLeader(member, 1_293_600));
        Assert.Collection(
            store.GetConnections(member),
            teamConnection => Assert.Same(leader, teamConnection),
            teamConnection => Assert.Same(member, teamConnection));

        store.SetForbidJoin(member, true);
        store.SetPickupMoney(member, false);
        store.SetPickupItems(member, true);

        Assert.True(store.GetForbidJoin(leader));
        Assert.False(store.GetPickupMoney(leader));
        Assert.True(store.GetPickupItems(leader));
    }

    [Fact]
    public void TryAddMemberRejectsFullTeams()
    {
        var store = new TeamSessionStateStore();
        var leader = CreateConnection();
        store.CreateTeam(leader, 1_293_600);

        Assert.True(store.TryAddMember(leader, CreateConnection(), 1_293_601));
        Assert.True(store.TryAddMember(leader, CreateConnection(), 1_293_602));
        Assert.True(store.TryAddMember(leader, CreateConnection(), 1_293_603));
        Assert.True(store.TryAddMember(leader, CreateConnection(), 1_293_604));
        var rejectedMember = CreateConnection();

        Assert.True(store.IsFull(leader));
        Assert.False(store.TryAddMember(leader, rejectedMember, 1_293_605));
        Assert.False(store.HasTeam(rejectedMember));
        Assert.Equal(5u, store.GetMemberCount(leader));
        Assert.Equal(5, store.GetConnections(leader).Count);
    }

    [Fact]
    public void TryAddMemberRejectsMemberAlreadyInTeam()
    {
        var store = new TeamSessionStateStore();
        var leader = CreateConnection();
        var member = CreateConnection();
        store.CreateTeam(leader, 1_293_600);
        store.CreateTeam(member, 1_293_601);

        Assert.False(store.TryAddMember(leader, member, 1_293_601));
        Assert.Equal(1u, store.GetMemberCount(leader));
        Assert.Equal(1u, store.GetMemberCount(member));
        Assert.Equal(1_293_601, store.GetLeaderUid(member));
        Assert.True(store.IsLeader(member, 1_293_601));
    }

    [Fact]
    public void DismissTeamClearsAllMembers()
    {
        var store = new TeamSessionStateStore();
        var leader = CreateConnection();
        var firstMember = CreateConnection();
        var secondMember = CreateConnection();
        store.CreateTeam(leader, 1_293_600);
        Assert.True(store.TryAddMember(leader, firstMember, 1_293_601));
        Assert.True(store.TryAddMember(leader, secondMember, 1_293_602));

        store.DismissTeam(leader);

        Assert.False(store.HasTeam(leader));
        Assert.False(store.HasTeam(firstMember));
        Assert.False(store.HasTeam(secondMember));
        Assert.Equal(0u, store.GetMemberCount(leader));
        Assert.Equal(0u, store.GetMemberCount(firstMember));
        Assert.Equal(0u, store.GetMemberCount(secondMember));
        Assert.Empty(store.GetConnections(leader));
        Assert.Empty(store.GetConnections(firstMember));
        Assert.Empty(store.GetConnections(secondMember));
    }

    [Fact]
    public void ExitTeamClearsOnlyNonLeaderMember()
    {
        var store = new TeamSessionStateStore();
        var leader = CreateConnection();
        var firstMember = CreateConnection();
        var secondMember = CreateConnection();
        store.CreateTeam(leader, 1_293_600);
        Assert.True(store.TryAddMember(leader, firstMember, 1_293_601));
        Assert.True(store.TryAddMember(leader, secondMember, 1_293_602));
        var teamUid = store.GetTeamUid(leader);

        store.ExitTeam(firstMember);

        Assert.False(store.HasTeam(firstMember));
        Assert.True(store.HasTeam(leader));
        Assert.True(store.HasTeam(secondMember));
        Assert.Equal(2u, store.GetMemberCount(leader));
        Assert.Equal(2u, store.GetMemberCount(secondMember));
        Assert.Equal(teamUid, store.GetTeamUid(leader));
        Assert.Equal(teamUid, store.GetTeamUid(secondMember));
        Assert.Equal(1_293_600, store.GetLeaderUid(leader));
        Assert.Equal(1_293_600, store.GetLeaderUid(secondMember));
        Assert.True(store.IsLeader(leader, 1_293_600));
        Assert.False(store.IsLeader(secondMember, 1_293_602));
        Assert.Collection(
            store.GetConnections(leader),
            teamConnection => Assert.Same(leader, teamConnection),
            teamConnection => Assert.Same(secondMember, teamConnection));
    }

    [Fact]
    public void ExitTeamPromotesNextMemberWhenLeaderLeaves()
    {
        var store = new TeamSessionStateStore();
        var leader = CreateConnection();
        var firstMember = CreateConnection();
        var secondMember = CreateConnection();
        store.CreateTeam(leader, 1_293_600);
        Assert.True(store.TryAddMember(leader, firstMember, 1_293_601));
        Assert.True(store.TryAddMember(leader, secondMember, 1_293_602));
        store.SetForbidJoin(leader, true);
        store.SetPickupMoney(leader, false);
        store.SetPickupItems(leader, true);
        var teamUid = store.GetTeamUid(leader);

        store.ExitTeam(leader);

        Assert.False(store.HasTeam(leader));
        Assert.True(store.HasTeam(firstMember));
        Assert.True(store.HasTeam(secondMember));
        Assert.Equal(2u, store.GetMemberCount(firstMember));
        Assert.Equal(2u, store.GetMemberCount(secondMember));
        Assert.Equal(1_293_601, store.GetLeaderUid(firstMember));
        Assert.Equal(1_293_601, store.GetLeaderUid(secondMember));
        Assert.Equal(teamUid, store.GetTeamUid(firstMember));
        Assert.Equal(teamUid, store.GetTeamUid(secondMember));
        Assert.True(store.IsLeader(firstMember, 1_293_601));
        Assert.False(store.IsLeader(secondMember, 1_293_602));
        Assert.False(store.IsLeader(firstMember, 1_293_600));
        Assert.True(store.GetForbidJoin(firstMember));
        Assert.False(store.GetPickupMoney(firstMember));
        Assert.True(store.GetPickupItems(firstMember));
        Assert.Collection(
            store.GetConnections(firstMember),
            teamConnection => Assert.Same(firstMember, teamConnection),
            teamConnection => Assert.Same(secondMember, teamConnection));
    }

    private static ConquerTcpConnection CreateConnection()
    {
        return new ConquerTcpConnection(null, null);
    }
}

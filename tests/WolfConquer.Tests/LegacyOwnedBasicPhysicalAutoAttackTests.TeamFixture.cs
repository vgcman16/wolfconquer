using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    private static TeamSessionStateStore ActualTeams(AttackFixture f) =>
        Assert.IsType<TeamSessionStateStore>(typeof(WorldServerFrameHandler)
            .GetField("_teamStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Owner.Handler));

    private static async Task FormActualTeamAsync(AttackFixture f, bool actorIsLeader)
    {
        uint leader = actorIsLeader ? Actor : Target;
        uint member = actorIsLeader ? Target : Actor;
        var teams = ActualTeams(f);
        await f.Owner.SendAsync(leader, new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes());
        Assert.True(teams.IsLeader(f.Owner.Connection(leader), leader));
        await f.Owner.SendAsync(leader, new MsgTeamPacket(MsgTeamType.InviteRequest, member).ToFrameBytes());
        Assert.Contains(f.Owner.Frames(member), bytes => MsgTeamPacket.TryParse(
            OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
            packet.Type == MsgTeamType.InviteRequest && packet.Uid == leader);
        await f.Owner.SendAsync(member, new MsgTeamPacket(MsgTeamType.AcceptInvitation, leader).ToFrameBytes());
        Assert.True(teams.HasTeam(f.Owner.Connection(leader)));
        Assert.True(teams.HasTeam(f.Owner.Connection(member)));
        Assert.Equal(2U, teams.GetMemberCount(f.Owner.Connection(leader)));
        Assert.Equal(2U, teams.GetMemberCount(f.Owner.Connection(member)));
        Assert.Equal((long)leader, teams.GetLeaderUid(f.Owner.Connection(member)));
        Assert.Contains(f.Owner.Connection(member), teams.GetConnections(f.Owner.Connection(leader)));
        f.Clear();
    }

    private static async Task SetActualTeamPkAndSitAsync(AttackFixture f)
    {
        await f.Owner.SendAsync(Actor, new MsgActionPacket
            { Type = MsgActionType.SetPkMode, DwParam = 2 }.ToFrameBytes());
        Assert.Equal("2", f.Current().Character.Attributes["PkMode"]);
        await f.SitAsync();
    }

    private static async Task ExitActualTargetTeamAsync(AttackFixture f)
    {
        var teams = ActualTeams(f);
        await f.Owner.SendAsync(Target, new MsgTeamPacket(MsgTeamType.ExitTeam, 0).ToFrameBytes());
        Assert.False(teams.HasTeam(f.Owner.Connection(Target)));
        Assert.True(teams.HasTeam(f.Owner.Connection(Actor)));
        Assert.Equal(1U, teams.GetMemberCount(f.Owner.Connection(Actor)));
        Assert.Contains(f.Owner.Frames(Actor), bytes => MsgTeamPacket.TryParse(
            OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
            packet.Type == MsgTeamType.ExitTeam && packet.Uid == Target);
        f.Clear();
    }
}

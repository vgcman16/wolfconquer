using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task ActualTeamPkRefusesLeaderOrMemberUntilTargetExits(bool actorIsLeader) => Run(async f =>
    {
        await FormActualTeamAsync(f, actorIsLeader);
        await SetActualTeamPkAndSitAsync(f);
        var before = f.Files();
        var stamp = f.Binding().Player.AttackStamp;
        var clock = f.Clock.GetTimestamp();
        await f.AttackAsync();
        f.AssertFiles(before); f.AssertCoherent(2000); Assert.Empty(f.Order);
        Assert.Equal(250U, LegacyActionState.RequireCurrent(f.Current().Character));
        Assert.Equal(250U, (uint)f.Binding().Player.Action);
        Assert.Equal(stamp, f.Binding().Player.AttackStamp);
        Assert.False(f.Binding().Client.OnAutoAttack);
        await f.TickAsync();
        f.AssertFiles(before); f.AssertCoherent(2000); Assert.Empty(f.Order);
        Assert.Equal(250U, LegacyActionState.RequireCurrent(f.Current().Character));
        Assert.Equal(250U, (uint)f.Binding().Player.Action);
        Assert.Equal(stamp, f.Binding().Player.AttackStamp);
        Assert.False(f.Binding().Client.OnAutoAttack);

        await ExitActualTargetTeamAsync(f);
        Assert.Equal(clock, f.Clock.GetTimestamp());
        await f.HitAsync(2000, 1700, repeat: false);
    });
}

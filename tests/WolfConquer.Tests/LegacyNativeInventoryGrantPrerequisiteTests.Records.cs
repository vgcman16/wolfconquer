using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryGrantPrerequisiteTests
{
    [Fact]
    public Task GenuineRecordConstructorsAllocateDistinctInfoAndDoNotBindClientOrRegisterPools() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var arena = new LegacyNativeArenaUser();
        var team = new LegacyNativeTeamArenaUser();
        Assert.Equal("None", arena.Name); Assert.Equal("None", team.Name);
        Assert.Equal(0u, arena.UID); Assert.Equal(0u, team.UID);
        Assert.Equal(0u, arena.Info.ArenaPoints); Assert.Equal(0u, team.Info.ArenaPoints);
        Assert.NotSame(arena.Info, new LegacyNativeArenaUser().Info);
        Assert.NotSame(team.Info, new LegacyNativeTeamArenaUser().Info);
        Assert.Equal(LegacyNativeArenaUser.StateType.None, arena.ArenaState);
        Assert.Equal(LegacyNativeArenaUser.MatchStatus.None, arena.QualifierStatus);
        Assert.Equal(default(DateTime), arena.AcceptBoxShow);
        Assert.False(arena.AcceptBox);
        Assert.Null(scope.Client.ArenaStatistic); Assert.Null(scope.Client.TeamArenaStatistic);
        Assert.Empty(LegacyNativeArenaRankings.ArenaPoll); Assert.Empty(LegacyNativeTeamArenaRankings.ArenaPoll);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task ArenaResetChangesOnlyItsFourSourceSlotsAndKeepsRecordInfoIdentity() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var user = ArenaRow();
        var info = user.Info;
        user.ArenaState = LegacyNativeArenaUser.StateType.Fight; user.AcceptBox = true;
        user.AcceptBoxShow = new DateTime(2026, 1, 2); user.QualifierStatus = LegacyNativeArenaUser.MatchStatus.Winner;
        user.Cheers = 4; user.Damage = 99; info.Status = LegacyNativeMsgArenaInfo.Action.WaitingInactive;
        user.Reset();
        Assert.Same(info, user.Info); Assert.Equal(LegacyNativeArenaUser.StateType.None, user.ArenaState);
        Assert.False(user.AcceptBox); Assert.Equal(0u, user.Cheers);
        Assert.Equal(LegacyNativeMsgArenaInfo.Action.NotSignedUp, info.Status);
        Assert.Equal(new DateTime(2026, 1, 2), user.AcceptBoxShow);
        Assert.Equal(LegacyNativeArenaUser.MatchStatus.Winner, user.QualifierStatus);
        Assert.Equal(99u, user.Damage); Assert.Equal(100u, info.ArenaPoints);
        Assert.Equal(8u, user.LastSeasonArenaPoints); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task ArenaResetNullInfoFaultOccursAfterStateAndAcceptButBeforeCheers() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var user = new LegacyNativeArenaUser { ArenaState = LegacyNativeArenaUser.StateType.Fight,
            AcceptBox = true, Cheers = 77, Info = null! };
        Assert.Throws<NullReferenceException>(() => user.Reset());
        Assert.Equal(LegacyNativeArenaUser.StateType.None, user.ArenaState); Assert.False(user.AcceptBox);
        Assert.Equal(77u, user.Cheers);
        var team = new LegacyNativeTeamArenaUser { Cheers = 77, Info = null! };
        team.Reset(); Assert.Equal(0u, team.Cheers); Assert.Null(team.Info);
    });

    [Theory]
    [InlineData(0u, 1)] [InlineData(1u, 1)] [InlineData(2u, 1)] [InlineData(3u, 0)]
    [InlineData(9u, 0)] [InlineData(10u, 1)] [InlineData(1002u, 1)] [InlineData(1003u, 0)]
    [InlineData(uint.MaxValue, 0)]
    public Task TeamGenderUsesOnlyNativeMeshLastDigit(uint mesh, byte expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Equal(expected, new LegacyNativeTeamArenaUser { Mesh = mesh }.GetGender);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task BothApplyMethodsReadTheActualPlayerWithoutRegisteringOrRetainingAProjection() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.PreexistingLevelAndClass(130, 16004);
        scope.Player.Name = "Native"; scope.Player.Body = 1003; scope.Player.Face = 4;
        var arena = new LegacyNativeArenaUser(); var team = new LegacyNativeTeamArenaUser();
        arena.ApplayInfo(scope.Player); team.ApplayInfo(scope.Player);
        Assert.Equal("Native", arena.Name); Assert.Equal("Native", team.Name);
        Assert.Equal(17u, arena.UID); Assert.Equal(17u, team.UID);
        Assert.Equal((ushort)130, arena.Level); Assert.Equal((ushort)130, team.Level);
        Assert.Equal(16004u, arena.Class); Assert.Equal(16004u, team.Class);
        Assert.Equal(41003u, arena.Mesh); Assert.Equal(41003u, team.Mesh);
        scope.Player.Name = "Later"; Assert.Equal("Native", arena.Name);
        Assert.Throws<NullReferenceException>(() => arena.ApplayInfo(null!));
        Assert.Throws<NullReferenceException>(() => team.ApplayInfo(null!));
        Assert.Equal("Native", arena.Name); Assert.Equal("Native", team.Name);
        Assert.Empty(LegacyNativeArenaRankings.ArenaPoll); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task FourClientPointPropertiesIgnoreAbsentOwnersAndUseCurrentPhysicalInfoWhenBound() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.ArenaPoints = 1; scope.Client.ArenaHonorPoints = 2;
        scope.Client.TeamArenaPoints = 3; scope.Client.TeamArenaHonorPoints = 4;
        Assert.Equal(0u, scope.Client.ArenaPoints); Assert.Equal(0u, scope.Client.ArenaHonorPoints);
        Assert.Equal(0u, scope.Client.TeamArenaPoints); Assert.Equal(0u, scope.Client.TeamArenaHonorPoints);
        Assert.Null(scope.Client.ArenaStatistic); Assert.Null(scope.Client.TeamArenaStatistic);
        var arena = new LegacyNativeArenaUser(); var team = new LegacyNativeTeamArenaUser();
        scope.Client.ArenaStatistic = arena; scope.Client.TeamArenaStatistic = team;
        scope.Client.ArenaPoints = uint.MaxValue; scope.Client.ArenaHonorPoints = 20;
        scope.Client.TeamArenaPoints = 30; scope.Client.TeamArenaHonorPoints = 40;
        Assert.Equal(uint.MaxValue, arena.Info.ArenaPoints); Assert.Equal(20u, arena.Info.CurrentHonor);
        Assert.Equal(30u, team.Info.ArenaPoints); Assert.Equal(40u, team.Info.CurrentHonor);
        arena.Info = new LegacyNativeMsgArenaInfo { ArenaPoints = 5, CurrentHonor = 6 };
        team.Info = new LegacyNativeMsgTeamArenaInfo { ArenaPoints = 7, CurrentHonor = 8 };
        Assert.Equal(5u, scope.Client.ArenaPoints); Assert.Equal(6u, scope.Client.ArenaHonorPoints);
        Assert.Equal(7u, scope.Client.TeamArenaPoints); Assert.Equal(8u, scope.Client.TeamArenaHonorPoints);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task BoundClientRecordsDoNotRepairNullInfoForAnyOfTheFourPointProperties() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.ArenaStatistic = new LegacyNativeArenaUser { Info = null! };
        scope.Client.TeamArenaStatistic = new LegacyNativeTeamArenaUser { Info = null! };
        Assert.Throws<NullReferenceException>(() => scope.Client.ArenaPoints);
        Assert.Throws<NullReferenceException>(() => scope.Client.ArenaHonorPoints);
        Assert.Throws<NullReferenceException>(() => scope.Client.TeamArenaPoints);
        Assert.Throws<NullReferenceException>(() => scope.Client.TeamArenaHonorPoints);
        Assert.Throws<NullReferenceException>(() => scope.Client.ArenaPoints = 1);
        Assert.Throws<NullReferenceException>(() => scope.Client.ArenaHonorPoints = 1);
        Assert.Throws<NullReferenceException>(() => scope.Client.TeamArenaPoints = 1);
        Assert.Throws<NullReferenceException>(() => scope.Client.TeamArenaHonorPoints = 1);
        Assert.Empty(scope.Queue);
    });
}

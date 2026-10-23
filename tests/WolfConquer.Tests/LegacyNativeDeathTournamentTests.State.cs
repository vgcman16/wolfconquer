using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDeathTournamentTests
{
    [Fact]
    public void ConstructorsPreserveTrueUnallocatedStateAndUnknownType()
    {
        var type = (LegacyNativeTournamentType)255;
        var killer = new LegacyNativeMsgTheKillerOfElite(type);
        var extreme = new LegacyNativeMsgExtremePk(type);
        var last = new LegacyNativeMsgLastManStand(type);
        var team = new LegacyNativeMsgTeamDeathMatch(type);
        Assert.Equal(type, killer.Type); Assert.Equal(type, extreme.Type); Assert.Equal(type, last.Type); Assert.Equal(type, team.Type);
        Assert.Equal(LegacyNativeProcesType.Dead, killer.Process); Assert.Equal(LegacyNativeProcesType.Dead, extreme.Process);
        Assert.Equal(LegacyNativeProcesType.Dead, last.Process); Assert.Equal(LegacyNativeProcesType.Dead, team.Process);
        Assert.Null(killer.Map); Assert.Null(extreme.Map); Assert.Null(last.Map); Assert.Null(team.Teams);
        Assert.Null(killer.KillSystem); Assert.Null(extreme.KillSystem); Assert.Null(last.KillSystem); Assert.Null(team.KillSystem);
        Assert.Equal(0u, killer.Secounds); Assert.Equal(0u, extreme.Secounds); Assert.Equal(60u, last.Secounds);
        Assert.Equal(default, killer.StartTimer); Assert.Equal(default, extreme.ScoreStamp); Assert.Equal(default, last.InfoTimer);
        Assert.Equal(0u, killer.DinamicID); Assert.Equal(0u, extreme.DinamicID); Assert.Equal(0u, last.DinamicMap);
    }
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "sound/fc1.wav")]
    [InlineData(2, "sound/fc2.wav")]
    [InlineData(3, "sound/fc3.wav")]
    [InlineData(4, "sound/fc4.wav")]
    [InlineData(255, "none")]
    public void KillerSoundUsesExactRawFlag(byte flag, string expected)
    {
        var state = new LegacyNativeKillerSystem { Flag = (LegacyNativeKillerSystem.KillFlag)flag };
        Assert.Equal(expected, LegacyNativeKillerSystem.GetSound(state));
        Assert.Equal((LegacyNativeKillerSystem.KillFlag)flag, state.Flag);
    }
    [Fact]
    public void KillerSoundNullFaultAndCheckDeadZeroIdentityAreNotNormalized()
    {
        Assert.Throws<NullReferenceException>(() => LegacyNativeKillerSystem.GetSound(null!));
        var state = new LegacyNativeKillerSystem();
        Assert.Equal(0u, state.UID); Assert.False(state.Dead);
        state.CheckDead(4); Assert.False(state.Dead);
        state.CheckDead(0); Assert.True(state.Dead);
        state.UID = 5; state.CheckDead(4); Assert.True(state.Dead);
    }
    [Theory]
    [InlineData(0, 185825u)]
    [InlineData(1, 185625u)]
    [InlineData(2, 181525u)]
    [InlineData(3, 185325u)]
    [InlineData(4, 0u)]
    [InlineData(255, 0u)]
    public void GarmentMappingsRetainUnknownSentinel(byte team, uint garment)
    {
        var owner = new LegacyNativeMsgTeamDeathMatch((LegacyNativeTournamentType)0);
        Assert.Equal(garment, owner.GetGarmentID((LegacyNativeMsgTeamDeathMatch.TeamTyps)team));
        Assert.Equal(garment == 0 ? LegacyNativeMsgTeamDeathMatch.TeamTyps.Count : (LegacyNativeMsgTeamDeathMatch.TeamTyps)team,
            owner.GetTeamTyp(garment));
    }
    [Fact]
    public void FinishTimerRetainsDateOverflowRatherThanClamping()
    {
        var owner = new LegacyNativeMsgTeamDeathMatch((LegacyNativeTournamentType)0) { StartTimer = DateTime.MaxValue };
        Assert.Throws<ArgumentOutOfRangeException>(() => owner.FinishTimer());
    }
    [Fact]
    public void FinishTimerUsesComponentMinutesAndSecondsAtTenMinutes()
    {
        var owner = new LegacyNativeMsgTeamDeathMatch((LegacyNativeTournamentType)0)
            { StartTimer = new DateTime(2026, 1, 1, 12, 57, 45) };
        var before = DateTime.Now;
        var actual = owner.FinishTimer();
        var after = DateTime.Now;
        // Enumerate only the observed clock window; no injected production clock.
        var possible = new HashSet<string>();
        for (long tick = before.Ticks / TimeSpan.TicksPerSecond; tick <= after.Ticks / TimeSpan.TicksPerSecond; tick++)
        {
            var current = new TimeSpan(tick * TimeSpan.TicksPerSecond);
            var finish = new TimeSpan(owner.StartTimer.AddMinutes(10).Ticks);
            possible.Add((finish.Minutes - current.Minutes) + " : " + (finish.Seconds - current.Seconds));
        }
        Assert.Contains(actual, possible);
    }
}

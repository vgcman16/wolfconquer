using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryGrantPrerequisiteTests
{
    [Fact]
    public Task BothRecordWritersRetainExactSixteenFieldsTrailingDelimiterAndNativeSpelling() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Equal(NativeRow, ArenaRow().ToString());
        Assert.Equal(NativeRow, TeamRow().ToString());
    });

    [Fact]
    public Task BothLoadersKeepNativeDuplicateTodayWinAndUnloadedTotalWinRatherThanFixingRoundTrip() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var arena = new LegacyNativeArenaUser(); var team = new LegacyNativeTeamArenaUser();
        var originalArenaInfo = arena.Info; var originalTeamInfo = team.Info;
        arena.Info.TotalWin = 777; team.Info.TotalWin = 888;
        arena.Info.TodayRank = 99; team.Info.TodayRank = 98;
        arena.Load(NativeRow); team.Load(NativeRow);
        Assert.Same(originalArenaInfo, arena.Info); Assert.Same(originalTeamInfo, team.Info);
        Assert.Equal(7u, arena.Info.TodayWin); Assert.Equal(7u, team.Info.TodayWin);
        Assert.Equal(777u, arena.Info.TotalWin); Assert.Equal(888u, team.Info.TotalWin);
        Assert.Equal(99u, arena.Info.TodayRank); Assert.Equal(98u, team.Info.TodayRank);
        Assert.Equal(16004u, arena.Class); Assert.Equal(0u, team.Class);
        Assert.Equal("1/Alpha/130/16004/1003/100/200/300/4/7/6/777/8/9/10/11/", arena.ToString());
        Assert.Equal("1/Alpha/130/0/1003/100/200/300/4/7/6/888/8/9/10/11/", team.ToString());
    });

    [Fact]
    public Task NullLoadIsATrueNoOpIncludingMalformedInfoReference() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var arena = ArenaRow(); var team = TeamRow();
        arena.Info = null!; team.Info = null!;
        arena.Load(null); team.Load(null);
        Assert.Equal(1u, arena.UID); Assert.Equal(1u, team.UID);
        Assert.Equal("Alpha", arena.Name); Assert.Equal("Alpha", team.Name);
        Assert.Null(arena.Info); Assert.Null(team.Info);
        Assert.Equal(8u, arena.LastSeasonArenaPoints); Assert.Equal(8u, team.LastSeasonArenaPoints);
    });

    [Theory]
    [InlineData("", "None")]
    [InlineData("not-a-delimited-row", "None")]
    [InlineData("/", "")]
    public Task InvalidAndTruncatedRowsUseActualNativeReadLineDefaultsWithoutResettingTotalWin(string line,
        string name) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var arena = ArenaRow(); var team = TeamRow();
        arena.Load(line); team.Load(line);
        Assert.Equal(name, arena.Name); Assert.Equal(name, team.Name);
        Assert.Equal(0u, arena.UID); Assert.Equal(0u, team.UID);
        Assert.Equal((ushort)0, arena.Level); Assert.Equal((ushort)0, team.Level);
        Assert.Equal(0u, arena.Class); Assert.Equal(0u, team.Class);
        Assert.Equal(0u, arena.Info.TodayWin); Assert.Equal(0u, team.Info.TodayWin);
        Assert.Equal(7u, arena.Info.TotalWin); Assert.Equal(7u, team.Info.TotalWin);
        Assert.Equal(0u, arena.LastSeasonRank); Assert.Equal(0u, team.LastSeasonRank);
    });

    [Theory]
    [InlineData(0u, 0u)] [InlineData(255u, 255u)] [InlineData(256u, 0u)]
    [InlineData(16004u, 0u)] [InlineData(uint.MaxValue, 0u)]
    public Task ArenaReadsUInt32ClassButTeamArenaReallyUsesTheByteParser(uint job, uint teamExpected) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        string line = NativeRow.Replace("/16004/", "/" + job + "/", StringComparison.Ordinal);
        var arena = new LegacyNativeArenaUser(); var team = new LegacyNativeTeamArenaUser();
        arena.Load(line); team.Load(line);
        Assert.Equal(job, arena.Class); Assert.Equal(teamExpected, team.Class);
        Assert.Equal(1003u, arena.Mesh); Assert.Equal(1003u, team.Mesh);
    });

    [Fact]
    public Task NullInfoLoadFaultLeavesFiveIdentityFieldsCommittedButSeasonTailUnchanged() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var arena = new LegacyNativeArenaUser { Info = null!, LastSeasonArenaPoints = 999 };
        var team = new LegacyNativeTeamArenaUser { Info = null!, LastSeasonArenaPoints = 999 };
        Assert.Throws<NullReferenceException>(() => arena.Load(NativeRow));
        Assert.Throws<NullReferenceException>(() => team.Load(NativeRow));
        Assert.Equal(1u, arena.UID); Assert.Equal(1u, team.UID);
        Assert.Equal("Alpha", arena.Name); Assert.Equal("Alpha", team.Name);
        Assert.Equal((ushort)130, arena.Level); Assert.Equal((ushort)130, team.Level);
        Assert.Equal(16004u, arena.Class); Assert.Equal(0u, team.Class);
        Assert.Equal(1003u, arena.Mesh); Assert.Equal(1003u, team.Mesh);
        Assert.Equal(999u, arena.LastSeasonArenaPoints); Assert.Equal(999u, team.LastSeasonArenaPoints);
    });

    [Fact]
    public Task RecordWritersDoNotEscapeSlashNamesAndKeepNullNameFaults() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var arena = ArenaRow(); var team = TeamRow();
        arena.Name = "A/B"; team.Name = "A/B";
        Assert.StartsWith("1/A/B/130/16004/", arena.ToString());
        Assert.StartsWith("1/A/B/130/16004/", team.ToString());
        var reloaded = new LegacyNativeArenaUser(); reloaded.Load(arena.ToString());
        Assert.Equal("A", reloaded.Name); Assert.Equal((ushort)0, reloaded.Level);
        Assert.Equal(130u, reloaded.Class); Assert.Equal(16004u, reloaded.Mesh);
        arena.Name = null!; team.Name = null!;
        Assert.Throws<NullReferenceException>(() => arena.ToString());
        Assert.Throws<NullReferenceException>(() => team.ToString());
    });
}

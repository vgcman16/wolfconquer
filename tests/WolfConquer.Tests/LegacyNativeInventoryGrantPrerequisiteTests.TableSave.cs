using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryGrantPrerequisiteTests
{
    [Fact]
    public Task NativeImplicitTableBirthDoesNotSaveClearPoolsOrAllocateAnotherRecord() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FileScope();
        var arena = ArenaRow(); var team = TeamRow();
        LegacyNativeArenaRankings.ArenaPoll.TryAdd(7, arena); LegacyNativeTeamArenaRankings.ArenaPoll.TryAdd(8, team);
        _ = new LegacyNativeArenaTable(); _ = new LegacyNativeTeamArenaTable();
        Assert.Same(arena, LegacyNativeArenaRankings.ArenaPoll[7]); Assert.Same(team, LegacyNativeTeamArenaRankings.ArenaPoll[8]);
        Assert.Empty(Directory.GetFiles(files.Root)); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task BothEmptySavesCreateOnlyTheirLiteralCountHeaderFiles() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FileScope();
        new LegacyNativeArenaTable().Save(); new LegacyNativeTeamArenaTable().Save();
        Assert.Equal("Count=0" + Environment.NewLine, File.ReadAllText(Path.Combine(files.Root, "Arena.ini")));
        Assert.Equal("Count=0" + Environment.NewLine, File.ReadAllText(Path.Combine(files.Root, "TeamArena.ini")));
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task BothSingleRowSavesUseTheGenuineDbWriterAndExactNativeRecordOrder() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FileScope();
        LegacyNativeArenaRankings.ArenaPoll.TryAdd(1, ArenaRow()); LegacyNativeTeamArenaRankings.ArenaPoll.TryAdd(1, TeamRow());
        new LegacyNativeArenaTable().Save(); new LegacyNativeTeamArenaTable().Save();
        string expected = "Count=1" + Environment.NewLine + NativeRow + Environment.NewLine;
        Assert.Equal(expected, File.ReadAllText(Path.Combine(files.Root, "Arena.ini")));
        Assert.Equal(expected, File.ReadAllText(Path.Combine(files.Root, "TeamArena.ini")));
    });

    [Fact]
    public Task SaveWritesValuesIncludingDuplicateAliasesWithoutNormalizingKeysToUids() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FileScope();
        var arena = ArenaRow(); var team = TeamRow();
        LegacyNativeArenaRankings.ArenaPoll.TryAdd(777, arena); LegacyNativeArenaRankings.ArenaPoll.TryAdd(888, arena);
        LegacyNativeTeamArenaRankings.ArenaPoll.TryAdd(999, team); LegacyNativeTeamArenaRankings.ArenaPoll.TryAdd(1000, team);
        new LegacyNativeArenaTable().Save(); new LegacyNativeTeamArenaTable().Save();
        string expected = "Count=2" + Environment.NewLine + NativeRow + Environment.NewLine + NativeRow + Environment.NewLine;
        Assert.Equal(expected, File.ReadAllText(Path.Combine(files.Root, "Arena.ini")));
        Assert.Equal(expected, File.ReadAllText(Path.Combine(files.Root, "TeamArena.ini")));
        Assert.Equal(1u, arena.UID); Assert.Equal(1u, team.UID);
    });

    [Fact]
    public Task RepeatedSaveUsesNativeOpenModeToReplaceRatherThanAppendOldContent() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FileScope();
        var arenaTable = new LegacyNativeArenaTable(); var teamTable = new LegacyNativeTeamArenaTable();
        LegacyNativeArenaRankings.ArenaPoll.TryAdd(1, ArenaRow()); LegacyNativeTeamArenaRankings.ArenaPoll.TryAdd(1, TeamRow());
        arenaTable.Save(); teamTable.Save();
        LegacyNativeArenaRankings.ArenaPoll.Clear(); LegacyNativeTeamArenaRankings.ArenaPoll.Clear();
        arenaTable.Save(); teamTable.Save();
        Assert.Equal("Count=0" + Environment.NewLine, File.ReadAllText(Path.Combine(files.Root, "Arena.ini")));
        Assert.Equal("Count=0" + Environment.NewLine, File.ReadAllText(Path.Combine(files.Root, "TeamArena.ini")));
    });

    [Fact]
    public Task SerializationFaultOccursBeforeFileExecutionAndKeepsExistingFileContents() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FileScope();
        string arenaPath = Path.Combine(files.Root, "Arena.ini"); string teamPath = Path.Combine(files.Root, "TeamArena.ini");
        File.WriteAllText(arenaPath, "original-arena-fixture"); File.WriteAllText(teamPath, "original-team-fixture");
        LegacyNativeArenaRankings.ArenaPoll.TryAdd(1, new LegacyNativeArenaUser { Name = null! });
        LegacyNativeTeamArenaRankings.ArenaPoll.TryAdd(1, new LegacyNativeTeamArenaUser { Info = null! });
        Assert.Throws<NullReferenceException>(() => new LegacyNativeArenaTable().Save());
        Assert.Throws<NullReferenceException>(() => new LegacyNativeTeamArenaTable().Save());
        Assert.Equal("original-arena-fixture", File.ReadAllText(arenaPath));
        Assert.Equal("original-team-fixture", File.ReadAllText(teamPath));
    });

    [Fact]
    public Task SaveDoesNotCreateMissingConfiguredParentDirectoriesOrMaskTheNativeIoFault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FileScope();
        string missing = Path.Combine(files.Root, "missing");
        LegacyNativeServerConfig.DbLocation = missing + Path.DirectorySeparatorChar;
        Assert.Throws<DirectoryNotFoundException>(() => new LegacyNativeArenaTable().Save());
        Assert.Throws<DirectoryNotFoundException>(() => new LegacyNativeTeamArenaTable().Save());
        Assert.False(Directory.Exists(missing));
    });
}

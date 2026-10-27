using System.Text;
using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    [Fact]
    public void SaveFileSafetyCreatesTheExactCurrentUtf8ImageWithoutOrdinaryStageResidue() =>
        RunOwnedPrestigeSaveFile(files =>
        {
            SeedPrestigeSaveFileGraph();
            var graph = new PrestigeSaveFileGraph();
            Assert.Empty(PrestigeSaveFileEntries(files));
            Ranking.Save();
            var expected = Text(FullRow);
            Assert.Equal(new UTF8Encoding(false, true).GetBytes(expected), File.ReadAllBytes(files.PathFor()));
            Assert.Equal(new[] { "PrestigeRanking.txt" }, PrestigeSaveFileEntries(files));
            graph.RequireUnchanged();
            files.Expect(expected);
        });

    [Fact]
    public void SaveFileSafetyReplacesExistingBytesWithExactUtf8AndCleansOrdinaryStageAndBackup() =>
        RunOwnedPrestigeSaveFile(files =>
        {
            files.Seed("previous owned bytes\r\n");
            SeedPrestigeSaveFileGraph(name: "H\u00E9ro\U0001F409", guild: "\u96EA");
            var graph = new PrestigeSaveFileGraph();
            var entries = PrestigeSaveFileEntries(files);
            Ranking.Save();
            var expected = Text("41/H\u00E9ro\U0001F409/4/505/206/707/30004/808/909/1010/1111/1212/1313/1414/1515/1616/1717/1818/1919/2020/\u96EA/2/201/202/");
            Assert.Equal(new UTF8Encoding(false, true).GetBytes(expected), File.ReadAllBytes(files.PathFor()));
            Assert.Equal(entries, PrestigeSaveFileEntries(files));
            graph.RequireUnchanged();
            files.Expect(expected);
        });

    [Fact]
    public void SaveFileSafetyMissingParentRefusesWithoutCreatingDirectoriesOrChangingOldOwnersAndBytes() =>
        RunOwnedPrestigeSaveFile(files =>
        {
            files.Seed(Text(FullRow));
            SeedPrestigeSaveFileGraph(name: "Current");
            var graph = new PrestigeSaveFileGraph();
            var entries = PrestigeSaveFileEntries(files);
            var bytes = File.ReadAllBytes(files.PathFor());
            var missing = Path.Combine(files.Root, "absent-save-parent");
            Assert.False(Directory.Exists(missing));
            LegacyNativeServerConfig.DbLocation = missing + Path.DirectorySeparatorChar;
            Assert.Throws<DirectoryNotFoundException>(() => Ranking.Save());
            Assert.False(Directory.Exists(missing));
            Assert.Equal(bytes, File.ReadAllBytes(files.PathFor()));
            Assert.Equal(entries, PrestigeSaveFileEntries(files));
            graph.RequireUnchanged();
        });

    [WindowsPrestigeSaveFileFact]
    public void SaveFileSafetyRealTargetSharingRefusesWithoutReplacingOldBytesOrLeavingOrdinaryStages() =>
        RunOwnedPrestigeSaveFile(files =>
        {
            files.Seed(Text(FullRow));
            SeedPrestigeSaveFileGraph(name: "Current");
            var graph = new PrestigeSaveFileGraph();
            var entries = PrestigeSaveFileEntries(files);
            var bytes = File.ReadAllBytes(files.PathFor());
            Exception? failure;
            using (var held = new FileStream(files.PathFor(), FileMode.Open, FileAccess.Read, FileShare.Read))
                failure = Record.Exception(Ranking.Save);
            RequireActualSaveFileSystemRefusal(failure);
            Assert.Equal(bytes, File.ReadAllBytes(files.PathFor()));
            Assert.Equal(entries, PrestigeSaveFileEntries(files));
            graph.RequireUnchanged();
        });

    [Fact]
    public void SaveFileSafetyDirectoryTargetRefusesWithoutChangingGraphSiblingBytesOrLeavingOrdinaryStages() =>
        RunOwnedPrestigeSaveFile(files =>
        {
            files.Seed("owned prefix control", prefix: true);
            SeedPrestigeSaveFileGraph();
            var graph = new PrestigeSaveFileGraph();
            var prefixBytes = File.ReadAllBytes(files.PathFor(prefix: true));
            WithOwnedPrestigeSaveTargetDirectory(files, () =>
            {
                var entries = PrestigeSaveFileEntries(files);
                RequireActualSaveFileSystemRefusal(Record.Exception(Ranking.Save));
                Assert.True(Directory.Exists(files.PathFor()));
                Assert.Empty(Directory.GetFileSystemEntries(files.PathFor()));
                Assert.Equal(entries, PrestigeSaveFileEntries(files));
                Assert.Equal(prefixBytes, File.ReadAllBytes(files.PathFor(prefix: true)));
                graph.RequireUnchanged();
            });
        });
}

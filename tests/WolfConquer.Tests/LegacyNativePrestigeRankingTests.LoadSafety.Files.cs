using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    [Fact]
    public void LoadSafetyMissingPrestigeFilePreservesTheExactPublishedGraphAndCache() =>
        RunOwnedPrestigeLoad(files =>
        {
            SeedPriorPrestigeGraph();
            var prior = new PublishedPrestigeGraph();
            Assert.False(File.Exists(files.PathFor()));
            Assert.Throws<FileNotFoundException>(() => Ranking.Load());
            prior.RequireUnchanged();
            Assert.False(File.Exists(files.PathFor()));
        });

    [Fact]
    public void LoadSafetyExclusivelyHeldPrestigeFileRefusesBeforePublishingAndPreservesItsBytes() =>
        RunOwnedPrestigeLoad(files =>
        {
            SeedPriorPrestigeGraph();
            var prior = new PublishedPrestigeGraph();
            files.Seed(Text(FullRow));
            var bytes = File.ReadAllBytes(files.PathFor());
            using (var held = new FileStream(files.PathFor(), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.ThrowsAny<IOException>(() => Ranking.Load());
                prior.RequireUnchanged();
            }
            Assert.Equal(bytes, File.ReadAllBytes(files.PathFor()));
        });

    public static IEnumerable<object[]> InvalidPrestigeCountedFiles()
    {
        var newline = Environment.NewLine;
        yield return ["empty-file", ""];
        yield return ["wrong-key", "Other=1" + newline + FullRow + newline];
        yield return ["wrong-key-case", "count=1" + newline + FullRow + newline];
        yield return ["missing-equals", "Count1" + newline + FullRow + newline];
        yield return ["extra-equals", "Count=1=ignored" + newline + FullRow + newline];
        yield return ["missing-count", "Count=" + newline];
        yield return ["negative-count", "Count=-1" + newline];
        yield return ["signed-count", "Count=+1" + newline + FullRow + newline];
        yield return ["spaced-count", "Count= 1" + newline + FullRow + newline];
        yield return ["nonnumeric-count", "Count=NaN" + newline];
        yield return ["nul-terminated-count", "Count=0\0" + newline];
        yield return ["overflow-count", "Count=2147483648" + newline];
        yield return ["missing-declared-row", "Count=1" + newline];
        yield return ["short-after-valid-prefix", "Count=2" + newline + FullRow + newline];
        yield return ["extra-after-valid-row", Text(FullRow) + Row(19) + newline];
        yield return ["extra-after-zero", Text() + FullRow + newline];
        yield return ["extra-blank-after-zero", Text() + newline];
        yield return ["extra-blank-after-valid-row", Text(FullRow) + newline];
        yield return ["blank-declared-row", "Count=1" + newline + newline];
    }

    [Theory]
    [MemberData(nameof(InvalidPrestigeCountedFiles))]
    public void LoadSafetyInvalidHeaderOrPhysicalCardinalityCannotPublishAPartialPrestigeGraph(string _, string text) =>
        RunOwnedPrestigeLoad(files => RequireRejectedPrestigeText(files, text));

    [Fact]
    public void LoadSafetyRefusedPrestigeLoadPreservesAnAlreadyDirtySafeDictionaryCacheFlag() =>
        RunOwnedPrestigeLoad(files => RequireRejectedPrestigeText(files,
            Text(FullRow, ChangePrestigeLoadField(CompletePrestigeLoadRow(19, 1), 5, "bad")), dirtyCache: true));
}

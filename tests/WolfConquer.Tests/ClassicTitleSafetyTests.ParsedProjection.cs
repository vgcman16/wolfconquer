using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ClassicTitleSafetyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task ParserTrimmedTitleValuesCanCommitWithoutRewritingRawOwnership(int kind) => InFixture(async fixture =>
    {
        Assert.Equal("2, 5/7", fixture.Original.Character.Attributes["Titles"]);
        Assert.Equal("2", fixture.Original.Character.Attributes["MyTitle"]);
        Assert.True(CharacterSnapshotFileBaseline.TryRead(fixture.SnapshotPath, "owned", out var before, out var reason), reason);
        Assert.Equal(" \t2, 5/7 \t", before!.Fields["Titles"]);
        Assert.Equal(" \t2 \t", before.Fields["MyTitle"]);
        fixture.VerifyCommittedBeforeSend = true;

        await fixture.Dispatch();

        Assert.Equal(new byte[] { 0x0A, 0, 0x68, 8, 4, 3, 2, 1, 7, 3 }, Assert.Single(fixture.Sent));
        Assert.Equal("7", fixture.Current().Character.Attributes["MyTitle"]);
        Assert.Equal("2, 5/7", fixture.Current().Character.Attributes["Titles"]);
        Assert.True(CharacterSnapshotFileBaseline.TryRead(fixture.SnapshotPath, "owned", out var after, out reason), reason);
        Assert.Equal(" \t2, 5/7 \t", after!.Fields["Titles"]);
        Assert.Equal("7", after.Fields["MyTitle"]);
        // Independent expected complete line image: only the selected RHS changes.
        Assert.Equal(before.Lines.Select(line => line.StartsWith("MyTitle=", StringComparison.Ordinal)
            ? "MyTitle=7" : line), after.Lines);
        Assert.Equal(before.Encoding.CodePage, after.Encoding.CodePage);
        Assert.Equal(before.Encoding.GetPreamble(), after.Encoding.GetPreamble());
        Assert.Equal("12345", after.Fields["Money"]);
        Assert.False(fixture.Recovery.IsBlocked);
    }, kind, titles: " \t2, 5/7 \t", selected: " \t2 \t");

    [Theory]
    [InlineData("Titles=2,5,7", "Titles= \t2,5,7 \t")]
    [InlineData("MyTitle=2", "MyTitle= \t2 \t")]
    public Task OnlyDiscardedOuterLayoutHasNoHistoricalRawTokenToCompare(string from, string to) => InFixture(async fixture =>
    {
        fixture.Rewrite(text => text.Replace(from, to, StringComparison.Ordinal));
        Assert.True(CharacterSnapshotFileBaseline.TryRead(fixture.SnapshotPath, "owned", out var before, out var reason), reason);
        await fixture.Dispatch();
        Assert.Single(fixture.Sent);
        Assert.Equal("7", fixture.Current().Character.Attributes["MyTitle"]);
        Assert.True(CharacterSnapshotFileBaseline.TryRead(fixture.SnapshotPath, "owned", out var after, out reason), reason);
        Assert.Equal(before!.Fields["Titles"], after!.Fields["Titles"]);
        Assert.Equal("7", after.Fields["MyTitle"]);
    });

    [Theory]
    [InlineData("Titles=2,5,7", "Titles= 7,5,2 ")]
    [InlineData("Titles=2,5,7", "Titles= 2,5,7,8 ")]
    [InlineData("MyTitle=2", "MyTitle= 02 ")]
    public Task TrimProjectionNeverAcceptsNumericEquivalenceReorderingOrAuthorizationChanges(string from, string to) => InFixture(async fixture =>
    {
        fixture.Rewrite(text => text.Replace(from, to, StringComparison.Ordinal));
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        await fixture.Dispatch();
        fixture.Reject(fixture.Original, before);
        Assert.Contains("classicTitleStateStale", fixture.Log.ToString());
        Assert.False(fixture.Recovery.IsBlocked);
    });

    [Fact]
    public Task PostWriteEvidenceStillRequiresExactCapturedRawOwnershipNotTrimProjection() => InFixture(fixture =>
    {
        Assert.True(CharacterSnapshotFileBaseline.TryRead(fixture.SnapshotPath, "owned", out var before, out var reason), reason);
        var candidate = new Dictionary<string, string> { ["MyTitle"] = "7" };
        Assert.True(CharacterSnapshotSectionLineTransformer.TryApply((string[])before!.Lines.Clone(), "Character",
            candidate, createIfMissing: false, out var projected));
        Assert.True(LegacyClassicTitlePersistence.TryPersist(fixture.Original,
            Fixture.WithSelection(fixture.Original, "7"), out reason), reason);
        fixture.Rewrite(text => text.Replace("Titles= \t2,5,7 \t", "Titles=2,5,7", StringComparison.Ordinal));
        var args = new object?[] { fixture.SnapshotPath, Fixture.Uid, candidate, before, projected, null };
        var method = typeof(LegacyClassicTitlePersistence).GetMethod("TryValidateWrittenState", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.False((bool)method.Invoke(null, args)!);
        Assert.Equal("classicTitleRecoveryRequired:postWriteStateUnproven", args[5]);
        return Task.CompletedTask;
    }, titles: " \t2,5,7 \t");
}

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
    [InlineData(3)]
    public Task PersistedCommitCannotSmuggleUnrelatedScalarsAttributesAuthorizationOrNoncanonicalSelection(int kind) => InFixture(fixture =>
    {
        var updated = Fixture.WithSelection(fixture.Original, kind == 3 ? "007" : "7");
        if (kind == 0) updated = updated with { Character = updated.Character with { Money = 999999 } };
        if (kind is 1 or 2) updated = updated with { Character = updated.Character with
        { Attributes = new Dictionary<string, string>(updated.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { [kind == 1 ? "Money" : "Titles"] = kind == 1 ? "999999" : "2,5,7,8" } } };
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        Assert.False(LegacyClassicTitlePersistence.TryPersist(fixture.Original, updated, out var reason));
        Assert.NotEmpty(reason);
        fixture.Reject(fixture.Original, before);
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ActualFailureEvidenceClassifierRequiresExactFullPreimage(int kind) => InFixture(fixture =>
    {
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        if (kind == 1) fixture.Rewrite(text => text + "\n; changed unrelated bytes\n");
        if (kind == 2) File.Delete(fixture.SnapshotPath);
        if (kind == 3)
        {
            using var file = new FileStream(fixture.SnapshotPath, FileMode.Open, FileAccess.Write, FileShare.None);
            file.SetLength(CharacterSnapshotFileBaseline.MaximumBytes + 1L);
        }
        var args = new object?[] { fixture.SnapshotPath, before, "owned-observed-failure", null };
        var method = typeof(LegacyClassicTitlePersistence).GetMethod("ClassifyFailedWrite", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.False((bool)method.Invoke(null, args)!);
        Assert.StartsWith(kind == 0 ? "classicTitleWriteFailed:" : LegacyClassicTitlePersistence.RecoveryRequired,
            Assert.IsType<string>(args[3]));
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData("MyTitle=7", "MyTitle=5")]
    [InlineData("Titles=2,5,7", "Titles=2,5")]
    [InlineData("UID=16909060", "UID=16909061")]
    [InlineData("Money=12345", "Money=999999")]
    [InlineData("Sentinel=café€", "Sentinel=changed")]
    public Task ActualPostconditionReaderRejectsTitleUidAndUnrelatedDrift(string from, string to) => InFixture(fixture =>
    {
        Assert.True(CharacterSnapshotFileBaseline.TryRead(fixture.SnapshotPath, "owned", out var before, out var reason), reason);
        var updated = Fixture.WithSelection(fixture.Original, "7");
        Assert.True(LegacyClassicTitlePersistence.TryPersist(fixture.Original, updated, out reason), reason);
        fixture.Rewrite(text => text.Replace(from, to, StringComparison.Ordinal));
        var candidate = new Dictionary<string, string> { ["MyTitle"] = "7" };
        Assert.True(CharacterSnapshotSectionLineTransformer.TryApply((string[])before!.Lines.Clone(), "Character",
            candidate, createIfMissing: false, out var projected));
        var args = new object?[] { fixture.SnapshotPath, Fixture.Uid, candidate, before, projected, null };
        var method = typeof(LegacyClassicTitlePersistence).GetMethod("TryValidateWrittenState", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.False((bool)method.Invoke(null, args)!);
        Assert.Equal("classicTitleRecoveryRequired:postWriteStateUnproven", args[5]);
        return Task.CompletedTask;
    });
}

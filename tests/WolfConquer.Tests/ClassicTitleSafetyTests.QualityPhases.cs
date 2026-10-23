using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ClassicTitleSafetyTests
{
    [Theory]
    [InlineData("account", "classicTitleBindingInvalid")]
    [InlineData("root", "classicTitleBindingInvalid")]
    [InlineData("unrelated", "classicTitleUnrelatedStateChanged")]
    [InlineData("canonical", "classicTitleStateInvalid")]
    [InlineData("unowned", "classicTitleStateInvalid")]
    [InlineData("missing-file", "classicTitleStateInvalid")]
    public Task PersistencePhasesRetainBindingUnrelatedStateAndCandidateReasonPrecedence(string kind, string expectedReason)
        => InFixture(fixture =>
    {
        var original = fixture.Original;
        if (kind == "account") original = original with { AccountId = 0 };
        if (kind == "root") original = original with { UsersRoot = "." };
        var updated = Fixture.WithSelection(original, kind == "unowned" ? "8" : "007");
        if (kind is "account" or "root" or "unrelated")
            updated = updated with { Character = updated.Character with { Money = 999999 } };
        if (kind == "missing-file") File.Delete(fixture.SnapshotPath);
        var before = File.Exists(fixture.SnapshotPath) ? File.ReadAllBytes(fixture.SnapshotPath) : null;
        Assert.False(LegacyClassicTitlePersistence.TryPersist(original, updated, out var reason));
        Assert.Equal(expectedReason, reason);
        fixture.Reject(fixture.Original, before);
        return Task.CompletedTask;
    });

    [Fact]
    public Task ChangeOwnershipRefusalPrecedesRecoveryWhileListRemainsReadOnly() => InFixture(async fixture =>
    {
        fixture.Recovery.Block("owned recovery fence");
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        await fixture.Dispatch(title: 8);
        fixture.Reject(fixture.Original, before);
        Assert.Contains("reason=titleNotOwned", fixture.Log.ToString());
        await fixture.Dispatch(LegacyTitleAction.List);
        Assert.Equal(3, fixture.Sent.Count);
        Assert.Same(fixture.Original, fixture.Current());
        Assert.Equal(before, File.ReadAllBytes(fixture.SnapshotPath));
    });
}

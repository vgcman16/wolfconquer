using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDragonSkinPersistenceTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void BoundItemCannotBecomeAnUnboundRetrievalEntitlementEvenThroughThePersistencePrimitive(byte bound) => Run(fixture =>
    {
        var items = FullItems() with { InventoryItems = [Cost().WithBound(bound), Item(11)] };
        var source = fixture.Seed(items: items);
        var updated = Fixture.Unlock(source);
        var before = fixture.Capture();

        Assert.False(LegacyDragonSkinPersistence.TryPersist(source, updated, fixture.Items, out var reason));

        Assert.Equal("dragonSkinTransitionInvalid", reason);
        fixture.AssertImages(before);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    });

    [Theory]
    [InlineData("0/1/2/", "0/1/1/")]
    [InlineData("0/2/2/3/", "0/2/3/2/")]
    [InlineData("0/1/2/", "0/2/2/2/")]
    [InlineData("0/1/2/", "7/1/2/")]
    public void StaleReorderedDuplicateOrUnownedStoredEntitlementCannotAuthorizeDebit(string liveState, string diskState) => Run(fixture =>
    {
        var source = fixture.Seed(liveState);
        var updated = Fixture.Unlock(source);
        var text = CharacterSnapshotFileEncoding.ReadAllText(fixture.CharacterPath, out var encoding);
        File.WriteAllBytes(fixture.CharacterPath, Encode(text.Replace("AnimaSkin=" + liveState + "\n", "AnimaSkin=" + diskState + "\n",
            StringComparison.Ordinal), encoding));
        var before = fixture.Capture();

        Assert.False(LegacyDragonSkinPersistence.TryPersist(source, updated, fixture.Items, out var reason));

        Assert.Equal("dragonSkinCharacterBaselineStale", reason);
        fixture.AssertImages(before);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    });

    [Fact]
    public void FullItemPreimageIncludesUnrelatedStoresAndTheirCompleteMutableFields() => Run(fixture =>
    {
        var source = fixture.Seed();
        var updated = Fixture.Unlock(source);
        var disk = File.ReadAllBytes(fixture.ItemPath);
        disk[^1] ^= 1;
        File.WriteAllBytes(fixture.ItemPath, disk);
        var before = fixture.Capture();

        Assert.False(LegacyDragonSkinPersistence.TryPersist(source, updated, fixture.Items, out var reason));

        Assert.Equal("dragonSkinItemBaselineStale", reason);
        fixture.AssertImages(before);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    });

    [Fact]
    public void UnrelatedAttributeCannotRideAlongWithAValidEntitlementTransition() => Run(fixture =>
    {
        var source = fixture.Seed();
        var updated = Fixture.Unlock(source);
        var fields = new Dictionary<string, string>(updated.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["Sentinel"] = "changed" };
        updated = updated with { Character = updated.Character with { Attributes = fields } };
        var before = fixture.Capture();

        Assert.False(LegacyDragonSkinPersistence.TryPersist(source, updated, fixture.Items, out var reason));

        Assert.Equal("dragonSkinUnrelatedAttributeChange", reason);
        fixture.AssertImages(before);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    });

    [Fact]
    public void ARealReplaceSharingFailureRollsBackBothFullImagesAndRetryCommitsOnce() => Run(fixture =>
    {
        if (!OperatingSystem.IsWindows()) return;
        var source = fixture.Seed();
        var updated = Fixture.Unlock(source);
        var before = fixture.Capture();
        using (new FileStream(fixture.ItemPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(LegacyDragonSkinPersistence.TryPersist(source, updated, fixture.Items, out var reason));
            Assert.StartsWith("dragonSkinWriteRejected:", reason);
            fixture.AssertImages(before);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
        }

        Assert.True(LegacyDragonSkinPersistence.TryPersist(source, updated, fixture.Items, out var retryReason), retryReason);
        fixture.AssertAfter(updated);
    });

    [Fact]
    public void MissingNonemptyPhysicalSidecarNeverBecomesAnAcceptedEmptyBaseline() => Run(fixture =>
    {
        var source = fixture.Seed();
        var updated = Fixture.Unlock(source);
        File.Delete(fixture.ItemPath);
        var before = fixture.Capture();

        Assert.False(LegacyDragonSkinPersistence.TryPersist(source, updated, fixture.Items, out var reason));

        Assert.Equal("dragonSkinItemBaselineStale", reason);
        fixture.AssertImages(before);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    });
}

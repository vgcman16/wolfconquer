using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDragonSkinPersistenceTests
{
    [Theory]
    [InlineData(false, "utf8", "\n")]
    [InlineData(false, "utf8-bom", "\r\n")]
    [InlineData(false, "windows1252", "\r")]
    [InlineData(true, "utf8", "\n")]
    [InlineData(true, "utf8-bom", "\r\n")]
    [InlineData(true, "windows1252", "\r")]
    public void ExactGrantAndDebitCommitBothImagesWithoutRewritingUnrelatedBytes(bool retrieve, string encodingName, string newline) => Run(fixture =>
    {
        var source = fixture.Seed(retrieve ? "1/2/1/2/" : "0/1/2/", encodingName, newline);
        var originalItems = PlayerItemSnapshotLoader.ToLegacyBytes(source.PlayerItems);
        var updated = retrieve ? Fixture.Retrieve(source) : Fixture.Unlock(source);
        var originalText = CharacterSnapshotFileEncoding.ReadAllText(fixture.CharacterPath, out var encoding);
        var expectedText = originalText.Replace("AnimaSkin=" + source.Character.Attributes["AnimaSkin"] + newline,
            "AnimaSkin=" + updated.Character.Attributes["AnimaSkin"] + newline, StringComparison.Ordinal);

        Assert.True(LegacyDragonSkinPersistence.TryPersist(source, updated, fixture.Items, out var reason), reason);

        Assert.Equal(Encode(expectedText, encoding), File.ReadAllBytes(fixture.CharacterPath));
        fixture.AssertAfter(updated);
        Assert.Equal(originalItems, PlayerItemSnapshotLoader.ToLegacyBytes(source.PlayerItems));
        Assert.Equal(retrieve ? "1/2/1/2/" : "0/1/2/", source.Character.Attributes["AnimaSkin"]);
    });

    [Fact]
    public void RealParserOuterTrimProjectionDoesNotDiscardTheRawFilePreimage() => Run(fixture =>
    {
        var source = fixture.Seed(" \t0/1/2/ \t", "utf8-bom", "\r\n");
        Assert.Equal("0/1/2/", source.Character.Attributes["AnimaSkin"]);
        var before = CharacterSnapshotFileEncoding.ReadAllText(fixture.CharacterPath, out var encoding);
        Assert.Contains("AnimaSkin= \t0/1/2/ \t\r\n", before, StringComparison.Ordinal);
        var updated = Fixture.Unlock(source);

        Assert.True(LegacyDragonSkinPersistence.TryPersist(source, updated, fixture.Items, out var reason), reason);

        var expected = before.Replace("AnimaSkin= \t0/1/2/ \t\r\n", "AnimaSkin=0/2/2/1/\r\n", StringComparison.Ordinal);
        Assert.Equal(Encode(expected, encoding), File.ReadAllBytes(fixture.CharacterPath));
        fixture.AssertAfter(updated);
    });

    [Fact]
    public void EquippedOnlyStoredDivergencePermitsOwnedEntitlementTransaction() => Run(fixture =>
    {
        var source = fixture.Seed("0/2/1/2/");
        source = Fixture.WithSkin(source, new LegacyDragonSkinState(1, new uint[] { 1, 2 }), source.PlayerItems);
        var updated = Fixture.Retrieve(source);

        Assert.True(LegacyDragonSkinPersistence.TryPersist(source, updated, fixture.Items, out var reason), reason);

        fixture.AssertAfter(updated);
        Assert.Equal("0/1/2/", updated.Character.Attributes["AnimaSkin"]);
    });

    [Fact]
    public void MissingEmptyItemFileIsCreatedByTheSameRealTwoTargetCommit() => Run(fixture =>
    {
        var source = fixture.Seed("1/1/1/", items: PlayerItemSnapshot.Empty);
        File.Delete(fixture.ItemPath);
        var updated = Fixture.Retrieve(source);

        Assert.True(LegacyDragonSkinPersistence.TryPersist(source, updated, fixture.Items, out var reason), reason);

        fixture.AssertAfter(updated);
        Assert.Single(updated.PlayerItems.InventoryItems);
    });
}

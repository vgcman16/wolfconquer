using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDragonSkinPersistenceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EachRealDurableInterruptionPrefixRecoversTheSameEntitlementAndItemExactlyOnce(int stopAfter) => Run(fixture =>
    {
        var source = fixture.Seed("1/2/1/2/");
        var updated = Fixture.Retrieve(source);
        var before = fixture.Capture();
        var text = CharacterSnapshotFileEncoding.Decode(before.Character, out var encoding);
        var afterCharacter = Encode(text.Replace("AnimaSkin=1/2/1/2/\n", "AnimaSkin=0/1/2/\n", StringComparison.Ordinal), encoding);
        var afterItems = PlayerItemSnapshotLoader.ToLegacyBytes(updated.PlayerItems);
        using var interruption = LegacyJournalInterruption.StopAfter(stopAfter);

        Assert.False(LegacyDragonSkinPersistence.TryPersist(source, updated, fixture.Items, interruption, out var reason));

        Assert.StartsWith(LegacyDragonSkinPersistence.RecoveryRequired + ":", reason);
        Assert.Equal(Enumerable.Range(0, stopAfter + 1), interruption.Visited);
        Assert.True(LegacyTradeJournal.HasPending(fixture.Users));
        Assert.Equal(stopAfter >= 1 ? afterCharacter : before.Character, File.ReadAllBytes(fixture.CharacterPath));
        Assert.Equal(stopAfter >= 2 ? afterItems : before.Items, File.ReadAllBytes(fixture.ItemPath));
        // Persistence failure has not mutated either source or proposed objects.
        Assert.Equal("1/2/1/2/", source.Character.Attributes["AnimaSkin"]);
        Assert.Equal("0/1/2/", updated.Character.Attributes["AnimaSkin"]);

        var restarted = new LegacyTradeRecoveryState(fixture.Users, fixture.Items);
        Assert.False(restarted.IsReadyFor(fixture.Users, fixture.Items));
        Assert.True(restarted.TryEnsureInitialized(out reason), reason);
        Assert.True(restarted.IsReadyFor(fixture.Users, fixture.Items));
        fixture.AssertAfter(updated);
        Assert.Equal(afterCharacter, File.ReadAllBytes(fixture.CharacterPath));
        var committed = fixture.Capture();
        Assert.True(LegacyTradeJournal.TryRecover(fixture.Users, fixture.Items, out var recovered, out reason), reason);
        Assert.Equal(0, recovered);
        fixture.AssertImages(committed);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ThrowingFailureOnlyInterruptNeverLooksLikeAnUnchangedPreparationRefusal(int throwAfter) => Run(fixture =>
    {
        var source = fixture.Seed("1/2/1/2/");
        var updated = Fixture.Retrieve(source);
        using var interruption = LegacyJournalInterruption.ThrowAfter(throwAfter, LegacyJournalFault.ThrowInvalidOperation);
        Assert.False(LegacyDragonSkinPersistence.TryPersist(source, updated, fixture.Items, interruption, out var reason));

        Assert.Equal("dragonSkinRecoveryRequired:writeFault:InvalidOperationException", reason);
        Assert.True(LegacyTradeJournal.HasPending(fixture.Users));
        var restarted = new LegacyTradeRecoveryState(fixture.Users, fixture.Items);
        Assert.True(restarted.TryEnsureInitialized(out reason), reason);
        fixture.AssertAfter(updated);
    });

    [Fact]
    public void PendingRecordRefusesAnotherMutationWithoutReplacingRecoveryEvidence() => Run(fixture =>
    {
        var source = fixture.Seed();
        var updated = Fixture.Unlock(source);
        Assert.False(LegacyDragonSkinPersistence.TryPersist(source, updated, fixture.Items, LegacyJournalInterruption.StopAfter(0), out _));
        var images = fixture.Capture();
        var pending = File.ReadAllBytes(fixture.PendingPath);

        Assert.False(LegacyDragonSkinPersistence.TryPersist(source, updated, fixture.Items, out var reason));

        Assert.Equal("dragonSkinRecoveryRequired:pending", reason);
        fixture.AssertImages(images);
        Assert.Equal(pending, File.ReadAllBytes(fixture.PendingPath));
    });

    [Fact]
    public void UnknownPhysicalPoststateMakesFreshRecoveryFailClosedWithoutDestroyingEvidence() => Run(fixture =>
    {
        var source = fixture.Seed("1/2/1/2/");
        var updated = Fixture.Retrieve(source);
        Assert.False(LegacyDragonSkinPersistence.TryPersist(source, updated, fixture.Items, LegacyJournalInterruption.StopAfter(1), out _));
        File.WriteAllBytes(fixture.ItemPath, [1, 2, 3, 4, 5]);
        var images = fixture.Capture();
        var pending = File.ReadAllBytes(fixture.PendingPath);
        var restarted = new LegacyTradeRecoveryState(fixture.Users, fixture.Items);

        Assert.False(restarted.TryEnsureInitialized(out var reason));

        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired + ":", reason);
        Assert.True(restarted.IsBlocked);
        Assert.False(restarted.IsReadyFor(fixture.Users, fixture.Items));
        fixture.AssertImages(images);
        Assert.Equal(pending, File.ReadAllBytes(fixture.PendingPath));
    });
}

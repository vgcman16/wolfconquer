using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCharacterSpellsJournalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AlreadyAfterSpellStageMustBeClearedBeforeTheRetainedJournalCanBeRemoved(bool holdStage) => RunJournalAsync(fixture =>
    {
        var changes = Changes(fixture);
        changes[1] = changes[1] with { After = changes[1].Before!.ToArray() };
        Assert.False(LegacyTradeJournal.TryCommitCharacterSpells(fixture.Users, fixture.SpellsRoot, changes,
            LegacyCharacterSpellsJournalInterruption.AfterCharacter, out _));
        RequireImages(fixture, changes[0].After, changes[1].After);
        var retained = RememberPending(fixture);
        var transactionId = Pending(fixture)["TransactionId"]!.GetValue<string>();
        // Reconstruct the real retained staging artifact for this published
        // transaction and exact image; no replacement or accepting callback.
        var stage = fixture.CreateOwnedSpellStage(transactionId, changes[1].After);
        var fresh = Recovery(fixture);

        if (holdStage)
        {
            using (var held = new FileStream(stage, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.False(fresh.TryEnsureInitialized(out var blockedReason));
                Assert.StartsWith("tradeJournalRecoveryRequired:", blockedReason);
                Assert.True(fresh.IsBlocked);
                Assert.False(fresh.IsReadyForCharacterSpells(fixture.Users, fixture.SpellsRoot));
                Assert.Equal(retained, File.ReadAllBytes(fixture.PendingPath));
                RequireImages(fixture, changes[0].After, changes[1].After);
                fixture.RequireFilesUnchanged();
                RequireNoExposure(fixture);
            }
            // The failed owner stays blocked. Only a fresh actual startup
            // owner retries the retained evidence after the share is released.
            Assert.False(fresh.TryEnsureInitialized(out _));
            fresh = Recovery(fixture);
        }

        Assert.True(fresh.TryEnsureInitialized(out var reason), reason);
        Assert.True(fresh.IsReadyForCharacterSpells(fixture.Users, fixture.SpellsRoot));
        RequireImages(fixture, changes[0].After, changes[1].After);
        fixture.ExpectStageAbsent(stage);
        fixture.ExpectNoPending();
        fixture.RequireFilesUnchanged();
        RequireNoExposure(fixture);
    });
}

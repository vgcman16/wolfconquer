namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Theory]
    [InlineData(21_972U)]
    [InlineData(22_577U)]
    public Task ActualD13VictoryStartupUsesExplicitQuestRecoveryAndCommitsBeforePublication(uint npc) =>
        RunVictoryStartupAsync(quests: true, async fixture =>
        {
            await fixture.EnterAsync();
            await fixture.HoldIndependentReaderAsync();
            await fixture.OpenAsync(npc);
            var before = fixture.Capture();
            fixture.ObserveCommit(before);
            await fixture.ReplyAsync();
            fixture.AssertCommitted(before);
            await fixture.AssertReplayAsync();
            await fixture.ProveSharedLeasesReturnedAsync();
        });

    [WindowsTradeJournalFailureFact]
    public Task ActualD13VictoryStartupQuestLeafSharingFailureRestoresAndRetriesWithoutRelog() =>
        RunVictoryStartupAsync(quests: true, async fixture =>
        {
            await fixture.EnterAsync();
            await fixture.HoldIndependentReaderAsync();
            await fixture.OpenAsync(21_972);
            var before = fixture.Capture();
            using (fixture.LeaseRequiredQuestLeaf())
                await fixture.ReplyAsync();
            fixture.AssertQuestSharingRefused(before);
            fixture.ObserveCommit(before);
            await fixture.ReplyAsync(); // Same real login, Store and recovery owner; no reset/reseed.
            fixture.AssertCommitted(before);
            await fixture.AssertReplayAsync();
            await fixture.ProveSharedLeasesReturnedAsync();
        });

    [Fact]
    public Task ActualD13VictoryStartupDoesNotInferAnOmittedQuestRootFromRealSiblingFiles() =>
        RunVictoryStartupAsync(quests: false, async fixture =>
        {
            await fixture.EnterAsync();
            await fixture.HoldIndependentReaderAsync();
            await fixture.OpenAsync(22_577);
            var before = fixture.Capture();
            await fixture.ReplyAsync(); // Client cannot invent the absent Accepted quest by selecting option1.
            fixture.AssertMissingQuestAuthorityRefused(before);
            await fixture.ProveSharedLeasesReturnedAsync();
        });
}

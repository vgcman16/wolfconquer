namespace WolfConquer.Tests;

// CURRENT registered APIs only: these compile before any v3 journal exists.
public sealed partial class LegacyVictoryChestTransactionTests
{
    [Theory]
    [InlineData(21_972U)]
    [InlineData(22_577U)]
    public Task RegisteredVictoryClaimRetainsExactSuccessfulSequenceAndReloadedReplay(uint npcId) => RunAsync(async fixture =>
    {
        fixture.Seed();
        await fixture.OpenAsync(npcId);
        var before = fixture.Capture();
        await fixture.ReplyAsync();
        fixture.AssertCommitted(before);
        await fixture.AssertSameSessionAndReloadedReplayAsync();
    });

    [Theory]
    [InlineData("itemsLocked")]
    [InlineData("questsLocked")]
    [InlineData("questsAuthorityMissing")]
    [InlineData("questsDurableFinished")]
    public Task RegisteredVictoryRefusesRequiredClaimSaveFaultThenRetriesWithoutReseeding(string fault) => RunAsync(async fixture =>
    {
        fixture.Seed();
        await fixture.OpenAsync(21_972);
        fixture.PrepareFault(fault);
        var before = fixture.Capture();
        await fixture.ReplyWithFaultAsync(fault);
        fixture.AssertRejected(before);
        fixture.RepairFault(fault);
        var repaired = fixture.Capture();
        await fixture.ReplyAsync();
        fixture.AssertCommitted(repaired);
        await fixture.AssertSameSessionAndReloadedReplayAsync();
    });
}

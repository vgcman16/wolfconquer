using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDeitylandChestCapacitySafetyTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public Task ChestJournalComponentRecoversOneRealOwnerAfterEachDurableBoundary(bool cp, int boundary) => RunAsync(fixture =>
    {
        fixture.Seed(40, firstStack: 1, secondStack: 2, firstBound: 1, accountId: 301);
        fixture.AssertChestJournalRecovery(cp, boundary);
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData(false, "account")]
    [InlineData(false, "items")]
    [InlineData(true, "account")]
    [InlineData(true, "items")]
    public Task ChestJournalComponentRejectsFileReplacementAfterPreparation(bool cp, string leaf) => RunAsync(fixture =>
    {
        fixture.Seed(40, firstStack: 1, secondStack: 2, firstBound: 0, accountId: 301);
        fixture.AssertPreparedChestFileReplacement(cp, leaf);
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData(false, "missing")]
    [InlineData(false, "uninitialized")]
    [InlineData(false, "blocked")]
    [InlineData(false, "foreign")]
    [InlineData(true, "missing")]
    [InlineData(true, "uninitialized")]
    [InlineData(true, "blocked")]
    [InlineData(true, "foreign")]
    public Task RealChestOwnerRequiresInitializedUnblockedSameDomainRecovery(bool cp, string fault) => RunAsync(async fixture =>
    {
        fixture.Seed(40, firstStack: 1, secondStack: 2, firstBound: 0);
        await fixture.AssertChestRecoveryAdmissionAsync(cp, fault);
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task RealChestOwnerRejectsReplacedOrRemovedSuppliedSession(bool cp, bool removed) => RunAsync(async fixture =>
    {
        fixture.Seed(40, firstStack: 1, secondStack: 2, firstBound: 0);
        await fixture.AssertChestRetiredAdmissionAsync(cp, removed);
    });
}

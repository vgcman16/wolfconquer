namespace WolfConquer.Tests;

public sealed partial class LegacyDeitylandChestCapacitySafetyTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    public Task ActualCpChestPersistsDistinctAccountAndCharacterWithoutTouchingUidNamedFile(
        ushort firstStack, byte firstBound) => RunAsync(async fixture =>
    {
        fixture.Seed(40, firstStack, secondStack: 1, firstBound, accountId: 301);
        fixture.SeedUnrelatedUidNamedCharacterFile();

        await fixture.UnlockSelectedAsync(rewardItemId: 0, conquerPointsReward: 200_000);

        fixture.AssertPositiveKeyCpReward(keyIndex: 0);
        fixture.AssertDistinctAccountCpSaved();
    });

    [Theory]
    [InlineData("missing", "snapshotMissing")]
    [InlineData("wrongUid", "uidMismatch")]
    [InlineData("uidMissing", "uidMissing")]
    [InlineData("usersRootMissing", "usersRootMissing")]
    public Task AccountAwarePersistenceComponentRejectsMissingOrForeignOwnerWithoutWrites(
        string fault, string expectedReason) => RunAsync(fixture =>
    {
        fixture.Seed(40, firstStack: 2, secondStack: 1, firstBound: 0, accountId: 301);
        fixture.SeedUnrelatedUidNamedCharacterFile();
        fixture.PrepareAccountPersistenceFault(fault);

        fixture.AssertAccountPersistenceOwnerRejectsWithoutEffects(fault, expectedReason);
        return Task.CompletedTask;
    });
}

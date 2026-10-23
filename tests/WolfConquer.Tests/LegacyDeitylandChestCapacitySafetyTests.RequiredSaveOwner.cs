namespace WolfConquer.Tests;

public sealed partial class LegacyDeitylandChestCapacitySafetyTests
{
    [Theory]
    [InlineData(false, "accountMissing")]
    [InlineData(true, "accountMissing")]
    [InlineData(false, "accountForeignUid")]
    [InlineData(true, "accountForeignUid")]
    [InlineData(false, "accountAmbiguousFields")]
    [InlineData(true, "accountAmbiguousFields")]
    [InlineData(false, "itemsMissing")]
    [InlineData(true, "itemsMissing")]
    [InlineData(false, "itemsForeignImage")]
    [InlineData(true, "itemsForeignImage")]
    [InlineData(false, "itemsTruncated")]
    [InlineData(true, "itemsTruncated")]
    public Task ActualChestRejectsRequiredSaveBaselineBeforeAnyReward(
        bool cpReward, string fault) => RunAsync(async fixture =>
    {
        fixture.Seed(40, firstStack: 1, secondStack: 2, firstBound: 1, accountId: 301);
        fixture.SeedUnrelatedUidNamedCharacterFile();
        fixture.AssertRequiredChestSaveOwnersReady();
        fixture.CorruptRequiredChestSaveBaseline(fault);
        var before = fixture.CaptureRequiredChestSavePreimage();

        await fixture.UnlockSelectedAsync(
            rewardItemId: cpReward ? 0U : 3_306_362U, conquerPointsReward: cpReward ? 200_000U : 0U);

        fixture.AssertRequiredChestSaveRejected(before);
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ActualChestRejectsLockedRequiredSaveLeafBeforeAnyReward(
        bool cpReward, bool accountFile) => RunAsync(async fixture =>
    {
        fixture.Seed(40, firstStack: 1, secondStack: 2, firstBound: 1, accountId: 301);
        fixture.SeedUnrelatedUidNamedCharacterFile();
        fixture.AssertRequiredChestSaveOwnersReady();
        var before = fixture.CaptureRequiredChestSavePreimage();

        await fixture.UnlockWithRequiredChestSaveLockAsync(cpReward, accountFile);

        fixture.AssertRequiredChestSaveRejected(before);
    });
}

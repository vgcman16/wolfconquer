using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Theory]
    [InlineData(false, 0, "io")]
    [InlineData(false, 1, "security")]
    [InlineData(false, 2, "invalid")]
    [InlineData(true, 0, "invalid")]
    [InlineData(true, 1, "io")]
    [InlineData(true, 3, "security")]
    public Task GenuinePublishedHitIntentExceptionRetainsItsActualPrefixAndRollsForward(bool lethal, int boundary, string kind) =>
        RunHitRecoveryAsync(lethal, async (f, target) =>
        {
            var capture = await CaptureHitRecoveryAsync(f, target, lethal, boundary, thrownFault: HitRecoveryFault(kind));
            Assert.True(LegacyTradeJournal.HasPending(f.Data.Users));
            await AssertHitRecoveryRestartsAsync(capture);
        });

    [Theory]
    [InlineData("io")] [InlineData("security")] [InlineData("invalid")]
    public Task GenuineChangedSpellDeathExceptionAfterTwoReplacementsRollsForward(string kind) =>
        RunHitRecoveryAsync(true, async (f, target) =>
        {
            var capture = await CaptureHitRecoveryDuringEquipAsync(f, target, 2, thrownFault: HitRecoveryFault(kind));
            string spellKey = Path.GetRelativePath(f.Data.Root, capture.Targets[1]);
            Assert.NotEqual(capture.Before[spellKey], capture.After[spellKey]);
            Assert.True(LegacyTradeJournal.HasPending(f.Data.Users));
            await AssertHitRecoveryRestartsAsync(capture);
        }, changedDeath: true);

    [WindowsTradeJournalFailureFact]
    public Task GenuineProficiencyReadLeaseAfterCharacterReplacementRetainsIntentForTwoRestarts() =>
        RunHitRecoveryAsync(false, async (f, target) =>
        {
            // The closed fault acquires a real OS read lease on the actual
            // actor Profs target after the target Character replacement.
            var capture = await CaptureHitRecoveryAsync(f, target, false, 1, leaseTarget: 1);
            Assert.True(LegacyTradeJournal.HasPending(f.Data.Users));
            await AssertHitRecoveryRestartsAsync(capture);
        });

    private static LegacyJournalFault HitRecoveryFault(string kind) => kind switch
    {
        "io" => LegacyJournalFault.ThrowIo,
        "security" => LegacyJournalFault.ThrowSecurity,
        "invalid" => LegacyJournalFault.ThrowInvalidOperation,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

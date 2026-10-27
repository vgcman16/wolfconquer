namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    [Fact]
    public void SaveObservationActualIdenticalReplacementWithConsumedStageIsCommittedWhenUnacknowledged() =>
        RequireRealUnacknowledgedPrestigeReplacement(changedImage: false);

    [Fact]
    public void SaveObservationActualChangedReplacementWithConsumedStageIsCommittedWhenUnacknowledged() =>
        RequireRealUnacknowledgedPrestigeReplacement(changedImage: true);

    private static void RequireRealUnacknowledgedPrestigeReplacement(bool changedImage) =>
        RunOwnedPrestigeSaveObservation(files =>
        {
            var candidate = changedImage
                ? Text(FullRow.Replace("/Hero/", "/Current/", StringComparison.Ordinal))
                : files.PriorText;
            files.PrepareSidecars(candidate);
            files.ReplaceTarget(candidate);
            // A real replacement returned in this test. Passing false tests the
            // observer's unacknowledged state; it does not prove Replace threw.
            files.RequireUnacknowledgedOutcome("Committed");
            Assert.False(File.Exists(files.Stage));
            Assert.Equal(files.PriorImage, File.ReadAllBytes(files.Backup));
        });

    [Fact]
    public void SaveObservationRetainedIdenticalStageAndUnchangedPriorIsRefusedWhenUnacknowledged() =>
        RunOwnedPrestigeSaveObservation(files =>
        {
            files.PrepareSidecars(files.PriorText);
            files.RequireUnacknowledgedOutcome("Refused");
            Assert.Equal(files.PriorImage, File.ReadAllBytes(files.Destination));
            Assert.Equal(files.PriorImage, File.ReadAllBytes(files.Stage));
            Assert.Empty(File.ReadAllBytes(files.Backup));
        });
}

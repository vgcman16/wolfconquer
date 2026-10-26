namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [WindowsTradeJournalFailureFact]
    public Task ActualD13MixedMailboxClaimCharacterLeafFailureRetainsEveryRewardThenSameOwnerRetries() =>
        RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync(mixed: true);
        var before = fixture.CaptureTransferFrame();
        fixture.HoldCharacterFile();

        await fixture.FrameAsync(Fixture.AtomicMailRequest(claimAll: true));

        fixture.ReleaseFiles();
        fixture.AssertFiles(before.Files);
        Assert.Equal(before.FrameCount, fixture.Sent.Count);
        var started = DateTime.Now;
        await fixture.FrameAsync(Fixture.AtomicMailRequest(claimAll: true));
        AssertAtomicMailCommitted(fixture, before, mixed: true);
        AssertAtomicMailFrames(fixture, before, true, true, started, DateTime.Now, before.FrameCount);
    });
}

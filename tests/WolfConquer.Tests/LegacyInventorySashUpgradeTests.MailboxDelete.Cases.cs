namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 40)]
    [InlineData(true, 40)]
    public Task ActualD13FrameMailboxDeleteCommitsAllFilesBeforeEchoAndIgnoresInventoryCapacity(bool batch, int inventoryCount) =>
        RunAsync(async fixture =>
        {
            await fixture.EnterAtomicMailFrameAsync(inventoryCount: inventoryCount, rewardMoney: 0, rewardCp: 0,
                hasAttachment: false, extraEmptyMail: batch);
            var before = fixture.CaptureTransferFrame();
            var expected = ExpectedMailboxDeleteFiles(fixture, before, batch);
            var observed = 0;
            fixture.SendObserver = _ => { fixture.AssertFiles(expected); observed++; };

            await fixture.FrameAsync(MailboxDeleteRequest(batch));

            fixture.SendObserver = null;
            Assert.Equal(batch ? 2 : 1, observed);
            AssertMailboxDeleteCommitted(fixture, before, batch);
            AssertMailboxDeleteFrames(fixture, before, batch);
            await AssertMailboxDeleteReplayAsync(fixture, before, batch);
        });

    [WindowsTradeJournalFailureFact]
    public Task ActualD13FrameMailboxSingleDeleteCharacterSharingRefusesThenSameOwnerRetries() =>
        RunMailboxDeleteSharingFailureAsync(batch: false);

    [WindowsTradeJournalFailureFact]
    public Task ActualD13FrameMailboxBatchDeleteMailSharingRefusesThenSameOwnerRetries() =>
        RunMailboxDeleteSharingFailureAsync(batch: true);

    private static Task RunMailboxDeleteSharingFailureAsync(bool batch) => RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync(rewardMoney: 0, rewardCp: 0,
            hasAttachment: false, extraEmptyMail: batch);
        var before = fixture.CaptureTransferFrame();
        if (batch) fixture.HoldAtomicMailFile(); else fixture.HoldCharacterFile();

        await fixture.FrameAsync(MailboxDeleteRequest(batch));

        fixture.ReleaseFiles(); // Only after the actual Frame operation is terminal.
        fixture.AssertFiles(before.Files);
        Assert.Equal(before.FrameCount, fixture.Sent.Count);
        await fixture.FrameAsync(MailboxDeleteRequest(batch));
        AssertMailboxDeleteCommitted(fixture, before, batch);
        AssertMailboxDeleteFrames(fixture, before, batch);
        await AssertMailboxDeleteReplayAsync(fixture, before, batch);
    });

    [Fact]
    public Task ActualD13FrameMailboxBatchDeleteFirstEchoFaultRetainsWholeCommitAndReplayHasNoEcho() =>
        RunAsync(async fixture =>
        {
            await fixture.EnterAtomicMailFrameAsync(rewardMoney: 0, rewardCp: 0,
                hasAttachment: false, extraEmptyMail: true);
            var before = fixture.CaptureTransferFrame();
            var expected = ExpectedMailboxDeleteFiles(fixture, before, batch: true);
            const string failure = "mailbox deletion first echo deliberately failed";
            var observed = 0;
            fixture.SendObserver = _ =>
            {
                fixture.AssertFiles(expected);
                observed++;
                throw new IOException(failure);
            };

            await fixture.FrameAsync(MailboxDeleteRequest(batch: true));

            fixture.SendObserver = null;
            Assert.Equal(1, observed);
            Assert.Equal(before.FrameCount + 1, fixture.Sent.Count); // Attempt recorded before the real delegate throws.
            Assert.Equal(MailboxDeleteRequest(batch: true), fixture.Sent[before.FrameCount]);
            Assert.Contains(failure, fixture.Log.ToString());
            AssertMailboxDeleteCommitted(fixture, before, batch: true);
            await AssertMailboxDeleteReplayAsync(fixture, before, batch: true);
        });
}

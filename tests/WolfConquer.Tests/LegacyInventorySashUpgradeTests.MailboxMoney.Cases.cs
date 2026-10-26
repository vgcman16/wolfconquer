using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(125UL, 0U, false, 0U, 1)]
    [InlineData(0UL, 7U, false, 0U, 1)]
    [InlineData(125UL, 7U, true, 1U, 1)]
    [InlineData(125UL, 7U, true, 1U, 40)]
    public Task ActualD13FrameMailboxMoneyCommitsBeforeEveryFrameAndPreservesUnclaimedRewards(
        ulong money, uint cp, bool hasAttachment, uint flag, int inventoryCount) => RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync(inventoryCount: inventoryCount, flag: flag,
            rewardMoney: money, rewardCp: cp, hasAttachment: hasAttachment);
        var before = fixture.CaptureTransferFrame();
        var expected = ExpectedMailboxMoneyFiles(fixture, before);
        var observed = 0;
        fixture.SendObserver = _ => { fixture.AssertFiles(expected); observed++; };

        await fixture.FrameAsync(MailboxMoneyRequest());

        fixture.SendObserver = null;
        Assert.Equal(ExpectedMailboxMoneyFrames(fixture, before).Length, observed);
        AssertMailboxMoneyCommitted(fixture, before);
        AssertMailboxMoneyFrames(fixture, before, before.FrameCount);
        await AssertMailboxMoneyReplayAsync(fixture, before);
    });

    [WindowsTradeJournalFailureFact]
    public Task ActualD13FrameMailboxMoneyCharacterSharingFailureCanRetryWithoutLostOrDuplicateCredit() =>
        RunMailboxMoneySharingFailureAsync(mailLeaf: false);

    [WindowsTradeJournalFailureFact]
    public Task ActualD13FrameMailboxMoneyMailSharingFailureCanRetryWithoutLostOrDuplicateCredit() =>
        RunMailboxMoneySharingFailureAsync(mailLeaf: true);

    private static Task RunMailboxMoneySharingFailureAsync(bool mailLeaf) => RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync(mixed: true, flag: 1);
        var before = fixture.CaptureTransferFrame();
        if (mailLeaf) fixture.HoldAtomicMailFile(); else fixture.HoldCharacterFile();

        await fixture.FrameAsync(MailboxMoneyRequest());

        fixture.ReleaseFiles(); // Release only after the actual Frame task is terminal.
        fixture.AssertFiles(before.Files);
        Assert.Equal(before.FrameCount, fixture.Sent.Count);
        await fixture.FrameAsync(MailboxMoneyRequest());
        AssertMailboxMoneyCommitted(fixture, before);
        AssertMailboxMoneyFrames(fixture, before, before.FrameCount);
        await AssertMailboxMoneyReplayAsync(fixture, before);
    });

    [Theory]
    [InlineData(ulong.MaxValue, 0U)]
    [InlineData(0UL, uint.MaxValue)]
    public Task ActualD13FrameMailboxMoneyOverflowRefusesRepeatedlyWithoutConsumingCachedReward(ulong money, uint cp) =>
        RunAsync(async fixture =>
        {
            await fixture.EnterAtomicMailFrameAsync(rewardMoney: money, rewardCp: cp, hasAttachment: false);
            var before = fixture.CaptureTransferFrame();

            await fixture.FrameAsync(MailboxMoneyRequest());

            fixture.AssertFiles(before.Files);
            Assert.Equal(before.FrameCount, fixture.Sent.Count);
            // A second real request catches a dirty first rejection that only cleared
            // the in-memory reward and would now publish an empty acknowledgement.
            await fixture.FrameAsync(MailboxMoneyRequest());
            fixture.AssertFiles(before.Files);
            Assert.Equal(before.FrameCount, fixture.Sent.Count);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
        });

    [Fact]
    public Task ActualD13FrameMailboxMoneyFirstSendFaultRetainsCommittedCreditAndReplayOnlyAcknowledges() =>
        RunAsync(async fixture =>
        {
            await fixture.EnterAtomicMailFrameAsync(mixed: true, flag: 1);
            var before = fixture.CaptureTransferFrame();
            var expected = ExpectedMailboxMoneyFiles(fixture, before);
            var observed = 0;
            const string failure = "mailbox money first send deliberately failed";
            fixture.SendObserver = _ =>
            {
                fixture.AssertFiles(expected);
                observed++;
                throw new IOException(failure);
            };

            await fixture.FrameAsync(MailboxMoneyRequest());

            fixture.SendObserver = null;
            Assert.Equal(1, observed);
            Assert.Equal(before.FrameCount + 1, fixture.Sent.Count); // The attempted frame is recorded before its delegate fails.
            Assert.Equal(ExpectedMailboxMoneyFrames(fixture, before)[0], fixture.Sent[before.FrameCount]);
            Assert.Contains(failure, fixture.Log.ToString());
            AssertMailboxMoneyCommitted(fixture, before);
            await AssertMailboxMoneyReplayAsync(fixture, before);
        });
}

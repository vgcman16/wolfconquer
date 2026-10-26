using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ActualD13FrameMailboxClaimCommitsCompleteFilesBeforeFirstOutputAndReplayCannotDuplicate(
        bool claimAll, bool mixed) => RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync(mixed: mixed);
        var before = fixture.CaptureTransferFrame();
        var expected = ExpectedAtomicMailFiles(fixture, before, mixed);
        var observed = 0;
        fixture.SendObserver = _ => { fixture.AssertFiles(expected); observed++; };
        var started = DateTime.Now;

        await fixture.FrameAsync(Fixture.AtomicMailRequest(claimAll));

        var ended = DateTime.Now;
        fixture.SendObserver = null;
        Assert.Equal(claimAll ? (mixed ? 7 : 3) : 2, observed);
        AssertAtomicMailCommitted(fixture, before, mixed);
        AssertAtomicMailFrames(fixture, before, claimAll, mixed, started, ended, before.FrameCount);
        var replayFirst = fixture.Sent.Count;
        started = DateTime.Now;

        await fixture.FrameAsync(Fixture.AtomicMailRequest(claimAll));

        AssertAtomicMailCommitted(fixture, before, mixed);
        AssertAtomicMailFrames(fixture, before, claimAll, mixed, started, DateTime.Now, replayFirst, replay: true);
    });

    [WindowsTradeJournalFailureFact]
    public Task ActualD13FrameMailboxItemLeafSharingFailurePublishesNothingThenSameOwnerRetries() =>
        RunAtomicMailSharingFailureAsync(mailLeaf: false);

    [WindowsTradeJournalFailureFact]
    public Task ActualD13FrameMailboxMailLeafSharingFailurePublishesNothingThenSameOwnerRetries() =>
        RunAtomicMailSharingFailureAsync(mailLeaf: true);

    private static Task RunAtomicMailSharingFailureAsync(bool mailLeaf) => RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync();
        var before = fixture.CaptureTransferFrame();
        if (mailLeaf) fixture.HoldAtomicMailFile(); else fixture.HoldTransferItemFile();

        await fixture.FrameAsync(Fixture.AtomicMailRequest());

        fixture.ReleaseFiles(); // Actual Frame is terminal before releasing its owned leaf.
        fixture.AssertFiles(before.Files);
        Assert.Equal(before.FrameCount, fixture.Sent.Count);
        var started = DateTime.Now;
        await fixture.FrameAsync(Fixture.AtomicMailRequest());
        AssertAtomicMailCommitted(fixture, before);
        AssertAtomicMailFrames(fixture, before, false, false, started, DateTime.Now, before.FrameCount);
    });

    [Fact]
    public Task ActualD13FrameMailboxFirstSendFaultRetainsBothCommittedFilesAndReplayHasNoSecondItem() => RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync();
        var before = fixture.CaptureTransferFrame();
        var observed = 0;
        const string failure = "mailbox first send deliberately failed";
        fixture.SendObserver = _ =>
        {
            fixture.AssertFiles(ExpectedAtomicMailFiles(fixture, before));
            observed++;
            throw new IOException(failure);
        };

        await fixture.FrameAsync(Fixture.AtomicMailRequest());

        fixture.SendObserver = null;
        Assert.Equal(1, observed);
        Assert.Equal(before.FrameCount + 1, fixture.Sent.Count); // Attempt recorded before delegate failure.
        Assert.Equal(AtomicMailItemFrame(before), fixture.Sent[before.FrameCount]);
        Assert.Contains(failure, fixture.Log.ToString());
        AssertAtomicMailCommitted(fixture, before);
        var replayFirst = fixture.Sent.Count;
        await fixture.FrameAsync(Fixture.AtomicMailRequest());
        AssertAtomicMailCommitted(fixture, before);
        Assert.Equal(Fixture.AtomicMailRequest(), Assert.Single(fixture.Sent.Skip(replayFirst)));
    });

    [Fact]
    public Task ActualD13FrameMailboxTransportCloseAfterItemSuppressesAcknowledgementWithoutUndoingCommit() => RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync();
        var before = fixture.CaptureTransferFrame();
        Task? close = null;
        var observed = 0;
        fixture.SendObserver = _ =>
        {
            fixture.AssertFiles(ExpectedAtomicMailFiles(fixture, before));
            observed++;
            if (observed != 1) return;
            // The actual Frame cleanup synchronously marks ended, then may await its own lease.
            // Track its real task; never wait inside the send callback or detach it.
            close = fixture.Connection.CompleteTransportCloseAsync();
            fixture.Calls.Add(close);
        };

        await fixture.FrameAsync(Fixture.AtomicMailRequest());

        fixture.SendObserver = null;
        Assert.NotNull(close);
        await fixture.ObserveAsync(close!);
        Assert.Equal(1, observed);
        Assert.Equal(before.FrameCount + 1, fixture.Sent.Count);
        Assert.Equal(AtomicMailItemFrame(before), fixture.Sent[before.FrameCount]);
        AssertAtomicMailCommitted(fixture, before);
    });

    [Fact]
    public Task ActualD13FrameMailboxCapacityRefusalPreservesCompleteFilesAndAttachmentWithoutClaimReply() => RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync(inventoryCount: 40);
        var before = fixture.CaptureTransferFrame();

        await fixture.FrameAsync(Fixture.AtomicMailRequest());

        fixture.AssertFiles(before.Files);
        var frames = fixture.Sent.Skip(before.FrameCount).ToArray();
        Assert.Equal(LegacyMsgMessagePacket.SystemTalk("STR_BAG_FULL@@").ToFrameBytes(), Assert.Single(frames));
        Assert.DoesNotContain(frames, bytes => ReadFrame(bytes).Header.Opcode is
            WorldPacketOpcodes.LegacyMailboxMode or WorldPacketOpcodes.LegacyMailList);
        var repeatFirst = fixture.Sent.Count;
        await fixture.FrameAsync(Fixture.AtomicMailRequest());
        fixture.AssertFiles(before.Files);
        Assert.Equal(frames[0], Assert.Single(fixture.Sent.Skip(repeatFirst)));
        Assert.Equal(repeatFirst + 1, fixture.Sent.Count);
    });

    [Fact]
    public Task ActualD13FrameMailboxClaimAllPackageFlagRefusesBeforeMoneyItemOrMailMutation() => RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync(mixed: true, flag: 1);
        var before = fixture.CaptureTransferFrame();

        await fixture.FrameAsync(Fixture.AtomicMailRequest(claimAll: true));

        fixture.AssertFiles(before.Files);
        Assert.Equal(before.FrameCount, fixture.Sent.Count);
    });

    [Fact]
    public Task ActualD13FrameMailboxTrailingRawByteRefusesDespiteValidCachedAttachment() => RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync(malformedTail: true);
        var before = fixture.CaptureTransferFrame();

        await fixture.FrameAsync(Fixture.AtomicMailRequest());

        fixture.AssertFiles(before.Files);
        Assert.Equal(before.FrameCount, fixture.Sent.Count);
    });
}

using System.Buffers.Binary;
using System.Text.Json;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LocalOperatorMailPipeTests
{
    private sealed class WindowsOperatorMailPipeTheoryAttribute : TheoryAttribute
    {
        public WindowsOperatorMailPipeTheoryAttribute()
        { if (!OperatingSystem.IsWindows()) Skip = "Actual CurrentUserOnly mail abrupt-peer recovery requires Windows."; }
    }

    [WindowsOperatorMailPipeTheory]
    [InlineData("partial-header")]
    [InlineData("partial-body")]
    [InlineData("partial-receipt")]
    public Task ActualAbruptMailPeerEofAllowsKnownOperationReplayWithoutAnotherGrant(string phase) =>
        WithPipeAsync(async (fixture, endpoint) =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var peer = Peer(endpoint.PipeName);
            var known = fixture.Request(Guid.NewGuid());
            LegacyOperatorGearMailResult committed;
            if (phase == "partial-receipt")
            {
                await WriteRequest(peer, known, deadline.Token);
                committed = await ReadResult(peer, known.OperationId, deadline.Token);
                await WaitStatus(endpoint, "responseAwaitingReceipt", deadline.Token);
            }
            else
            {
                committed = await ExchangeAsync(endpoint.PipeName, known);
                await peer.ConnectAsync(deadline.Token);
            }
            Assert.Equal(LegacyOperatorGearMailStatus.CommittedNotificationCompleted, committed.Status);
            var itemUid = Assert.Single(committed.ItemUids); var mailId = Assert.Single(committed.MailIds);
            Assert.Equal(itemUid, Assert.Single(fixture.Items().WarehouseItems).Uid);
            Assert.Equal(mailId, Assert.Single(fixture.Mail()).Id);
            var files = fixture.Fixture.Files(); var sent = fixture.SentSnapshot(); var high = fixture.HighWater; var current = fixture.Current;
            if (phase == "partial-header")
                await peer.WriteAsync(new byte[] { 0x40, 0x00 }, deadline.Token);
            else if (phase == "partial-body")
            {
                // A distinct incomplete operation must never reserve or grant.
                var body = JsonSerializer.SerializeToUtf8Bytes(fixture.Request(Guid.NewGuid()), RequestJson);
                Assert.True(body.Length > 1);
                var header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, body.Length);
                await peer.WriteAsync(header, deadline.Token);
                await peer.WriteAsync(body.AsMemory(0, body.Length - 1), deadline.Token);
            }
            else if (phase == "partial-receipt")
            {
                var receipt = LocalOperatorMailPipe.CreateResponseReceipt(known.OperationId);
                await peer.WriteAsync(receipt.AsMemory(0, receipt.Length - 1), deadline.Token);
            }
            else throw new ArgumentOutOfRangeException(nameof(phase));
            await peer.FlushAsync(deadline.Token);
            peer.Dispose(); // Genuine EOF, with the actual endpoint still enabled.
            await WaitStatus(endpoint, "peerIoStoppedOutcomeMayBeUnknown", deadline.Token);
            Assert.False(endpoint.Completion.IsCompleted);
            UnchangedCommittedMail(fixture, files, sent, high, current, itemUid, mailId);

            // Explicit same-operation regression control, never CLI auto-retry.
            var repeated = await ExchangeAsync(endpoint.PipeName, known);
            Assert.Equal(LegacyOperatorGearMailStatus.PreviouslySubmittedOutcomeUnproven, repeated.Status);
            Assert.Empty(repeated.MailIds); Assert.Empty(repeated.ItemUids);
            Assert.False(endpoint.Completion.IsCompleted);
            UnchangedCommittedMail(fixture, files, sent, high, current, itemUid, mailId);
        });

    private static void UnchangedCommittedMail(OperatorGearMailFixture fixture, Dictionary<string, byte[]> files,
        byte[][] sent, uint high, WorldSession current, uint itemUid, uint mailId)
    {
        fixture.Fixture.AssertFiles(files); Assert.Equal(sent, fixture.SentSnapshot()); Assert.Equal(high, fixture.HighWater);
        Assert.Same(current, fixture.Current); Assert.False(fixture.Native.OrdinarySpellBinding!.IsRetired);
        Assert.Equal(itemUid, Assert.Single(fixture.Items().WarehouseItems).Uid);
        var mail = Assert.Single(fixture.Mail()); Assert.Equal(mailId, mail.Id); Assert.Equal(itemUid, mail.ItemId);
    }
}

using System.Buffers.Binary;
using System.IO.Pipes;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LocalOperatorMailPipeTests
{
    private sealed class WindowsOperatorPipeFactAttribute : FactAttribute
    {
        public WindowsOperatorPipeFactAttribute()
        { if (!OperatingSystem.IsWindows()) Skip = "Actual CurrentUserOnly operator named-pipe IO requires Windows."; }
    }

    [Fact]
    public void ResponseReceiptHasExactRevisionMarkerAndReturnedOperationIdentity()
    {
        var operation = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        byte[] expected = [0x57, 0x43, 0x4d, 0x32, 0x33, 0x22, 0x11, 0x00,
            0x55, 0x44, 0x77, 0x66, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff];
        Assert.Equal(20, LocalOperatorMailPipe.ResponseReceiptLength);
        Assert.Equal(expected, LocalOperatorMailPipe.CreateResponseReceipt(operation));
        Assert.NotEqual(expected, LocalOperatorMailPipe.CreateResponseReceipt(Guid.NewGuid()));
    }

    [WindowsOperatorPipeFact]
    public Task ActualResponseRemainsConnectedWhileCompleteTypedBodyIsReadInFragments() => WithPipeAsync(async (fixture, endpoint) =>
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var peer = Peer(endpoint.PipeName);
        var request = fixture.Request(Guid.NewGuid());
        await WriteRequest(peer, request, deadline.Token);
        var header = new byte[4];
        await peer.ReadExactlyAsync(header, deadline.Token);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        Assert.InRange(length, 1, LocalOperatorMailPipe.MaximumResponseBytes);
        // Deliberately do NOT consume the body until the real server has
        // completed its write/flush and is waiting for this operation's receipt.
        await WaitStatus(endpoint, "responseAwaitingReceipt", deadline.Token);
        Assert.False(endpoint.Completion.IsCompleted);
        var body = new byte[length];
        for (var offset = 0; offset < length;)
        {
            var count = Math.Min(3, length - offset);
            await peer.ReadExactlyAsync(body.AsMemory(offset, count), deadline.Token);
            offset += count;
        }
        var result = LocalOperatorMailPipe.ParseResult(body);
        Assert.Equal(request.OperationId, result.OperationId);
        Assert.Equal(LegacyOperatorGearMailStatus.CommittedNotificationCompleted, result.Status);
        Assert.Equal(Assert.Single(result.ItemUids), Assert.Single(fixture.Items().WarehouseItems).Uid);
        Assert.Equal(Assert.Single(result.MailIds), Assert.Single(fixture.Mail()).Id);
        Assert.Equal("responseAwaitingReceipt", endpoint.LastTransportStatus);
        await peer.WriteAsync(LocalOperatorMailPipe.CreateResponseReceipt(result.OperationId), deadline.Token);
        await peer.FlushAsync(deadline.Token);
        await WaitStatus(endpoint, "typedResponseAcknowledged", deadline.Token);
    });

    [WindowsOperatorPipeFact]
    public Task WrongOperationReceiptCannotRollbackOrRegrantTheAlreadyCommittedMail() => WithPipeAsync(async (fixture, endpoint) =>
    {
        var request = fixture.Request(Guid.NewGuid());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        uint highWater;
        uint itemUid;
        uint mailId;
        await using (var peer = Peer(endpoint.PipeName))
        {
            await WriteRequest(peer, request, deadline.Token);
            var result = await ReadResult(peer, request.OperationId, deadline.Token);
            Assert.Equal(LegacyOperatorGearMailStatus.CommittedNotificationCompleted, result.Status);
            highWater = fixture.HighWater; itemUid = Assert.Single(result.ItemUids); mailId = Assert.Single(result.MailIds);
            await peer.WriteAsync(LocalOperatorMailPipe.CreateResponseReceipt(Guid.NewGuid()), deadline.Token);
            await peer.FlushAsync(deadline.Token);
            await WaitStatus(endpoint, "peerIoStoppedOutcomeMayBeUnknown", deadline.Token);
        }
        // An explicit same-operation replay is a regression control, never a
        // transport/CLI automatic retry or authority to materialize another item.
        var repeated = await ExchangeAsync(endpoint.PipeName, request);
        Assert.Equal(LegacyOperatorGearMailStatus.PreviouslySubmittedOutcomeUnproven, repeated.Status);
        Assert.Equal(highWater, fixture.HighWater);
        Assert.Equal(itemUid, Assert.Single(fixture.Items().WarehouseItems).Uid);
        Assert.Equal(mailId, Assert.Single(fixture.Mail()).Id);
    });

    [WindowsOperatorPipeFact]
    public Task DisposeCancelsAnOutstandingReceiptAndJoinsActualWorkerWithoutUndoingGrant() => WithPipeAsync(async (fixture, endpoint) =>
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var peer = Peer(endpoint.PipeName);
        var request = fixture.Request(Guid.NewGuid());
        await WriteRequest(peer, request, deadline.Token);
        var result = await ReadResult(peer, request.OperationId, deadline.Token);
        Assert.Equal(LegacyOperatorGearMailStatus.CommittedNotificationCompleted, result.Status);
        await WaitStatus(endpoint, "responseAwaitingReceipt", deadline.Token);
        var highWater = fixture.HighWater;
        // No receipt is sent. Shutdown must cancel that genuine asynchronous
        // read and await the real worker; a timeout is a test failure, not drain.
        await endpoint.DisposeAsync().AsTask().WaitAsync(deadline.Token);
        Assert.True(endpoint.Completion.IsCompleted);
        Assert.Equal(highWater, fixture.HighWater);
        Assert.Equal(Assert.Single(result.ItemUids), Assert.Single(fixture.Items().WarehouseItems).Uid);
        Assert.Equal(Assert.Single(result.MailIds), Assert.Single(fixture.Mail()).Id);
    });

    private static Task WithPipeAsync(Func<OperatorGearMailFixture, LocalOperatorMailPipe, Task> test) =>
        LegacyOperatorGearMailOwnerTests.WithOwnerAsync(async fixture =>
        {
            var previous = Environment.GetEnvironmentVariable("WOLFCONQUER_OPERATOR_MAIL");
            try
            {
                Environment.SetEnvironmentVariable("WOLFCONQUER_OPERATOR_MAIL", "1");
                var endpoint = Assert.IsType<LocalOperatorMailPipe>(LocalOperatorMailPipe.StartIfEnabled(fixture.Handler, fixture.Runtime, fixture.Domain));
                Exception? primary = null;
                try { await test(fixture, endpoint); }
                catch (Exception error) { primary = error; }
                try { await endpoint.DisposeAsync(); }
                catch (Exception cleanup)
                {
                    if (primary is not null) throw new AggregateException(primary, cleanup);
                    throw;
                }
                if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
                Assert.True(endpoint.Completion.IsCompleted);
            }
            finally { Environment.SetEnvironmentVariable("WOLFCONQUER_OPERATOR_MAIL", previous); }
        });

    private static NamedPipeClientStream Peer(string name) => new(".", name, PipeDirection.InOut,
        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    private static async Task WriteRequest(NamedPipeClientStream peer, LegacyOperatorGearMailRequest request, CancellationToken token)
    {
        await peer.ConnectAsync(token);
        var body = JsonSerializer.SerializeToUtf8Bytes(request, RequestJson);
        var frame = new byte[4 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, body.Length); body.CopyTo(frame, 4);
        await peer.WriteAsync(frame, token); await peer.FlushAsync(token);
    }

    private static async Task<LegacyOperatorGearMailResult> ReadResult(NamedPipeClientStream peer, Guid expectedOperation, CancellationToken token)
    {
        var header = new byte[4]; await peer.ReadExactlyAsync(header, token);
        var count = BinaryPrimitives.ReadInt32LittleEndian(header);
        Assert.InRange(count, 1, LocalOperatorMailPipe.MaximumResponseBytes);
        var body = new byte[count]; await peer.ReadExactlyAsync(body, token);
        var result = LocalOperatorMailPipe.ParseResult(body);
        Assert.Equal(expectedOperation, result.OperationId);
        return result;
    }

    private static async Task WaitStatus(LocalOperatorMailPipe endpoint, string expected, CancellationToken token)
    {
        while (endpoint.LastTransportStatus != expected)
        {
            Assert.False(endpoint.Completion.IsCompleted, "Actual operator worker ended before the expected receipt boundary.");
            await Task.Delay(1, token);
        }
    }
}

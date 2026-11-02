using System.Buffers;
using System.Diagnostics;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRelicDecomposeTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task ActualStartedDecomposeWriteAllowsOutOfViewAuthenticatedProgressAndRetainsCommittedBatch(bool cancel) => Run(async f =>
    {
        var peer = Assert.IsType<RootLoopbackFramePeer>(f.Peer);
        await peer.PauseReaderAfterNextFrame(); f.Sent.Clear(); f.PeerSent.Clear();
        await peer.PrimeRealSocketUntilBlocked();
        Assert.False(peer.RealFrameWriteInProgress);
        var high = f.HighWater; var starts = peer.WireStarts;
        var bytes = DecomposeFixture.Request(f.Selected);
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        using var operationLifetime = CancellationTokenSource.CreateLinkedTokenSource(f.ActualToken);
        var producing = f.Handler.HandleFrameAsync(f.ActualConnection, frame, operationLifetime.Token).AsTask();
        var timer = Stopwatch.StartNew();
        while ((!peer.RealFrameWriteInProgress || peer.WireStarts == starts) && timer.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(10);
        Assert.Equal(starts + 1, peer.WireStarts);
        Assert.True(peer.RealFrameWriteInProgress);
        var stablePressure = Stopwatch.StartNew();
        while (stablePressure.Elapsed < TimeSpan.FromMilliseconds(500))
        {
            Assert.True(peer.RealFrameWriteInProgress);
            Assert.False(producing.IsCompleted);
            await Task.Delay(10);
        }
        Assert.Equal(high + 18U, f.HighWater);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        f.AssertConverged();
        var saved = File.ReadAllBytes(f.ItemsPath);
        var rewards = PlayerItemSnapshotLoader.Parse(saved).InventoryItems
            .Where(row => row.ItemId == DecomposeFixture.Crystal).Select(row => row.Uid).ToArray();
        Assert.Equal(Enumerable.Range(1, 18).Select(index => checked(high + (uint)index)), rewards);

        // The second player is actually admitted and authenticated on this same
        // handler; its real reply proves world progress while the write remains pending.
        Assert.NotNull(f.PeerCurrent.NativeGameClient!.OrdinarySpellBinding);
        Assert.Equal(f.Current.Character.MapId, f.PeerCurrent.Character.MapId);
        Assert.Equal(430, f.Current.Character.X); Assert.Equal(380, f.Current.Character.Y);
        Assert.Equal(100, f.PeerCurrent.Character.X); Assert.Equal(100, f.PeerCurrent.Character.Y);
        await f.SendPeerAsync(LegacyRuneStoragePacket.Startup().ToFrameBytes()).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEmpty(f.PeerSent);
        Assert.True(peer.RealFrameWriteInProgress); Assert.False(producing.IsCompleted);
        var committedFiles = f.Data.Files();
        var priorDiagnostics = f.ReadDiagnostics();
        if (cancel) operationLifetime.Cancel();
        await producing.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.False(f.ActualConnection.HasRegisteredRelicWire);
        if (cancel)
        {
            var diagnostic = f.ReadDiagnostics()[priorDiagnostics.Length..];
            Assert.Contains("LegacyCommittedSpellPublicationException", diagnostic);
            Assert.Contains("CanceledException", diagnostic);
            Assert.DoesNotContain(f.Data.Users, diagnostic, StringComparison.OrdinalIgnoreCase);
        }
        var settling = Stopwatch.StartNew();
        while (peer.RealFrameWriteOutstanding && settling.Elapsed < TimeSpan.FromSeconds(5)) await Task.Delay(10);
        Assert.False(peer.RealFrameWriteOutstanding);
        Assert.Equal(starts + 1, peer.WireStarts); Assert.Empty(f.Sent);
        Assert.Equal(high + 18U, f.HighWater); f.Data.AssertFiles(committedFiles);
        await f.CloseAsync(); await f.ReopenFrameAsync(); f.Sent.Clear();
        Assert.Equal(saved, File.ReadAllBytes(f.ItemsPath)); f.AssertConverged();
        await f.SendAsync(bytes);
        Assert.Empty(f.Sent); Assert.Equal(high + 18U, f.HighWater);
        Assert.Equal(saved, File.ReadAllBytes(f.ItemsPath)); f.Data.AssertFiles(committedFiles); f.AssertConverged();
    }, count: 2, ranking: true, distantPeer: true);

    [Fact]
    public Task VisibleAuthenticatedPeerReceivesRuneResponseWhileRelicWritePendingAndHandlersSettleAfterCancel() => Run(async f =>
    {
        var recipient = Assert.IsType<RootLoopbackFramePeer>(f.Peer);
        Assert.Equal(f.Current.Character.MapId, f.PeerCurrent.Character.MapId);
        Assert.Equal(f.Current.Character.X, f.PeerCurrent.Character.X);
        Assert.Equal(f.Current.Character.Y, f.PeerCurrent.Character.Y);
        await recipient.PauseReaderAfterNextFrame(); f.Sent.Clear(); f.PeerSent.Clear();
        await recipient.PrimeRealSocketUntilBlocked();
        var high = f.HighWater; var starts = recipient.WireStarts;
        var request = DecomposeFixture.Request(f.Selected);
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(request), out var frame, out _, out _));
        using var operationLifetime = CancellationTokenSource.CreateLinkedTokenSource(f.ActualToken);
        var producing = f.Handler.HandleFrameAsync(f.ActualConnection, frame, operationLifetime.Token).AsTask();
        var timer = Stopwatch.StartNew();
        while ((!recipient.RealFrameWriteInProgress || recipient.WireStarts == starts) && timer.Elapsed < TimeSpan.FromSeconds(5)) await Task.Delay(10);
        Assert.Equal(starts + 1, recipient.WireStarts); Assert.True(recipient.RealFrameWriteInProgress);
        var stable = Stopwatch.StartNew();
        while (stable.Elapsed < TimeSpan.FromMilliseconds(500))
        { Assert.True(recipient.RealFrameWriteInProgress); Assert.False(producing.IsCompleted); await Task.Delay(10); }
        Assert.Equal(high + 18U, f.HighWater); f.AssertConverged();
        var saved = File.ReadAllBytes(f.ItemsPath);
        var observed = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.BeforePeerSend = bytes =>
        {
            if (PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var responseFrame, out _, out _) &&
                LegacyRuneStoragePacket.TryParse(responseFrame, out var response) && response.Type == LegacyRuneStoragePacket.StartupType)
                observed.TrySetResult(bytes);
        };
        var peerRequest = f.SendPeerAsync(LegacyRuneStoragePacket.Startup().ToFrameBytes());
        try
        {
            var actualResponse = await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(actualResponse), out var responseFrame, out _, out _));
            Assert.True(LegacyRuneStoragePacket.TryParse(responseFrame, out var response));
            Assert.Equal(LegacyRuneStoragePacket.StartupType, response.Type);
            Assert.True(recipient.RealFrameWriteInProgress); Assert.False(producing.IsCompleted);
            operationLifetime.Cancel();
            await producing.WaitAsync(TimeSpan.FromSeconds(15));
            await peerRequest.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { f.BeforePeerSend = null; }
        Assert.False(f.ActualConnection.HasRegisteredRelicWire);
        Assert.Equal(starts + 1, recipient.WireStarts);
        Assert.Equal(high + 18U, f.HighWater); Assert.Equal(saved, File.ReadAllBytes(f.ItemsPath));
        await f.CloseAsync(); await f.ReopenFrameAsync(); f.Sent.Clear();
        Assert.Equal(high + 18U, f.HighWater); Assert.Equal(saved, File.ReadAllBytes(f.ItemsPath));
        await f.SendAsync(request); Assert.Empty(f.Sent); Assert.Equal(high + 18U, f.HighWater); f.AssertConverged();
    }, count: 2, ranking: true);
}

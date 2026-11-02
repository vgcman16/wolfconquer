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
    public Task ActualStartedDecomposeWriteAllowsAuthenticatedProgressAndRetainsCommittedBatch(bool cancel) => Run(async f =>
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
    }, count: 2, ranking: true);
}

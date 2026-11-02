using System.Buffers;
using System.Diagnostics;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRelicFuseTests
{
    [Fact]
    public Task ActualStartedRegisteredRelicWriteDeadlineRetiresSocketAndPreservesCommit() => Run(async f =>
    {
        var peer = Assert.IsType<RootLoopbackFramePeer>(f.Peer);
        await peer.PauseReaderAfterNextFrame(); f.Sent.Clear();
        // This only primes the real socket with complete framed padding under
        // its own semaphore. The following relic write is the actual registered
        // producer, not the priming code or a supplied completion callback.
        await peer.PrimeRealSocketUntilBlocked();
        Assert.False(peer.RealFrameWriteInProgress);
        var starts = peer.WireStarts; var highWater = f.HighWater;
        var bytes = new LegacyRelicFusePacket(0, FuseFixture.First, FuseFixture.Second, 0).ToFrameBytes();
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        var operation = f.Handler.HandleFrameAsync(f.ActualConnection, frame, f.ActualToken).AsTask();
        var timer = Stopwatch.StartNew();
        while ((!peer.RealFrameWriteInProgress || peer.WireStarts == starts) && timer.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(10);
        Assert.Equal(starts + 1, peer.WireStarts);
        Assert.True(peer.RealFrameWriteInProgress, "The registered relic WriteAsync must itself still be pending while holding the real gate.");
        var stillPending = Stopwatch.StartNew();
        while (stillPending.Elapsed < TimeSpan.FromMilliseconds(500))
        {
            Assert.True(peer.RealFrameWriteInProgress);
            Assert.False(operation.IsCompleted);
            await Task.Delay(10);
        }
        Assert.Equal(0, peer.ExpiryWriter.RelicWaitingForGate);
        Assert.False(operation.IsCompleted); Assert.Equal(highWater + 1, f.HighWater);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        await using var independentPeer = new RootLoopbackFramePeer(_ => ValueTask.CompletedTask);
        var independentBytes = new LegacyRelicFusePacket(9, 0, 0, 0).ToFrameBytes();
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(independentBytes), out var probe, out _, out _));
        var independent = f.Handler.HandleFrameAsync(independentPeer.Connection, probe, CancellationToken.None).AsTask();
        await independent.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(peer.RealFrameWriteInProgress);
        Assert.False(operation.IsCompleted);
        await operation.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(peer.ExpiryWriter.IsClosed); Assert.Empty(f.Sent);
        var writeSettling = Stopwatch.StartNew();
        while (peer.RealFrameWriteOutstanding && writeSettling.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(10);
        Assert.False(peer.RealFrameWriteOutstanding);
        Assert.Equal(starts + 1, peer.WireStarts); // No late second relic frame.
        Assert.Equal(highWater + 1, f.HighWater); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        var saved = File.ReadAllBytes(f.ItemsPath);
        Assert.Equal(highWater + 1, Assert.Single(PlayerItemSnapshotLoader.Parse(saved).RelicFuseItems, row => row.Position == 215).Uid);
        await f.CloseAsync(); await f.ReopenFrameAsync(); f.Sent.Clear();
        await f.SendAsync(bytes);
        Assert.Empty(f.Sent); Assert.Equal(saved, File.ReadAllBytes(f.ItemsPath)); Assert.Equal(highWater + 1, f.HighWater);
        f.AssertConverged();
    });
}

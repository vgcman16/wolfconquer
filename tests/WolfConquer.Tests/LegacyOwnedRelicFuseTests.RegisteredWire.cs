using System.Buffers;
using System.Diagnostics;
using System.Net.Sockets;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRelicFuseTests
{
    [Fact]
    public Task UnassociatedPermitAndNullStartCannotInvokeTheRegisteredWire() => Run(f =>
    {
        var peer = Assert.IsType<RootLoopbackFramePeer>(f.Peer);
        var starts = peer.WireStarts;
        Assert.False(f.Store.TryStartRelicWire(new WorldSessionStore.RelicWirePermit(), f.ActualConnection,
            peer.ExpiryWriter, null!, CancellationToken.None, out _));
        Assert.Equal(starts, peer.WireStarts);
        return Task.CompletedTask;
    });

    [Fact]
    public Task ActualRegisteredWriterPublishesTwoOrderedCommittedRelicFrames() => Run(async f =>
    {
        var peer = Assert.IsType<RootLoopbackFramePeer>(f.Peer);
        var starts = peer.WireStarts; var highWater = f.HighWater;
        await f.SendAsync(new LegacyRelicFusePacket(0, FuseFixture.First, FuseFixture.Second, 0).ToFrameBytes());
        Assert.Equal(starts + 2, peer.WireStarts); Assert.Equal(2, f.Sent.Count);
        var result = Assert.Single(f.Current.PlayerItems.RelicFuseItems, row => row.Position == 215);
        Assert.Equal(highWater + 1, result.Uid);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(result).ToFrameBytes(), f.Sent[0]);
        Assert.Equal(new LegacyRelicFusePacket(0, FuseFixture.First, FuseFixture.Second, result.Uid).ToFrameBytes(), f.Sent[1]);
        f.AssertConverged();
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualRegisteredRelicGateRetiresStalledOrClosedPeerWithoutBlockingWorldForever(bool closePeer) => Run(async f =>
    {
        var peer = Assert.IsType<RootLoopbackFramePeer>(f.Peer);
        var highWater = f.HighWater;
        var filler = peer.StartBackpressuredWrite();
        await peer.WaitForBackpressure();
        var bytes = new LegacyRelicFusePacket(0, FuseFixture.First, FuseFixture.Second, 0).ToFrameBytes();
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        var producing = f.Handler.HandleFrameAsync(f.ActualConnection, frame, f.ActualToken).AsTask();
        var timer = Stopwatch.StartNew();
        while (peer.ExpiryWriter.RelicWaitingForGate == 0 && timer.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(10);
        Assert.True(peer.ActualBackpressure); Assert.Equal(1, peer.ExpiryWriter.RelicWaitingForGate);
        Assert.Equal(highWater + 1, f.HighWater);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        Assert.False(producing.IsCompleted);

        await using var other = new RootLoopbackFramePeer(_ => ValueTask.CompletedTask);
        var probeBytes = new LegacyRelicFusePacket(9, 0, 0, 0).ToFrameBytes();
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(probeBytes), out var probe, out _, out _));
        var independent = f.Handler.HandleFrameAsync(other.Connection, probe, CancellationToken.None).AsTask();
        await independent.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(peer.ActualBackpressure);
        Assert.Equal(1, peer.ExpiryWriter.RelicWaitingForGate);
        Assert.False(producing.IsCompleted);
        if (closePeer) peer.CloseReader();
        await producing.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Empty(f.Sent); Assert.Equal(highWater + 1, f.HighWater);
        try { await filler.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
        // Committed data is not rolled back or re-issued when output is lost.
        var saved = PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(f.ItemsPath));
        Assert.Equal(3, saved.RelicFuseItems.Count);
        Assert.Equal(highWater + 1, saved.RelicFuseItems.Single(row => row.Position == 215).Uid);
        await f.CloseAsync();
        await f.ReopenFrameAsync();
        Assert.Equal(highWater + 1, f.HighWater); f.Sent.Clear();
        await f.SendAsync(bytes);
        Assert.Empty(f.Sent); Assert.Equal(highWater + 1, f.HighWater);
        f.AssertConverged();
    });
}

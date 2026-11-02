using System.Buffers;
using System.Diagnostics;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRelicFuseTests
{
    [Fact]
    public Task ActualReturnHandoffAllowsIndependentFrameBeforeItsBlockedOutputCompletes() => Run(async f =>
    {
        await Compose(f); f.Sent.Clear();
        var before = f.Current; var selected = before.PlayerItems.RelicFuseItems.Single(row => row.Position == 215);
        var highWater = f.HighWater; var ledger = File.ReadAllBytes(f.Data.State);
        var peer = Assert.IsType<RootLoopbackFramePeer>(f.Peer);
        var filler = peer.StartBackpressuredWrite(); await peer.WaitForBackpressure();
        var request = new LegacyRelicFusePacket(1, selected.Uid, 0, 0);
        var bytes = request.ToFrameBytes();
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        var producing = f.Handler.HandleFrameAsync(f.ActualConnection, frame, f.ActualToken).AsTask();
        var timer = Stopwatch.StartNew();
        while (peer.ExpiryWriter.RelicWaitingForGate == 0 && timer.Elapsed < TimeSpan.FromSeconds(5)) await Task.Delay(10);
        Assert.True(peer.ActualBackpressure); Assert.Equal(1, peer.ExpiryWriter.RelicWaitingForGate);
        Assert.False(producing.IsCompleted); Assert.Empty(f.Current.PlayerItems.RelicFuseItems);
        Assert.Contains(f.Current.PlayerItems.InventoryItems, row => row.Uid == selected.Uid && row.Position == 0);
        Assert.Equal(highWater, f.HighWater); Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); f.AssertConverged();
        await using var other = new RootLoopbackFramePeer(_ => ValueTask.CompletedTask);
        var probeBytes = new LegacyRelicFusePacket(9, 0, 0, 0).ToFrameBytes();
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(probeBytes), out var probe, out _, out _));
        await f.Handler.HandleFrameAsync(other.Connection, probe, CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(peer.ActualBackpressure); Assert.Equal(1, peer.ExpiryWriter.RelicWaitingForGate);
        Assert.False(producing.IsCompleted);
        await peer.DrainBackpressuredWrite(); await filler.WaitAsync(TimeSpan.FromSeconds(5));
        await producing.WaitAsync(TimeSpan.FromSeconds(5)); await peer.DrainObservations();
        Assert.Equal(2, f.Sent.Count);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(selected.WithPosition(0), mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes(), f.Sent[0]);
        Assert.Equal(bytes, f.Sent[1]); Assert.Equal(highWater, f.HighWater); Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
        f.AssertConverged();
    });
}

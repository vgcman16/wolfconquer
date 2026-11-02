using System.Buffers;
using System.Diagnostics;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRelicFuseTests
{
    [Fact]
    public Task RecoveryFenceBeforeRegisteredStartSuppressesOutputWithoutUndoingTheCommit() => Run(async f =>
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
        Assert.True(peer.ActualBackpressure);
        Assert.Equal(1, peer.ExpiryWriter.RelicWaitingForGate);
        Assert.False(producing.IsCompleted);
        Assert.Equal(highWater + 1, f.HighWater);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        var saved = File.ReadAllBytes(f.ItemsPath);
        var actualStarts = peer.WireStarts;

        // The actual world's denial producer runs while the real socket gate
        // is held. This covers a fence observed before start, not an atomic
        // ordering claim for a flag changed concurrently with socket initiation.
        f.Runtime.Recovery.Block("ownedRelicOutputControlFence");
        Assert.True(f.Runtime.Recovery.IsBlocked);
        await peer.DrainBackpressuredWrite();
        await filler.WaitAsync(TimeSpan.FromSeconds(5));
        await producing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(actualStarts, peer.WireStarts);
        Assert.False(f.ActualConnection.HasRegisteredRelicWire);
        Assert.Empty(f.Sent);
        Assert.Equal(saved, File.ReadAllBytes(f.ItemsPath));
        Assert.Equal(highWater + 1, f.HighWater);

        await f.CloseAsync();
        await f.ReopenFrameAsync();
        f.Sent.Clear();
        await f.SendAsync(bytes);
        Assert.Empty(f.Sent);
        Assert.Equal(saved, File.ReadAllBytes(f.ItemsPath));
        Assert.Equal(highWater + 1, f.HighWater);
        f.AssertConverged();

        // The recovered held result remains usable through the actual return
        // handler; restart safety must not strand it in a saved container.
        var selected = f.Current.PlayerItems.RelicFuseItems.Single(row => row.Position == 215);
        var ledger = File.ReadAllBytes(f.Data.State);
        var choose = new LegacyRelicFusePacket(1, selected.Uid, 0, 0).ToFrameBytes();
        await f.SendAsync(choose);
        Assert.Equal(2, f.Sent.Count);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(selected.WithPosition(0),
            mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes(), f.Sent[0]);
        Assert.Equal(choose, f.Sent[1]);
        Assert.Empty(f.Current.PlayerItems.RelicFuseItems);
        Assert.Contains(f.Current.PlayerItems.InventoryItems, row => row.Uid == selected.Uid && row.Position == 0);
        Assert.Equal(highWater + 1, f.HighWater);
        Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
        f.AssertConverged();
    });
}

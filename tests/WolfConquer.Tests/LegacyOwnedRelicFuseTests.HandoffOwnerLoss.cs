using System.Buffers;
using System.Diagnostics;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRelicFuseTests
{
    [Fact]
    public Task CloseLifecycleRetirementInjectionSuppressesWaitingCommittedRelicOutput() => Run(async f =>
    {
        var peer = Assert.IsType<RootLoopbackFramePeer>(f.Peer);
        var highWater = f.HighWater; var filler = peer.StartBackpressuredWrite();
        await peer.WaitForBackpressure();
        var bytes = new LegacyRelicFusePacket(0, FuseFixture.First, FuseFixture.Second, 0).ToFrameBytes();
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        var producing = f.Handler.HandleFrameAsync(f.ActualConnection, frame, f.ActualToken).AsTask();
        var timer = Stopwatch.StartNew();
        while (peer.ExpiryWriter.RelicWaitingForGate == 0 && timer.Elapsed < TimeSpan.FromSeconds(5)) await Task.Delay(10);
        Assert.True(peer.ActualBackpressure); Assert.Equal(1, peer.ExpiryWriter.RelicWaitingForGate);
        Assert.Equal(highWater + 1, f.HighWater); Assert.False(producing.IsCompleted);
        var saved = File.ReadAllBytes(f.ItemsPath);
        Assert.Equal(highWater + 1, Assert.Single(PlayerItemSnapshotLoader.Parse(saved).RelicFuseItems, row => row.Position == 215).Uid);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));

        // Inject the actual registered close lifecycle while the gate is held.
        // This exercises genuine Store retirement, not observed network EOF:
        // production TCP invokes this callback after its Frame pipeline exits.
        // No caller-created holder or successful receipt participates.
        await f.ActualConnection.CompleteTransportCloseAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(f.Store.TryGet(f.ActualConnection, out _));
        Assert.True(peer.ActualBackpressure); Assert.False(producing.IsCompleted);
        await peer.DrainBackpressuredWrite(); await filler.WaitAsync(TimeSpan.FromSeconds(5));
        await producing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(f.ActualConnection.HasRegisteredRelicWire);
        Assert.Empty(f.Sent); Assert.Equal(saved, File.ReadAllBytes(f.ItemsPath)); Assert.Equal(highWater + 1, f.HighWater);

        await f.CloseAsync(); await f.ReopenFrameAsync(); f.Sent.Clear();
        await f.SendAsync(bytes);
        Assert.Empty(f.Sent); Assert.Equal(saved, File.ReadAllBytes(f.ItemsPath)); Assert.Equal(highWater + 1, f.HighWater);
        f.AssertConverged();
    });
}

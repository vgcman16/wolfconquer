using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRelicFuseTests
{
    [Theory]
    [InlineData(213)] [InlineData(214)] [InlineData(215)]
    public Task ActualRemoteResetDuringReturnPreservesSelectedUidAcrossReopen(int position) => Run(async f =>
    {
        // This first step is the actual admitted native/file/registry compose.
        await Compose(f); f.Sent.Clear();
        var before = f.Current;
        var selected = before.PlayerItems.RelicFuseItems.Single(row => row.Position == position);
        var expected = PlayerItemSnapshotLoader.ToLegacyBytes(ExpectedReturn(before.PlayerItems, selected));
        var highWater = f.HighWater; var ledger = File.ReadAllBytes(f.Data.State);
        var peer = Assert.IsType<RootLoopbackFramePeer>(f.Peer);
        var starts = peer.WireStarts;
        peer.AbortRemoteOnlyWithReset();
        await peer.WaitForRemoteSocketClosure();
        // The peer reset has not directly closed the local registered writer.
        Assert.False(peer.ExpiryWriter.IsClosed);
        var bytes = new LegacyRelicFusePacket(1, selected.Uid, 0, 0).ToFrameBytes();
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        await f.Handler.HandleFrameAsync(f.ActualConnection, frame, f.ActualToken).AsTask().WaitAsync(TimeSpan.FromSeconds(15));
        // The counter is advanced immediately before the real socket WriteAsync
        // call; no other frame is sent on this connection during this interval.
        Assert.InRange(peer.WireStarts - starts, 1L, 2L);
        Assert.True(peer.ExpiryWriter.IsClosed);
        Assert.Empty(f.Sent);
        Assert.Equal(highWater, f.HighWater); Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        Assert.Equal(expected, File.ReadAllBytes(f.ItemsPath));
        f.AssertConverged();
        await f.CloseAsync(); await f.ReopenFrameAsync(); f.Sent.Clear();
        Assert.Equal(highWater, f.HighWater); Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
        Assert.Equal(expected, File.ReadAllBytes(f.ItemsPath));
        Assert.Empty(f.Current.PlayerItems.RelicFuseItems);
        Assert.Contains(f.Current.PlayerItems.InventoryItems, row => row.Uid == selected.Uid && row.Position == 0);
        await f.SendAsync(bytes);
        Assert.Empty(f.Sent); Assert.Equal(highWater, f.HighWater); Assert.Equal(expected, File.ReadAllBytes(f.ItemsPath));
        f.AssertConverged();
    });

    [Fact]
    public Task ActualRemoteResetAfterComposeCommitDoesNotReissueTheUid() => Run(async f =>
    {
        var peer = Assert.IsType<RootLoopbackFramePeer>(f.Peer);
        var highWater = f.HighWater; var starts = peer.WireStarts;
        peer.AbortRemoteOnlyWithReset(); await peer.WaitForRemoteSocketClosure();
        Assert.False(peer.ExpiryWriter.IsClosed);
        var bytes = new LegacyRelicFusePacket(0, FuseFixture.First, FuseFixture.Second, 0).ToFrameBytes();
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        await f.Handler.HandleFrameAsync(f.ActualConnection, frame, f.ActualToken).AsTask().WaitAsync(TimeSpan.FromSeconds(15));
        Assert.InRange(peer.WireStarts - starts, 1L, 2L); Assert.True(peer.ExpiryWriter.IsClosed);
        Assert.Empty(f.Sent); Assert.Equal(highWater + 1, f.HighWater);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        f.AssertConverged();
        var after = File.ReadAllBytes(f.ItemsPath);
        var saved = PlayerItemSnapshotLoader.Parse(after);
        Assert.Equal(highWater + 1, Assert.Single(saved.RelicFuseItems, row => row.Position == 215).Uid);
        await f.CloseAsync(); await f.ReopenFrameAsync(); f.Sent.Clear();
        await f.SendAsync(bytes);
        Assert.Empty(f.Sent); Assert.Equal(highWater + 1, f.HighWater); Assert.Equal(after, File.ReadAllBytes(f.ItemsPath));
        f.AssertConverged();
    });
}

using System.Buffers;
using System.Diagnostics;
using System.Net.Sockets;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRelicDecomposeTests
{
    [Theory]
    [InlineData("release")] [InlineData("remote-close")] [InlineData("cancel")] [InlineData("timeout")]
    public Task RegisteredDecomposeOutputIsBoundedAndHandsOffExactFrameBeforeSocketWaiting(string mode) => Run(async f =>
    {
        var peer = Assert.IsType<RootLoopbackFramePeer>(f.Peer); var high = f.HighWater;
        var filler = peer.StartBackpressuredWrite(); await peer.WaitForBackpressure();
        var bytes = DecomposeFixture.Request(f.Selected); var frame = ReadFrame(bytes);
        using var frameLifetime = CancellationTokenSource.CreateLinkedTokenSource(f.ActualToken);
        var producing = f.Handler.HandleFrameAsync(f.ActualConnection, frame, frameLifetime.Token).AsTask();
        var timer = Stopwatch.StartNew();
        while (peer.ExpiryWriter.RelicWaitingForGate == 0 && timer.Elapsed < TimeSpan.FromSeconds(5)) await Task.Delay(10);
        Assert.True(peer.ActualBackpressure); Assert.Equal(1, peer.ExpiryWriter.RelicWaitingForGate);
        Assert.False(producing.IsCompleted); Assert.Equal(high + 9, f.HighWater);
        Assert.Equal(9, f.Current.PlayerItems.InventoryItems.Count(row => row.ItemId == DecomposeFixture.Crystal));
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); f.AssertConverged();
        await using var unrelated = new RootLoopbackFramePeer(_ => ValueTask.CompletedTask);
        var probe = ReadFrame(new LegacyRelicFusePacket(9, 0, 0, 0).ToFrameBytes());
        await f.Handler.HandleFrameAsync(unrelated.Connection, probe, CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(peer.ActualBackpressure); Assert.False(producing.IsCompleted);
        var beforeCancellation = f.ReadDiagnostics();
        if (mode == "release") await peer.DrainBackpressuredWrite();
        else if (mode == "remote-close") peer.CloseReader();
        else if (mode == "cancel") frameLifetime.Cancel();
        await producing.WaitAsync(TimeSpan.FromSeconds(15));
        if (mode == "release")
        {
            await peer.DrainObservations(); Assert.Equal(11, f.Sent.Count); Assert.Equal(bytes, f.Sent[^1]);
        }
        else
        {
            Assert.Empty(f.Sent); Assert.False(f.ActualConnection.HasRegisteredRelicWire);
            if (mode == "cancel")
            {
                var diagnostic = f.ReadDiagnostics()[beforeCancellation.Length..];
                Assert.Contains("LegacyCommittedSpellPublicationException", diagnostic);
                Assert.Contains("CanceledException", diagnostic);
                Assert.DoesNotContain(f.Data.Users, diagnostic, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(f.ItemsPath, diagnostic, StringComparison.OrdinalIgnoreCase);
            }
        }
        try { await filler.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
        Assert.Equal(high + 9, f.HighWater);
        var saved = PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(f.ItemsPath));
        Assert.Equal(9, saved.InventoryItems.Count(row => row.ItemId == DecomposeFixture.Crystal));
        await f.CloseAsync(); await f.ReopenFrameAsync(); f.Sent.Clear();
        await f.SendAsync(bytes); Assert.Empty(f.Sent); Assert.Equal(high + 9, f.HighWater); f.AssertConverged();
    });

    [Fact]
    public Task RegisteredDecomposePermitCannotBeForgedOrStartFromNullAuthority() => Run(f =>
    {
        var peer = f.Peer!; var starts = peer.WireStarts;
        Assert.False(f.Store.TryStartRelicWire(new WorldSessionStore.RelicWirePermit(), f.ActualConnection,
            peer.ExpiryWriter, null!, CancellationToken.None, out _));
        Assert.Equal(starts, peer.WireStarts); return Task.CompletedTask;
    });

    [Fact]
    public Task ActualRegisteredOpenedTradeRefusesDecompositionBeforeAnyUidOrOutput() => Run(async f =>
    {
        var request = new LegacyTradePacket(0) { UID = DecomposeFixture.PeerUid }.ToNativeFrameBytes();
        var accept = new LegacyTradePacket(1) { UID = DecomposeFixture.Uid }.ToNativeFrameBytes();
        Assert.True(LegacyTradePacket.TryParse(ReadFrame(request), out var parsedRequest));
        Assert.Equal(0UL, parsedRequest.Action); Assert.Equal((ulong)DecomposeFixture.PeerUid, parsedRequest.UID);
        Assert.True(LegacyTradePacket.TryParse(ReadFrame(accept), out var parsedAccept));
        Assert.Equal(1UL, parsedAccept.Action); Assert.Equal((ulong)DecomposeFixture.Uid, parsedAccept.UID);
        await f.SendAsync(request); await f.SendPeerAsync(accept);
        await f.Peer!.DrainObservations(); await f.OtherPeer!.DrainObservations();
        Assert.True(f.Current.LegacyTradeActive, f.ReadDiagnostics()); Assert.True(f.PeerCurrent.LegacyTradeActive);
        var partnerConnection = Assert.IsType<RootLoopbackFramePeer>(f.OtherPeer).Connection;
        var trades = Assert.IsType<LegacyTradeStateStore>(typeof(WorldServerFrameHandler)
            .GetField("_tradeStates", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(f.Handler));
        LegacyTradeParty party;
        lock (trades.SyncRoot)
        {
            Assert.True(trades.TryGet(f.ActualConnection, out party!));
            Assert.True(trades.TryGet(partnerConnection, out var partner));
            Assert.Same(party.Other, partner); Assert.Same(party, partner.Other);
            Assert.Equal(DecomposeFixture.Uid, party.Uid); Assert.Equal(DecomposeFixture.PeerUid, partner.Uid);
            Assert.True(party.IsRequester); Assert.False(partner.IsRequester);
            Assert.Equal(party.TradeId, partner.TradeId); Assert.NotEqual(Guid.Empty, party.TradeId);
            Assert.True(party.WindowOpen); Assert.True(partner.WindowOpen);
        }
        Assert.Contains(f.Sent, bytes => LegacyTradePacket.TryParse(ReadFrame(bytes), out var opened) &&
            opened.Action == 3 && opened.UID == DecomposeFixture.PeerUid);
        Assert.Contains(f.PeerSent, bytes => LegacyTradePacket.TryParse(ReadFrame(bytes), out var opened) &&
            opened.Action == 3 && opened.UID == DecomposeFixture.Uid);
        f.Sent.Clear(); f.PeerSent.Clear();
        var peerOwner = f.PeerCurrent; var peerFile = File.ReadAllBytes(f.PeerItemsPath);
        var peerBinding = peerOwner.NativeGameClient!.OrdinarySpellBinding!;
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(peerBinding));
        await AssertRefusal(f, DecomposeFixture.Request(f.Selected));
        Assert.Empty(f.PeerSent); Assert.Same(peerOwner, f.PeerCurrent); Assert.Equal(peerFile, File.ReadAllBytes(f.PeerItemsPath));
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(peerBinding));
        lock (trades.SyncRoot)
        {
            Assert.True(trades.TryGet(f.ActualConnection, out var retained)); Assert.Same(party, retained);
            Assert.True(trades.TryGet(partnerConnection, out var retainedPartner)); Assert.Same(party.Other, retainedPartner);
            Assert.True(retained.WindowOpen); Assert.True(retainedPartner.WindowOpen);
            Assert.False(retained.Confirmed); Assert.False(retainedPartner.Confirmed);
            Assert.Empty(retained.Items); Assert.Empty(retainedPartner.Items);
            Assert.Equal(0L, retained.Money); Assert.Equal(0L, retainedPartner.Money);
            Assert.Equal(0U, retained.ConquerPoints); Assert.Equal(0U, retainedPartner.ConquerPoints);
        }
    }, ranking: true);
}

using System.Buffers;
using System.Diagnostics;
using System.Net.Sockets;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRelicFuseTests
{
    [Fact]
    public Task CancellationAfterRelicCommitRetainsCommittedDiagnosticsAndCannotIssueAgain() => Run(async f =>
    {
        var peer = Assert.IsType<RootLoopbackFramePeer>(f.Peer);
        var highWater = f.HighWater;
        var filler = peer.StartBackpressuredWrite();
        await peer.WaitForBackpressure();
        var bytes = new LegacyRelicFusePacket(0, FuseFixture.First, FuseFixture.Second, 0).ToFrameBytes();
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var request, out _, out _));
        using var canceledFrame = CancellationTokenSource.CreateLinkedTokenSource(f.ActualToken);
        var producing = f.Handler.HandleFrameAsync(f.ActualConnection, request, canceledFrame.Token).AsTask();
        var timer = Stopwatch.StartNew();
        while (peer.ExpiryWriter.RelicWaitingForGate == 0 && timer.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(10);
        Assert.True(peer.ActualBackpressure);
        Assert.Equal(1, peer.ExpiryWriter.RelicWaitingForGate);
        Assert.False(producing.IsCompleted);
        Assert.Equal(highWater + 1, f.HighWater);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));

        // An unrelated actual Frame must pass while the committed writer is
        // still waiting; cancellation must not hide the committed outcome.
        await using var independentPeer = new RootLoopbackFramePeer(_ => ValueTask.CompletedTask);
        var independentBytes = new LegacyRelicFusePacket(9, 0, 0, 0).ToFrameBytes();
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(independentBytes), out var probe, out _, out _));
        await f.Handler.HandleFrameAsync(independentPeer.Connection, probe, CancellationToken.None).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(peer.ActualBackpressure);
        Assert.Equal(1, peer.ExpiryWriter.RelicWaitingForGate);
        Assert.False(producing.IsCompleted);
        var diagnosticsBeforeCancellation = f.ReadDiagnostics();
        canceledFrame.Cancel();
        await producing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(f.ActualConnection.HasRegisteredRelicWire);
        Assert.Empty(f.Sent);
        var diagnostics = f.ReadDiagnostics();
        Assert.StartsWith(diagnosticsBeforeCancellation, diagnostics, StringComparison.Ordinal);
        // Startup has its own paths in the log; this checks only the bounded
        // committed-cancellation failure emitted by the operation under test.
        var cancellationDiagnostic = diagnostics[diagnosticsBeforeCancellation.Length..];
        Assert.Contains("LegacyCommittedSpellPublicationException", cancellationDiagnostic);
        Assert.Contains("CanceledException", cancellationDiagnostic);
        Assert.DoesNotContain(f.Data.Users, cancellationDiagnostic, StringComparison.OrdinalIgnoreCase);
        try { await filler.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }

        await f.CloseAsync();
        await f.ReopenFrameAsync();
        Assert.Equal(highWater + 1, f.HighWater);
        Assert.Equal(3, f.Current.PlayerItems.RelicFuseItems.Count);
        f.Sent.Clear();
        await f.SendAsync(bytes);
        Assert.Empty(f.Sent);
        Assert.Equal(highWater + 1, f.HighWater);
        f.AssertConverged();
    });
}

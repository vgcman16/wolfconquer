using System.Net.Sockets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedWindWalkerQueryTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task OwnerOrViewerSendLossAfterActualCommitCannotUndoRenewOrReplayTheFan(bool viewerFails) => Run(async f =>
    {
        AssertJoined(f, false); var expected = ExpectedWindEquippedFiles(f); int first = f.Sent.Count;
        int peerFirst = f.Owner.Frames(Peer).Count; var failure = new SocketException((int)SocketError.ConnectionReset);
        LegacyNativeStatusFlagsBigVector32.Flag? committed = null; DateTime timer = default;
        int failed = 0; var attempts = new List<uint>();
        f.BeforeSend = (recipient, bytes) =>
        {
            if (!IsVector(bytes, out _)) return;
            attempts.Add(recipient); Assert.Equal(VectorGolden(Words(true)), bytes);
            AssertJoined(f, true); AssertUnchangedSaved(f, expected);
            if (recipient != (viewerFails ? Peer : Uid)) return;
            Assert.Equal(0, failed++); committed = f.Row!; timer = committed.Timer;
            if (viewerFails) Assert.Equal(VectorGolden(Words(true)), f.Sent[^1]);
            // Failure only, in the real owned connection's actual send route.
            // It supplies no accepting writer, result, permission or timer.
            throw failure;
        };
        try { Assert.Same(failure, await Assert.ThrowsAsync<SocketException>(() => f.EquipAsync())); }
        finally { f.BeforeSend = null; }
        Assert.Equal(1, failed); Assert.NotNull(committed); AssertJoined(f, true);
        Assert.Same(committed, f.Row); Assert.Equal(timer, f.Row!.Timer); f.Owner.Data.AssertFiles(expected);
        Assert.Equal(viewerFails ? new[] { Uid, Peer } : new[] { Uid }, attempts);
        Assert.Equal(viewerFails ? 1 : 0, f.Sent.Skip(first).Count(bytes => IsVector(bytes, out _)));
        Assert.DoesNotContain(f.Owner.Frames(Peer).Skip(peerFirst), bytes => IsVector(bytes, out _));

        first = f.Sent.Count; peerFirst = f.Owner.Frames(Peer).Count;
        // The shared fixture truthfully latches its expected observer exception.
        // Re-enter the SAME real Frame/connection directly for the retry, so that
        // bookkeeping latch cannot stand in for a second transport failure.
        await f.Owner.Handler.HandleFrameAsync(f.Owner.Connection(Uid),
            OwnedEquipmentBehaviorFixture.ReadFrame(LegacyRuneStoragePacket.Startup().ToFrameBytes()));
        Assert.Equal(first + 12, f.Sent.Count); LegacySelectedEquipmentQueryAssertions.NeutralQuery(f.Sent, first + 2, Uid);
        Assert.Same(committed, f.Row); Assert.Equal(timer, f.Row.Timer); AssertJoined(f, true);
        Assert.DoesNotContain(f.Sent.Skip(first), bytes => IsVector(bytes, out _));
        Assert.DoesNotContain(f.Owner.Frames(Peer).Skip(peerFirst), bytes => IsVector(bytes, out _));
        f.Owner.Data.AssertFiles(expected);
    }, peer: true);

    [Fact]
    public Task ActualEofDuringCommittedOwnerVectorRetiresPublicationAndRawRestartsDoNotReplayIt() => Run(async f =>
    {
        AssertJoined(f, false); var expected = ExpectedWindEquippedFiles(f); var native = f.Native;
        var binding = native.OrdinarySpellBinding!; Task? close = null;
        LegacyNativeStatusFlagsBigVector32.Flag? committed = null; DateTime timer = default;
        int first = f.Sent.Count, peerFirst = f.Owner.Frames(Peer).Count;
        f.BeforeSend = (recipient, bytes) =>
        {
            if (recipient != Uid || !IsVector(bytes, out _)) return;
            Assert.Null(close); AssertJoined(f, true); AssertUnchangedSaved(f, expected);
            committed = f.Row!; timer = committed.Timer;
            // The actual TCP-close callback marks Ended synchronously. Its
            // exclusive Store retirement is awaited after the Frame returns.
            close = f.Owner.Connection(Uid).CompleteTransportCloseAsync();
        };
        try { await f.EquipAsync(); } finally { f.BeforeSend = null; }
        Assert.NotNull(close); await close!; Assert.NotNull(committed);
        Assert.Equal(VectorGolden(Words(true)), Assert.Single(f.Sent.Skip(first), bytes => IsVector(bytes, out _)));
        Assert.DoesNotContain(f.Owner.Frames(Peer).Skip(peerFirst), bytes => IsVector(bytes, out _));
        Assert.False(f.Store.TryGet(f.Owner.Connection(Uid), out _)); Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.True(binding.IsRetired); Assert.Same(committed, native.Player!.BitVector.ArrayFlags[Fan]); Assert.Equal(timer, committed!.Timer);
        f.Owner.Data.AssertFiles(expected); int actorFrames = f.Sent.Count, peerFrames = f.Owner.Frames(Peer).Count;
        await f.Owner.Connection(Peer).DisconnectAsync(); await f.Owner.Connection(Peer).CompleteTransportCloseAsync();
        await f.CloseAsync();
        for (int restart = 0; restart < 2; restart++)
        {
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(f.Owner.Data.Loader, f.Owner.Data.Options(), out var opened, out var reason), reason);
            await using var owner = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
            Assert.True(owner.Recovery.IsReadyForCharacterItemsSpells(f.Owner.Data.Users, f.Owner.Data.Items, f.Owner.Data.Spells));
            f.Owner.Data.AssertFiles(expected); Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
            Assert.Equal(actorFrames, f.Sent.Count); Assert.Equal(peerFrames, f.Owner.Frames(Peer).Count);
            Assert.Same(committed, native.Player.BitVector.ArrayFlags[Fan]); Assert.Equal(timer, committed.Timer);
        }
    }, peer: true);
}

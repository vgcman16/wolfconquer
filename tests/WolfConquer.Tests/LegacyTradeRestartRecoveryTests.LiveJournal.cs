using System.Reflection;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyTradeRestartRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegisteredTradeCancellationObservesPendingJournalBeforeStoreRefusal(bool directDelegate)
    {
        var state = await PrepareLiveTrade();
        var pending = PublishLivePending(state);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(state.Left.Connection, state.Owner.Log,
            state.BeforeLeft, Sessions: state.Owner.Sessions, PlayerItemsRoot: ItemsRoot,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
        {
            Trades = ActualOwner<LegacyTradeStateStore>(state.Owner, "_tradeStates"),
            TradeOperations = state.Operations,
            TradeRecovery = state.Recovery,
        };
        var frame = ReadFrame(new LegacyTradePacket(2).ToNativeFrameBytes());

        if (directDelegate)
        {
            Assert.True(registry.TryGetHandler(frame.Header.Opcode, out var handler));
            await handler(context, frame, CancellationToken.None);
        }
        else await registry.HandleAsync(context, frame);

        AssertLivePendingRetained(state, pending);
        await RecoverFreshAndRequireOldFence(state);
    }

    [Fact]
    public async Task QueuedFrameObservesPendingJournalOnlyAfterItsActualLease()
    {
        var state = await PrepareLiveTrade();
        Task queued;
        byte[] pending;
        using (await state.Operations.EnterExclusiveAsync())
        {
            queued = Move(state.Owner, state.Left);
            Assert.False(queued.IsCompleted);
            pending = PublishLivePending(state);
            Assert.False(state.Recovery.IsBlocked);
        }

        await queued.WaitAsync(TimeSpan.FromSeconds(5));

        AssertLivePendingRetained(state, pending);
        await RecoverFreshAndRequireOldFence(state);
    }

    [Fact]
    public async Task DisconnectCleanupFirstObservesPendingJournalAndRetainsEscrow()
    {
        var state = await PrepareLiveTrade();
        var pending = PublishLivePending(state);

        state.Right.Lifetime.Cancel();
        await WaitForRetainedDisconnectCleanup(state.Owner);

        AssertLivePendingRetained(state, pending);
        await RecoverFreshAndRequireOldFence(state);
    }

    private async Task<LivePendingState> PrepareLiveTrade()
    {
        const uint leftUid = 1_296_091;
        const uint rightUid = 1_296_092;
        Directory.CreateDirectory(UsersRoot);
        Directory.CreateDirectory(ItemsRoot);
        Seed(leftUid, 1_000, 50, 10);
        Seed(rightUid, 2_000, 60, 20);
        var owner = NewOwner();
        var left = await Login(owner, leftUid);
        var right = await Login(owner, rightUid);
        await Open(owner, left, right);
        await Trade(owner, left, new LegacyTradePacket(7) { Money = 300 });
        await Trade(owner, right, new LegacyTradePacket(12) { CPS = 10 });
        AssertWallet(leftUid, 700, 50);
        AssertWallet(rightUid, 2_000, 50);
        return new(owner, left, right, Current(owner, left), Current(owner, right), SaveBytes(),
            left.Sent.Count, right.Sent.Count, ActualOwner<LegacyTradeRecoveryState>(owner, "_tradeRecovery"),
            ActualOwner<WorldOperationBarrier>(owner, "_worldOperations"));
    }

    private byte[] PublishLivePending(LivePendingState state)
    {
        var changes = new[] { state.Left.Uid, state.Right.Uid }.SelectMany(uid => new[]
        {
            new LegacyTradeJournalChange(LegacyTradeJournalFileKind.Character, uid,
                state.BeforeFiles[UserPath(uid)], state.BeforeFiles[UserPath(uid)]),
            new LegacyTradeJournalChange(LegacyTradeJournalFileKind.Items, uid,
                state.BeforeFiles[ItemPath(uid)], state.BeforeFiles[ItemPath(uid)]),
        }).ToArray();
        Assert.False(LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, changes, count => count == 0, out var reason));
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason);
        Assert.True(LegacyTradeJournal.HasPending(UsersRoot));
        return File.ReadAllBytes(PendingPath);
    }

    private void AssertLivePendingRetained(LivePendingState state, byte[] pending)
    {
        Assert.True(state.Recovery.IsBlocked);
        Assert.Same(state.BeforeLeft, Current(state.Owner, state.Left));
        Assert.Same(state.BeforeRight, Current(state.Owner, state.Right));
        Assert.True(state.BeforeLeft.LegacyTradeActive);
        Assert.True(state.BeforeRight.LegacyTradeActive);
        AssertWallet(state.Left.Uid, 700, 50);
        AssertWallet(state.Right.Uid, 2_000, 50);
        AssertClaim(state.Left.Uid, 300, 0);
        AssertClaim(state.Right.Uid, 0, 10);
        Assert.Equal(state.LeftFrames, state.Left.Sent.Count);
        Assert.Equal(state.RightFrames, state.Right.Sent.Count);
        AssertSavesUnchanged(state.BeforeFiles);
        Assert.Equal(pending, File.ReadAllBytes(PendingPath));
    }

    private async Task RecoverFreshAndRequireOldFence(LivePendingState state)
    {
        var fresh = NewOwner();
        var left = await Login(fresh, state.Left.Uid);
        var right = await Login(fresh, state.Right.Uid);
        AssertWallet(left.Uid, 1_000, 50);
        AssertWallet(right.Uid, 2_000, 60);
        AssertClearedClaim(left.Uid);
        AssertClearedClaim(right.Uid);
        Assert.False(Current(fresh, left).LegacyTradeActive);
        Assert.False(Current(fresh, right).LegacyTradeActive);
        Assert.Equal(state.BeforeFiles[ItemPath(left.Uid)], File.ReadAllBytes(ItemPath(left.Uid)));
        Assert.Equal(state.BeforeFiles[ItemPath(right.Uid)], File.ReadAllBytes(ItemPath(right.Uid)));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        var recovered = SaveBytes();

        await Move(state.Owner, state.Left);
        await Trade(state.Owner, state.Left, new LegacyTradePacket(2));

        Assert.True(state.Recovery.IsBlocked);
        Assert.Same(state.BeforeLeft, Current(state.Owner, state.Left));
        Assert.Same(state.BeforeRight, Current(state.Owner, state.Right));
        Assert.Equal(state.LeftFrames, state.Left.Sent.Count);
        Assert.Equal(state.RightFrames, state.Right.Sent.Count);
        AssertSavesUnchanged(recovered);
    }

    private static T ActualOwner<T>(Owner owner, string fieldName)
    {
        var field = typeof(WorldServerFrameHandler).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field.GetValue(owner.Handler));
    }

    private sealed record LivePendingState(Owner Owner, Player Left, Player Right,
        WorldSession BeforeLeft, WorldSession BeforeRight, Dictionary<string, byte[]> BeforeFiles,
        int LeftFrames, int RightFrames, LegacyTradeRecoveryState Recovery, WorldOperationBarrier Operations);
}

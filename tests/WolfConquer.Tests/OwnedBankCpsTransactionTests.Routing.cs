using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedBankCpsTransactionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task QueuedConfirmationCannotConsumeAChangedCurrentPendingHolder(bool deposit) =>
        WithBank(async f =>
        {
            await f.Login(); await StageBank(f, deposit, 100);
            var original = f.Current; var files = f.Files(); int first = f.Sent.Count;
            Task queued;
            using (await f.Operations.EnterExclusiveAsync())
            {
                queued = ConfirmBank(f, deposit);
                Assert.False(queued.IsCompleted);
                // Actual Store publication of a new transient confirmation while
                // this fixture owns the real Exclusive. No wallet saver or
                // accepting callback supplies the later confirmation authority.
                f.Store.Set(f.Connection, original with { PendingBankConquerPoints = 150 });
            }
            await queued.WaitAsync(TimeSpan.FromSeconds(5));

            AssertBankTuple(f, 1000, 250, 150, deposit); f.CheckFiles(files);
            AssertNoBankSuccess(f.Sent.Skip(first));
            int retry = f.Sent.Count;
            await ConfirmBank(f, deposit);
            AssertBankTuple(f, deposit ? 850 : 1150, deposit ? 400 : 100);
            AssertBankSuccessFrames(f.Sent.Skip(retry).ToArray(), deposit ? 850 : 1150, deposit ? 400 : 100);
        });

    [Fact]
    public Task QueuedImplicitBankReplyRefusesWhenTheActualActiveNpcChangesBeforeExclusive() =>
        WithBank(async f =>
        {
            await f.Login(); await StageBank(f, true, 100);
            var files = f.Files(); int first = f.Sent.Count; Task queued;
            using (await f.Operations.EnterExclusiveAsync())
            {
                queued = f.Send(new LegacyNpcDialogRequestPacket(0, 0, 2,
                    (byte)LegacyNpcReplyInteractType.Option, 0, "").ToFrameBytes());
                Assert.False(queued.IsCompleted);
                f.Store.Set(f.Connection, f.Current with { ActiveNpcId = 20_499 });
            }
            await queued.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(20_499U, f.Current.ActiveNpcId); AssertBankTuple(f, 1000, 250, 100, deposit: true);
            f.CheckFiles(files); Assert.Equal(first, f.Sent.Count);
            // A fresh explicit bank request is still allowed to confirm the
            // preserved amount after ordinary current-owner checks.
            await ConfirmBank(f, true);
            AssertBankTuple(f, 900, 350); AssertBankSuccessFrames(f.Sent.Skip(first).ToArray(), 900, 350);
        });

    [Fact]
    public Task ActualInvocationReacquiresSharedWhenAnEarlierBankHintNowResolvesToAnotherNpc() =>
        WithBank(async f =>
        {
            await f.Login(); await StageBank(f, true, 100);
            var frame = BankFrame(new LegacyNpcDialogRequestPacket(0, 0, 2,
                (byte)LegacyNpcReplyInteractType.Option, 0, "").ToFrameBytes());
            Assert.True(WorldServerFrameHandler.HandlerOwnsOperation(frame, ConquerProtocolProfile.VisibleDx8D13, BankNpcUid));
            f.Store.Set(f.Connection, f.Current with { ActiveNpcId = 20_499 });
            var current = f.Current; var files = f.Files(); int first = f.Sent.Count;
            Assert.False(WorldServerFrameHandler.HandlerOwnsOperation(frame, ConquerProtocolProfile.VisibleDx8D13, current.ActiveNpcId));
            var catalog = Assert.IsType<WorldDataCatalog>(typeof(WorldServerFrameHandler)
                .GetField("_worldData", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var registry = Assert.IsType<PacketHandlerRegistry<WorldPacketHandlerContext>>(typeof(WorldServerFrameHandler)
                .GetField("_packetHandlers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            // This is the actual invocation state after a bank classification
            // relinquished the outer Frame lease but the subsequently captured
            // session resolves NPC0 elsewhere. The real registered handler and
            // invocation wrapper must reacquire Shared before foreign dispatch.
            var context = new WorldPacketHandlerContext(f.Connection, f.Log, Session: current, Sessions: f.Store,
                WorldData: catalog, PlayerItemsRoot: f.Items, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
            { TradeOperations = f.Operations, TradeRecovery = f.Recovery, Trades = f.Trades };
            Task invocation;
            using (await f.Operations.EnterExclusiveAsync())
            {
                invocation = registry.HandleAsync(context, frame).AsTask();
                Assert.False(invocation.IsCompleted, "Actual foreign NPC invocation must wait for its Shared lease.");
            }
            await invocation.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Same(current, f.Current); AssertBankTuple(f, 1000, 250, 100, deposit: true);
            f.CheckFiles(files); AssertNoBankSuccess(f.Sent.Skip(first));
        });

    [Fact]
    public Task ActualBankInvocationRefusesRetainedFrameSharedAfterANonBankRoutingHintChanges() =>
        WithBank(async f =>
        {
            await f.Login(); await StageBank(f, true, 100);
            var frame = BankFrame(new LegacyNpcDialogRequestPacket(0, 0, 2,
                (byte)LegacyNpcReplyInteractType.Option, 0, "").ToFrameBytes());
            f.Store.Set(f.Connection, f.Current with { ActiveNpcId = 20_499 });
            Assert.False(WorldServerFrameHandler.HandlerOwnsOperation(frame, ConquerProtocolProfile.VisibleDx8D13, f.Current.ActiveNpcId));
            // A different actual publication can change the active NPC while
            // Frame is queued for the Shared selected from that earlier hint.
            f.Store.Set(f.Connection, f.Current with { ActiveNpcId = BankNpcUid });
            var current = f.Current; var files = f.Files(); int first = f.Sent.Count;
            var catalog = Assert.IsType<WorldDataCatalog>(typeof(WorldServerFrameHandler)
                .GetField("_worldData", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var registry = Assert.IsType<PacketHandlerRegistry<WorldPacketHandlerContext>>(typeof(WorldServerFrameHandler)
                .GetField("_packetHandlers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            using (var shared = await f.Operations.EnterSharedAsync())
            {
                var context = new WorldPacketHandlerContext(f.Connection, f.Log, Session: current, Sessions: f.Store,
                    WorldData: catalog, PlayerItemsRoot: f.Items, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
                { TradeOperations = f.Operations, TradeRecovery = f.Recovery, Trades = f.Trades, FrameOperationLease = shared };
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await registry.HandleAsync(context, frame, cancellation.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(6));
            }
            Assert.Same(current, f.Current); AssertBankTuple(f, 1000, 250, 100, deposit: true);
            f.CheckFiles(files); Assert.Equal(first, f.Sent.Count);
            Assert.Contains("bankCpsFrameLeaseRetained", f.Log.ToString(), StringComparison.Ordinal);
        });
}

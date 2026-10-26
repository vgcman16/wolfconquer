using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualRegisteredClaimQueuedBehindWorldWriterRechecksNewDebtAfterItsRealLease(bool direct) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var initial = CapturePackageJoin(fixture, join);
            var context = ActualDebtContext(fixture, join);
            var operations = Assert.IsType<WorldOperationBarrier>(context.TradeOperations);
            IDisposable? held = await operations.EnterExclusiveAsync();
            try
            {
                var packet = new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimMoney, AtomicMailId, []).ToFrameBytes();
                var frame = ReadFrame(packet);
                Assert.True(DebtRegistry.TryGetHandler(frame.Header.Opcode, out var handler));
                var actual = (direct ? handler(context, frame, CancellationToken.None) :
                    DebtRegistry.HandleAsync(context, frame, CancellationToken.None)).AsTask();
                fixture.Calls.Add(actual);
                Assert.False(actual.IsCompleted); // Real writer lease holds the actual registered claim.
                var pending = PrepareDebtIntent(fixture, join, initial);
                var before = CapturePackageJoin(fixture, join);
                held.Dispose();
                held = null;
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ObserveAsync(actual));
                Assert.Contains("Mailbox recovery denied", error.Message);
                AssertDebtRetained(fixture, join, before, pending);
                Assert.False(join.Recovery.IsBlocked);
            }
            finally { held?.Dispose(); }
        });

    [Fact]
    public Task ActualPackageFirstOutputTransportCloseCompletesCleanupWithoutInheritingPacketAdmissionAndRetainsCommittedWork() =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var initial = CapturePackageJoin(fixture, join);
            Task? close = null;
            Dictionary<string, byte[]>? committed = null;
            LegacyItemUidProducerIntentSnapshot? intent = null;
            join.SendObserver = _ =>
            {
                intent = AssertPackageJoinAllocation(fixture, join);
                AssertDebtPackageCommitted(fixture, join, initial, intent);
                committed = fixture.PackageFiles();
                close = join.Connection!.CompleteTransportCloseAsync();
                fixture.Calls.Add(close);
            };
            await join.FrameAsync(PackageJoinRequest(claimAll: true));
            join.SendObserver = null;
            Assert.NotNull(close);
            await fixture.ObserveAsync(close);
            Assert.True(close.IsCompletedSuccessfully);
            Assert.False(join.SessionStore.TryGet(join.Connection!, out _));
            Assert.NotNull(committed);
            AssertPackageJoinFiles(fixture, committed);
            Assert.Equal(initial.FirstFrame + 1, fixture.Sent.Count);
            Assert.DoesNotContain("Trade disconnect cleanup failed", fixture.Log.ToString());
            Assert.NotNull(intent);
            Assert.True(join.Registry.TryReadProducerIntent(join.Domain, intent.Request.OperationId, out var retained, out var reason), reason);
            Assert.Equal(intent.Request, retained!.Request);
            Assert.Equal(intent.Allocation, retained.Allocation);
        });
}

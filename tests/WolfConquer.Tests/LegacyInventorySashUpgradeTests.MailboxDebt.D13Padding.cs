using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public Task ActualD13ContentPaddingReachesRegisteredFrameWithoutSavingAndKeepsCanonicalAck() =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var canonical = new LegacyMailboxModePacket(LegacyMailboxModeType.Content, AtomicMailId, []).ToFrameBytes();
            await join.FrameAsync(LegacyMailboxD13CapturedFrames.Pad(canonical, 8));
            AssertPackageJoinUnchanged(fixture, join, before, extraFrames: 2);
            Assert.Equal(WorldPacketOpcodes.LegacyMailContent, ReadFrame(fixture.Sent[before.FirstFrame]).Header.Opcode);
            Assert.Equal(canonical, fixture.Sent[before.FirstFrame + 1]);
        });

    [Theory]
    [InlineData(0)] // Actual WorldServerFrameHandler predispatch and registered invocation.
    [InlineData(1)] // Actual registry wrapper.
    [InlineData(2)] // Actual registered direct handler wrapper.
    public Task ActualD13ReadOnlyRecoveryAdmitsOnlyTheSamePaddedPackageReplayThroughEveryEntrypoint(int entrypoint) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var initial = CapturePackageJoin(fixture, join);
            var intent = PrepareDebtIntent(fixture, join, initial);
            var producer = ActualDebtFrameOwner<LegacyMailboxPackageProducer>(join, "_mailboxPackages");
            Assert.True(join.SessionStore.TryRemove(join.Connection!, out var retired));
            var candidate = LoadDebtRecoveryCandidate(fixture, Assert.IsType<WorldSession>(retired));
            Assert.True(join.SessionStore.TrySetMailboxRecoveryOwner(join.Connection!, candidate, producer, out _, out var reason), reason);
            var before = CapturePackageJoin(fixture, join);
            var canonical = PackageJoinRequest(claimAll: true);
            var padded = LegacyMailboxD13CapturedFrames.Pad(canonical);
            var context = ActualDebtContext(fixture, join);
            foreach (var direct in new[] { false, true })
            {
                await DebtRegisteredAsync(fixture, context with { ProtocolProfile = ConquerProtocolProfile.RootD11_7899 }, padded, direct);
                AssertDebtRetained(fixture, join, before, intent);
            }
            foreach (var fault in new[] { "padding", "id", "item" })
            {
                var wrong = padded.ToArray();
                if (fault == "padding") wrong[^1] = 1;
                if (fault == "id") BinaryPrimitives.WriteUInt32LittleEndian(wrong.AsSpan(6), 99);
                if (fault == "item") BinaryPrimitives.WriteUInt64LittleEndian(wrong.AsSpan(14), AtomicMailId + 1UL);
                await join.FrameAsync(wrong); AssertDebtRetained(fixture, join, before, intent);
                await DebtRegisteredAsync(fixture, context, wrong, direct: true); AssertDebtRetained(fixture, join, before, intent);
            }
            var started = DateTime.Now;
            if (entrypoint == 0) await join.FrameAsync(padded);
            else await DebtRegisteredAsync(fixture, context, padded, direct: entrypoint == 2);
            AssertDebtPackageCommitted(fixture, join, initial, intent);
            AssertPackageJoinFrames(fixture, join, initial, true, started, DateTime.Now, before.FirstFrame);
            Assert.Equal(canonical, fixture.Sent[^1]); // Outbound padding is explicitly not introduced.
            var committed = fixture.PackageFiles(); var firstReplay = fixture.Sent.Count;
            await join.FrameAsync(padded);
            AssertPackageJoinFiles(fixture, committed);
            AssertPackageJoinReplay(fixture, claimAll: true, firstReplay);
            Assert.Equal(intent.Request.OperationId, AssertPackageJoinAllocation(fixture, join).Request.OperationId);
        });

    [Fact]
    public Task ActualPaddedPackageQueuedBeforeDebtUsesD13AgainAfterItsRealWorldLease() =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var initial = CapturePackageJoin(fixture, join); var context = ActualDebtContext(fixture, join);
            var operations = Assert.IsType<WorldOperationBarrier>(context.TradeOperations);
            IDisposable? held = await operations.EnterExclusiveAsync();
            try
            {
                var padded = LegacyMailboxD13CapturedFrames.Pad(PackageJoinRequest(claimAll: true));
                var frame = ReadFrame(padded);
                var actual = DebtRegistry.HandleAsync(context, frame, CancellationToken.None).AsTask();
                fixture.Calls.Add(actual); Assert.False(actual.IsCompleted);
                var intent = PrepareDebtIntent(fixture, join, initial);
                var before = CapturePackageJoin(fixture, join);
                var started = DateTime.Now;
                held.Dispose(); held = null;
                await fixture.ObserveAsync(actual);
                AssertDebtPackageCommitted(fixture, join, initial, intent);
                AssertPackageJoinFrames(fixture, join, initial, true, started, DateTime.Now, before.FirstFrame);
                Assert.False(join.Recovery.IsBlocked);
            }
            finally { held?.Dispose(); }
        });
}

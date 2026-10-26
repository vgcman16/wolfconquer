using System.Text.Json.Nodes;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyItemUidRegistryTests
{
    [Theory]
    [InlineData(false, (int)LegacyItemUidInterruption.AfterPendingFlush)]
    [InlineData(false, (int)LegacyItemUidInterruption.AfterStateReplace)]
    [InlineData(true, (int)LegacyItemUidInterruption.AfterPendingFlush)]
    [InlineData(true, (int)LegacyItemUidInterruption.AfterStateReplace)]
    public void InterruptedProducerStageRecoversSameIntentAndRangeWithoutAnyLegacyToken(bool allocationStage, int stop)
    {
        using var files = new Fixture();
        var payload = ProducerPayload();
        var request = ProducerRequest(payload);
        Guid domain;
        using (var owner = files.Bootstrap())
        {
            domain = OperationDomain(files);
            if (allocationStage) PrepareProducer(owner, domain, request, payload);
            LegacyItemUidProducerIntentSnapshot? snapshot;
            string reason;
            var success = allocationStage
                ? owner.TryReserveProducerIntent(domain, request, payload, (LegacyItemUidInterruption)stop,
                    out snapshot, out reason)
                : owner.TryPrepareProducerIntent(domain, request, payload, (LegacyItemUidInterruption)stop,
                    out snapshot, out reason);
            Assert.False(success);
            Assert.Null(snapshot);
            Assert.StartsWith(LegacyItemUidRegistry.RecoveryRequired, reason);
            Assert.False(owner.TryReadProducerIntent(domain, request.OperationId, out snapshot, out var blocked));
            Assert.Null(snapshot);
            Assert.Equal(reason, blocked);
            Assert.False(owner.TryReserve(1, out _, out blocked));
            Assert.Equal(reason, blocked);
        }
        using var reopened = files.Open();
        Assert.True(reopened.TryReadProducerIntent(domain, request.OperationId, out var recovered, out var recoveredReason), recoveredReason);
        Assert.Equal(payload, recovered!.CopyPayload());
        Assert.Equal(request, recovered.Request);
        Assert.Equal(1UL, recovered.PreparedGeneration);
        Assert.Equal(allocationStage, recovered.Allocation is not null);
        var allocated = AllocateProducer(reopened, domain, request, payload);
        Assert.Equal(1001U, allocated.Allocation!.First);
        Assert.Equal(2UL, allocated.Allocation.Generation);
        Assert.Equal(3, allocated.Allocation.Count);
        var beforeReplay = File.ReadAllBytes(files.State);
        Assert.Equal(allocated.Allocation, AllocateProducer(reopened, domain, request, payload).Allocation);
        Assert.Equal(beforeReplay, File.ReadAllBytes(files.State));
        Assert.False(File.Exists(files.Pending));
        Assert.Equal(new uint[] { 1004 }, Fixture.Reserve(reopened, 1));
    }

    [Fact]
    public void CrashAfterAllocationBeforeAnyRewardJournalRetainsTheWholeProducerDescription()
    {
        using var files = new Fixture();
        var payload = ProducerPayload();
        var request = ProducerRequest(payload);
        LegacyItemUidReservationReceipt receipt;
        Guid domain;
        using (var owner = files.Bootstrap())
        {
            domain = OperationDomain(files);
            PrepareProducer(owner, domain, request, payload);
            receipt = AllocateProducer(owner, domain, request, payload).Allocation!;
            Assert.False(Directory.Exists(files.SnapshotJournal));
        }
        using var reopened = files.Open();
        Assert.True(reopened.TryReadProducerIntents(domain, out var discovered, out var discoveryReason), discoveryReason);
        Assert.Equal(request, Assert.Single(discovered).Request);
        Assert.True(reopened.TryReadProducerIntent(domain, request.OperationId, out var intent, out var reason), reason);
        Assert.Equal(receipt, intent!.Allocation);
        Assert.Equal(request, intent.Request);
        Assert.Equal(payload, intent.CopyPayload());
        var before = File.ReadAllBytes(files.State);
        Assert.Equal(receipt, AllocateProducer(reopened, domain, intent.Request, intent.CopyPayload()).Allocation);
        Assert.Equal(before, File.ReadAllBytes(files.State));
        Assert.False(Directory.Exists(files.SnapshotJournal));
        Assert.Equal(new uint[] { 1004 }, Fixture.Reserve(reopened, 1));
    }

    [Fact]
    public void ProducerRefusesARealPendingSnapshotJournalAndBlocksItsRegistryOwner()
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap();
        var payload = ProducerPayload();
        var request = ProducerRequest(payload);
        var domain = OperationDomain(files);
        PrepareProducer(owner, domain, request, payload);
        var before = File.ReadAllBytes(files.State);
        // Owned corrupt/pending journal evidence is a negative fixture, never an accepting journal substitute.
        Directory.CreateDirectory(files.SnapshotJournal);
        File.WriteAllText(Path.Combine(files.SnapshotJournal, "pending.json"), "invalid-owned-test-journal");
        Assert.False(owner.TryReserveProducerIntent(domain, request, payload, out var snapshot, out var reason));
        Assert.Null(snapshot);
        Assert.StartsWith(LegacyItemUidRegistry.RecoveryRequired, reason);
        Assert.False(owner.TryReserve(1, out var token, out var blocked));
        Assert.Null(token);
        Assert.Equal(reason, blocked);
        Assert.Equal(before, File.ReadAllBytes(files.State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PendingIntentCannotRewriteAnEarlierDurablePayloadOrAdvanceUidWatermark(bool payloadRewrite)
    {
        using var files = new Fixture();
        var payload = ProducerPayload();
        var first = ProducerRequest(payload);
        using (var owner = files.Bootstrap())
        {
            var domain = OperationDomain(files);
            PrepareProducer(owner, domain, first, payload);
            Assert.False(owner.TryPrepareProducerIntent(domain, ProducerRequest(payload), payload,
                LegacyItemUidInterruption.AfterPendingFlush, out _, out _));
        }
        var pending = JsonNode.Parse(File.ReadAllBytes(files.Pending))!;
        if (payloadRewrite)
        {
            var changedPayload = ProducerPayload("changed-valid-payload");
            pending["ProducerIntents"]![0]!["PayloadBase64"] = Convert.ToBase64String(changedPayload);
            pending["ProducerIntents"]![0]!["PayloadSha256"] = ProducerRequest(changedPayload).PayloadSha256;
        }
        else pending["HighWater"] = 1001U;
        File.WriteAllText(files.Pending, pending.ToJsonString());
        var beforeState = File.ReadAllBytes(files.State);
        var beforePending = File.ReadAllBytes(files.Pending);
        Assert.False(LegacyItemUidRegistry.TryOpen(files.Options(), out var reopened, out var reason));
        Assert.Null(reopened);
        Assert.Equal(LegacyItemUidRegistry.RecoveryRequired + ":pendingChainInvalid", reason);
        Assert.Equal(beforeState, File.ReadAllBytes(files.State));
        Assert.Equal(beforePending, File.ReadAllBytes(files.Pending));
    }

    [Fact]
    public async Task ConcurrentExactProducerRetriesShareOneIntentOneAllocationAndNoToken()
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap();
        var domain = OperationDomain(files);
        var payload = ProducerPayload();
        var request = ProducerRequest(payload);
        var tasks = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            PrepareProducer(owner, domain, request, payload);
            return AllocateProducer(owner, domain, request, payload).Allocation;
        })).ToArray();
        var receipts = await Task.WhenAll(tasks);
        Assert.All(receipts, receipt => Assert.Equal(receipts[0], receipt));
        var state = JsonNode.Parse(File.ReadAllBytes(files.State))!;
        Assert.Single(state["Operations"]!.AsArray());
        Assert.Single(state["ProducerIntents"]!.AsArray());
        Assert.Equal(2UL, state["Generation"]!.GetValue<ulong>());
        Assert.Equal(new uint[] { 1004 }, Fixture.Reserve(owner, 1));
    }
}

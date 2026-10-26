using System.Text.Json.Nodes;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyItemUidRegistryTests
{
    [Theory]
    [InlineData((int)LegacyItemUidInterruption.AfterPendingFlush, false)]
    [InlineData((int)LegacyItemUidInterruption.AfterStateReplace, false)]
    [InlineData((int)LegacyItemUidInterruption.AfterPendingFlush, true)]
    [InlineData((int)LegacyItemUidInterruption.AfterStateReplace, true)]
    public void InterruptedOperationRecoversReceiptWithoutReissuingToken(int interruptionValue, bool priorOperation)
    {
        var interruption = (LegacyItemUidInterruption)interruptionValue;
        using var files = new Fixture();
        var request = OperationRequest();
        Guid domain;
        using (var owner = files.Bootstrap())
        {
            domain = OperationDomain(files);
            if (priorOperation) ReserveOperation(owner, domain, OperationRequest(2));
            Assert.Equal(LegacyItemUidReservationOutcome.Rejected,
                owner.ReserveForOperation(domain, request, interruption, out var token, out var receipt, out var reason));
            Assert.Null(token);
            Assert.Null(receipt);
            Assert.StartsWith(LegacyItemUidRegistry.RecoveryRequired, reason);
            Assert.False(owner.TryReserve(1, out token, out var blocked));
            Assert.Null(token);
            Assert.Equal(reason, blocked);
            Assert.Equal(LegacyItemUidReservationOutcome.Rejected,
                owner.ReserveForOperation(domain, request, out token, out receipt, out blocked));
            Assert.Null(token);
            Assert.Null(receipt);
            Assert.Equal(reason, blocked);
        }
        using var reopened = files.Open();
        var recorded = RecordedOperation(reopened, domain, request);
        Assert.Equal(priorOperation ? 1003U : 1001U, recorded.First);
        Assert.Equal(3, recorded.Count);
        Assert.Equal(priorOperation ? 2UL : 1UL, recorded.Generation);
        Assert.False(File.Exists(files.Pending));
        Assert.Equal(new[] { priorOperation ? 1006U : 1004U }, Fixture.Reserve(reopened, 1));
    }

    [Theory]
    [InlineData((int)LegacyItemUidInterruption.AfterPendingFlush)]
    [InlineData((int)LegacyItemUidInterruption.AfterStateReplace)]
    public void OrdinaryRangeRecoveryPreservesVersionTwoOperationReceipts(int interruptionValue)
    {
        using var files = new Fixture();
        var request = OperationRequest();
        Guid domain;
        using (var owner = files.Bootstrap())
        {
            domain = OperationDomain(files);
            ReserveOperation(owner, domain, request);
            Assert.False(owner.TryReserve(2, (LegacyItemUidInterruption)interruptionValue, out var token, out var reason));
            Assert.Null(token);
            Assert.StartsWith(LegacyItemUidRegistry.RecoveryRequired, reason);
        }
        using var reopened = files.Open();
        Assert.Equal((uint)1001, RecordedOperation(reopened, domain, request).First);
        Assert.Equal(new uint[] { 1006 }, Fixture.Reserve(reopened, 1));
        Assert.False(File.Exists(files.Pending));
    }

    [Fact]
    public void DuplicatePendingReceiptStateUsesStructuralReadbackAndRetainsAllReceipts()
    {
        using var files = new Fixture();
        var first = OperationRequest();
        var second = OperationRequest(2);
        Guid domain;
        using (var owner = files.Bootstrap())
        {
            domain = OperationDomain(files);
            ReserveOperation(owner, domain, first);
            ReserveOperation(owner, domain, second);
        }
        File.Copy(files.State, files.Pending);
        var state = File.ReadAllBytes(files.State);
        using var reopened = files.Open();
        Assert.Equal((uint)1001, RecordedOperation(reopened, domain, first).First);
        Assert.Equal((uint)1004, RecordedOperation(reopened, domain, second).First);
        Assert.Equal(state, File.ReadAllBytes(files.State));
        Assert.False(File.Exists(files.Pending));
        Assert.Equal(new uint[] { 1006 }, Fixture.Reserve(reopened, 1));
    }

    [Fact]
    public void PendingOperationCannotRewritePreviouslyRecordedRequest()
    {
        using var files = new Fixture();
        var first = OperationRequest();
        using (var owner = files.Bootstrap())
        {
            var domain = OperationDomain(files);
            ReserveOperation(owner, domain, first);
            Assert.Equal(LegacyItemUidReservationOutcome.Rejected,
                owner.ReserveForOperation(domain, OperationRequest(2), LegacyItemUidInterruption.AfterPendingFlush,
                    out _, out _, out _));
        }
        var pending = JsonNode.Parse(File.ReadAllBytes(files.Pending))!;
        pending["Operations"]![0]!["PayloadSha256"] = new string('b', 64);
        File.WriteAllText(files.Pending, pending.ToJsonString());
        var stateBytes = File.ReadAllBytes(files.State);
        var pendingBytes = File.ReadAllBytes(files.Pending);
        Assert.False(LegacyItemUidRegistry.TryOpen(files.Options(), out var reopened, out var reason));
        Assert.Null(reopened);
        Assert.Equal(LegacyItemUidRegistry.RecoveryRequired + ":pendingChainInvalid", reason);
        Assert.Equal(stateBytes, File.ReadAllBytes(files.State));
        Assert.Equal(pendingBytes, File.ReadAllBytes(files.Pending));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiveOwnerDoesNotIssueReceiptOrTokenAfterDurableStateChanges(bool replay)
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap();
        var domain = OperationDomain(files);
        var request = OperationRequest();
        ReserveOperation(owner, domain, request);
        var changed = JsonNode.Parse(File.ReadAllBytes(files.State))!;
        changed["Operations"]![0]!["Writer"] = "other-producer";
        File.WriteAllText(files.State, changed.ToJsonString());
        var bytes = File.ReadAllBytes(files.State);
        Assert.Equal(LegacyItemUidReservationOutcome.Rejected,
            owner.ReserveForOperation(domain, replay ? request : OperationRequest(),
                out var token, out var receipt, out var reason));
        Assert.Null(token);
        Assert.Null(receipt);
        Assert.StartsWith(LegacyItemUidRegistry.RecoveryRequired, reason);
        Assert.Equal(bytes, File.ReadAllBytes(files.State));
        Assert.False(File.Exists(files.Pending));
        Assert.False(owner.TryReserve(1, out token, out _));
        Assert.Null(token);
    }

    [Fact]
    public void ExhaustedUidDomainStillReportsExistingReceiptWithoutReallocation()
    {
        using var files = new Fixture();
        var request = OperationRequest(1);
        Guid domain;
        LegacyItemUidReservationReceipt first;
        using (var owner = files.Bootstrap(uint.MaxValue - 1))
        {
            domain = OperationDomain(files);
            var reserved = ReserveOperation(owner, domain, request);
            first = reserved.Receipt;
            Assert.True(reserved.Token.TryTake(out var ids));
            Assert.Equal(new[] { uint.MaxValue }, ids);
        }
        using var reopened = files.Open(uint.MaxValue - 1);
        Assert.Equal(first, RecordedOperation(reopened, domain, request));
        var bytes = File.ReadAllBytes(files.State);
        Assert.Equal(LegacyItemUidReservationOutcome.Rejected,
            reopened.ReserveForOperation(domain, OperationRequest(1), out var token, out var receipt, out var reason));
        Assert.Null(token);
        Assert.Null(receipt);
        Assert.Equal("itemUidNamespaceExhausted", reason);
        Assert.Equal(bytes, File.ReadAllBytes(files.State));
        Assert.False(File.Exists(files.Pending));
    }

    [Fact]
    public async Task ConcurrentSameOperationCreatesOneDurableRangeAndOneToken()
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap();
        var domain = OperationDomain(files);
        var request = OperationRequest();
        var tasks = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            var outcome = owner.ReserveForOperation(domain, request, out var token, out var receipt, out var reason);
            Assert.Equal(string.Empty, reason);
            return (Outcome: outcome, Token: token, Receipt: receipt);
        })).ToArray();
        var results = await Task.WhenAll(tasks);
        var created = Assert.Single(results, row => row.Outcome == LegacyItemUidReservationOutcome.Reserved);
        Assert.Equal(15, results.Count(row => row.Outcome == LegacyItemUidReservationOutcome.AlreadyRecorded));
        Assert.All(results, row => Assert.Equal(created.Receipt, row.Receipt));
        Assert.All(results.Where(row => row.Outcome == LegacyItemUidReservationOutcome.AlreadyRecorded),
            row => Assert.Null(row.Token));
        Assert.NotNull(created.Token);
        Assert.True(created.Token.TryTake(out var ids));
        Assert.Equal(new uint[] { 1001, 1002, 1003 }, ids);
        Assert.False(created.Token.TryTake(out _));
        Assert.Equal(new uint[] { 1004 }, Fixture.Reserve(owner, 1));
    }

    [Fact]
    public async Task ConcurrentDifferentOperationsShareTheActualRegistryGate()
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap();
        var domain = OperationDomain(files);
        var tasks = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            var result = ReserveOperation(owner, domain, OperationRequest(3));
            Assert.True(result.Token.TryTake(out var ids));
            return ids;
        })).ToArray();
        var all = (await Task.WhenAll(tasks)).SelectMany(ids => ids).Order().ToArray();
        Assert.Equal(Enumerable.Range(1001, 48).Select(value => (uint)value), all);
        Assert.Equal(new uint[] { 1049 }, Fixture.Reserve(owner, 1));
    }
}

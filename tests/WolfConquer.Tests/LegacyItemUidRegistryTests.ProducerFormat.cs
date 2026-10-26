using System.Text.Json.Nodes;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyItemUidRegistryTests
{
    [Theory]
    [InlineData("unknown")]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("duplicateProperty")]
    [InlineData("duplicateIdentity")]
    [InlineData("base64")]
    [InlineData("noncanonicalBase64")]
    [InlineData("digest")]
    [InlineData("generation")]
    [InlineData("allocationBinding")]
    [InlineData("generationCollision")]
    [InlineData("downgrade")]
    public void CorruptProducerStateCannotPublishPartialLedgerOrRewriteEvidence(string corruption)
    {
        using var files = new Fixture();
        var payload = ProducerPayload();
        var request = ProducerRequest(payload);
        using (var owner = files.Bootstrap())
        {
            var domain = OperationDomain(files);
            PrepareProducer(owner, domain, request, payload);
            AllocateProducer(owner, domain, request, payload);
        }
        var state = JsonNode.Parse(File.ReadAllBytes(files.State))!;
        var intents = state["ProducerIntents"]!.AsArray();
        var first = intents[0]!;
        switch (corruption)
        {
            case "unknown": first["Accept"] = true; break;
            case "missing": state.AsObject().Remove("ProducerIntents"); break;
            case "null": intents[0] = null; break;
            case "duplicateIdentity": intents.Add(first.DeepClone()); break;
            case "base64": first["PayloadBase64"] = "****"; break;
            case "noncanonicalBase64": first["PayloadBase64"] = first["PayloadBase64"]!.GetValue<string>() + "\n"; break;
            case "digest": first["PayloadSha256"] = new string('b', 64); break;
            case "generation": first["PreparedGeneration"] = 0; break;
            case "allocationBinding": first["Count"] = 4; break;
            case "generationCollision": first["PreparedGeneration"] = 2; break;
            case "downgrade": state["Version"] = 2; break;
            case "duplicateProperty": break;
            default: throw new ArgumentOutOfRangeException(nameof(corruption));
        }
        var text = state.ToJsonString();
        if (corruption == "duplicateProperty")
        {
            var original = first.ToJsonString();
            var duplicate = original.Replace("\"Writer\":\"mail-package-rewards\"",
                "\"Writer\":\"mail-package-rewards\",\"Writer\":\"mail-package-rewards\"", StringComparison.Ordinal);
            text = text.Replace(original, duplicate, StringComparison.Ordinal);
        }
        File.WriteAllText(files.State, text);
        var before = File.ReadAllBytes(files.State);
        Assert.False(LegacyItemUidRegistry.TryOpen(files.Options(), out var ownerAfter, out var reason));
        Assert.Null(ownerAfter);
        Assert.StartsWith(LegacyItemUidRegistry.RecoveryRequired, reason);
        Assert.Equal(before, File.ReadAllBytes(files.State));
        Assert.False(File.Exists(files.Pending));
        Assert.False(LegacyItemUidRegistry.TryBootstrap(files.Options(), out var reset, out _));
        Assert.Null(reset);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProducerLedgerBoundsRejectFreshIntentsButRetainExactReconciliation(bool aggregateBudget)
    {
        using var files = new Fixture();
        var payload = aggregateBudget ? new byte[LegacyItemUidRegistry.MaximumProducerPayloadBytes] : ProducerPayload();
        var request = ProducerRequest(payload);
        Guid domain;
        using (var owner = files.Bootstrap())
        {
            domain = OperationDomain(files);
            PrepareProducer(owner, domain, request, payload);
        }
        // Synthetic codec rows only; no reward execution or grant claim.
        var state = JsonNode.Parse(File.ReadAllBytes(files.State))!;
        var original = state["ProducerIntents"]![0]!.DeepClone();
        var count = aggregateBudget ? LegacyItemUidRegistry.MaximumTotalProducerPayloadBytes / payload.Length :
            LegacyItemUidRegistry.MaximumProducerIntents;
        var intents = new JsonArray();
        for (var index = 0; index < count; index++)
        {
            var row = original.DeepClone();
            row["OperationId"] = index == 0 ? request.OperationId.ToString("N") : Guid.NewGuid().ToString("N");
            row["PreparedGeneration"] = (ulong)index + 1;
            intents.Add(row);
        }
        state["ProducerIntents"] = intents;
        state["Generation"] = (ulong)count;
        File.WriteAllText(files.State, state.ToJsonString());
        var before = File.ReadAllBytes(files.State);
        using var reopened = files.Open();
        Assert.Null(PrepareProducer(reopened, domain, request, payload).Allocation);
        Assert.False(reopened.TryPrepareProducerIntent(domain, ProducerRequest(payload), payload, out var snapshot, out var reason));
        Assert.Null(snapshot);
        Assert.Equal("itemUidProducerIntentLedgerFull", reason);
        Assert.Equal(before, File.ReadAllBytes(files.State));
        Assert.False(File.Exists(files.Pending));
        Assert.True(reopened.TryReadProducerIntents(domain, out var discovered, out reason), reason);
        Assert.Equal(count, discovered.Length);
    }

    [Fact]
    public void ExhaustedUidNamespaceKeepsPreparedIntentWithoutCreatingAnAllocation()
    {
        using var files = new Fixture();
        var payload = ProducerPayload();
        var request = ProducerRequest(payload);
        Guid domain;
        using (var owner = files.Bootstrap(uint.MaxValue))
        {
            domain = OperationDomain(files);
            PrepareProducer(owner, domain, request, payload);
            var before = File.ReadAllBytes(files.State);
            Assert.False(owner.TryReserveProducerIntent(domain, request, payload, out var snapshot, out var reason));
            Assert.Null(snapshot);
            Assert.Equal("itemUidNamespaceExhausted", reason);
            Assert.Equal(before, File.ReadAllBytes(files.State));
        }
        using var reopened = files.Open(uint.MaxValue);
        Assert.True(reopened.TryReadProducerIntent(domain, request.OperationId, out var recovered, out var failure), failure);
        Assert.Null(recovered!.Allocation);
        Assert.Equal(payload, recovered.CopyPayload());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DurableProducerStateDriftBlocksBothReadAndReserveWithoutDirtyOutputs(bool allocate)
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap();
        var domain = OperationDomain(files);
        var payload = ProducerPayload();
        var request = ProducerRequest(payload);
        PrepareProducer(owner, domain, request, payload);
        if (allocate) AllocateProducer(owner, domain, request, payload);
        var state = JsonNode.Parse(File.ReadAllBytes(files.State))!;
        state["HistoricalEvidence"] = "changed-owned-negative-fixture";
        File.WriteAllText(files.State, state.ToJsonString());
        var before = File.ReadAllBytes(files.State);
        Assert.False(owner.TryReadProducerIntents(domain, out var snapshots, out var reason));
        Assert.Empty(snapshots);
        Assert.StartsWith(LegacyItemUidRegistry.RecoveryRequired, reason);
        Assert.False(owner.TryReserveProducerIntent(domain, request, payload, out var snapshot, out var blocked));
        Assert.Null(snapshot);
        Assert.Equal(reason, blocked);
        Assert.Equal(before, File.ReadAllBytes(files.State));
        Assert.False(File.Exists(files.Pending));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GenerationExhaustionKeepsProducerEvidenceAndCannotCreateAnotherStage(bool allocated)
    {
        using var files = new Fixture();
        var payload = ProducerPayload();
        var request = ProducerRequest(payload);
        Guid domain;
        using (var owner = files.Bootstrap())
        {
            domain = OperationDomain(files);
            PrepareProducer(owner, domain, request, payload);
            if (allocated) AllocateProducer(owner, domain, request, payload);
        }
        var state = JsonNode.Parse(File.ReadAllBytes(files.State))!;
        state["Generation"] = ulong.MaxValue;
        File.WriteAllText(files.State, state.ToJsonString());
        var before = File.ReadAllBytes(files.State);
        using var reopened = files.Open();
        Assert.Equal(allocated, PrepareProducer(reopened, domain, request, payload).Allocation is not null);
        Assert.False(reopened.TryPrepareProducerIntent(domain, ProducerRequest(payload), payload, out var snapshot, out var reason));
        Assert.Null(snapshot);
        Assert.Equal("itemUidNamespaceExhausted", reason);
        if (allocated) Assert.NotNull(AllocateProducer(reopened, domain, request, payload).Allocation);
        else
        {
            Assert.False(reopened.TryReserveProducerIntent(domain, request, payload, out snapshot, out reason));
            Assert.Null(snapshot);
            Assert.Equal("itemUidNamespaceExhausted", reason);
        }
        Assert.Equal(before, File.ReadAllBytes(files.State));
    }

    [Fact]
    public void FullOperationLedgerKeepsPreparedProducerIntentAndExistingAllocation()
    {
        using var files = new Fixture();
        var payload = ProducerPayload();
        var request = ProducerRequest(payload, 1);
        Guid domain;
        using (var owner = files.Bootstrap())
        {
            domain = OperationDomain(files);
            PrepareProducer(owner, domain, request, payload);
            AllocateProducer(owner, domain, request, payload);
        }
        // Explicit synthetic complete codec fixture, not executed rewards.
        var state = JsonNode.Parse(File.ReadAllBytes(files.State))!;
        var original = state["Operations"]![0]!.DeepClone();
        var operations = new JsonArray();
        for (var index = 0; index < LegacyItemUidRegistry.MaximumOperationReceipts; index++)
        {
            var row = original.DeepClone();
            row["OperationId"] = index == 0 ? request.OperationId.ToString("N") : Guid.NewGuid().ToString("N");
            row["Generation"] = (ulong)index + 2;
            row["First"] = 1001U + (uint)index;
            operations.Add(row);
        }
        state["Operations"] = operations;
        state["Generation"] = (ulong)LegacyItemUidRegistry.MaximumOperationReceipts + 1;
        state["HighWater"] = 1000U + (uint)LegacyItemUidRegistry.MaximumOperationReceipts;
        File.WriteAllText(files.State, state.ToJsonString());
        using var reopened = files.Open();
        var next = ProducerRequest(payload, 1);
        PrepareProducer(reopened, domain, next, payload);
        var before = File.ReadAllBytes(files.State);
        Assert.False(reopened.TryReserveProducerIntent(domain, next, payload, out var snapshot, out var reason));
        Assert.Null(snapshot);
        Assert.Equal("itemUidOperationLedgerFull", reason);
        Assert.Equal(1001U, AllocateProducer(reopened, domain, request, payload).Allocation!.First);
        Assert.Equal(before, File.ReadAllBytes(files.State));
    }
}

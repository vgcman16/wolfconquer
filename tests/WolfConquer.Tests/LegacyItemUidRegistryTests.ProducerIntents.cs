using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyItemUidRegistryTests
{
    private static byte[] ProducerPayload(string value = "owned-mail-operation:11:12:99:package6:revision1") =>
        Encoding.UTF8.GetBytes(value);

    private static LegacyItemUidReservationRequest ProducerRequest(byte[] payload, int count = 3) =>
        new(Guid.NewGuid(), "mail-package-rewards", 11, 12,
            Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(), count);

    private static LegacyItemUidProducerIntentSnapshot PrepareProducer(LegacyItemUidRegistry owner, Guid domain,
        LegacyItemUidReservationRequest request, byte[] payload)
    {
        Assert.True(owner.TryPrepareProducerIntent(domain, request, payload, out var snapshot, out var reason), reason);
        Assert.Equal(string.Empty, reason);
        return Assert.IsType<LegacyItemUidProducerIntentSnapshot>(snapshot);
    }

    private static LegacyItemUidProducerIntentSnapshot AllocateProducer(LegacyItemUidRegistry owner, Guid domain,
        LegacyItemUidReservationRequest request, byte[] payload)
    {
        Assert.True(owner.TryReserveProducerIntent(domain, request, payload, out var snapshot, out var reason), reason);
        Assert.Equal(string.Empty, reason);
        Assert.NotNull(snapshot?.Allocation);
        return Assert.IsType<LegacyItemUidProducerIntentSnapshot>(snapshot);
    }

    [Fact]
    public void ProducerIntentIsDurableBeforeAnyUidIsReservedAndPayloadIsDetached()
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap();
        var domain = OperationDomain(files);
        var payload = ProducerPayload();
        var original = payload.ToArray();
        var request = ProducerRequest(payload);
        var prepared = PrepareProducer(owner, domain, request, payload);
        Assert.Equal(domain, prepared.DomainId);
        Assert.Equal(request, prepared.Request);
        Assert.Equal(1UL, prepared.PreparedGeneration);
        Assert.Null(prepared.Allocation);
        var json = JsonNode.Parse(File.ReadAllBytes(files.State))!;
        Assert.Equal(3, json["Version"]!.GetValue<int>());
        Assert.Equal(1000U, json["HighWater"]!.GetValue<uint>());
        Assert.Empty(json["Operations"]!.AsArray());
        Assert.Single(json["ProducerIntents"]!.AsArray());
        payload[0] ^= 1;
        var returned = prepared.CopyPayload();
        returned[0] ^= 1;
        Assert.Equal(original, prepared.CopyPayload());
        Assert.Equal(new uint[] { 1001 }, Fixture.Reserve(owner, 1));
        var allocation = AllocateProducer(owner, domain, request, original).Allocation!;
        Assert.Equal(1002U, allocation.First);
        Assert.Equal(3, allocation.Count);
        Assert.Equal(3UL, allocation.Generation);
        Assert.False(File.Exists(files.Pending));
    }

    [Fact]
    public void ProducerAllocationRequiresAnEarlierExactDurableIntent()
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap();
        var before = File.ReadAllBytes(files.State);
        var payload = ProducerPayload();
        Assert.False(owner.TryReserveProducerIntent(OperationDomain(files), ProducerRequest(payload), payload,
            out var snapshot, out var reason));
        Assert.Null(snapshot);
        Assert.Equal("itemUidProducerIntentMissing", reason);
        Assert.Equal(before, File.ReadAllBytes(files.State));
        Assert.False(File.Exists(files.Pending));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProducerIntentCannotAdoptAnEarlierLegacyTokenOrReissueIt(bool consume)
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap();
        var domain = OperationDomain(files);
        var payload = ProducerPayload();
        var request = ProducerRequest(payload);
        var reserved = ReserveOperation(owner, domain, request);
        if (consume) Assert.True(reserved.Token.TryTake(out _));
        var before = File.ReadAllBytes(files.State);
        Assert.False(owner.TryPrepareProducerIntent(domain, request, payload, out var snapshot, out var reason));
        Assert.Null(snapshot);
        Assert.Equal("itemUidProducerIntentAfterReservation", reason);
        Assert.Equal(before, File.ReadAllBytes(files.State));
        Assert.Equal(reserved.Receipt, RecordedOperation(owner, domain, request));
        if (!consume) Assert.True(reserved.Token.TryTake(out _));
        Assert.False(reserved.Token.TryTake(out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProducerOwnedOperationNeverIssuesALegacyMaterializationToken(bool allocate)
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap();
        var domain = OperationDomain(files);
        var payload = ProducerPayload();
        var request = ProducerRequest(payload);
        PrepareProducer(owner, domain, request, payload);
        if (allocate) AllocateProducer(owner, domain, request, payload);
        var before = File.ReadAllBytes(files.State);
        Assert.Equal(LegacyItemUidReservationOutcome.Rejected,
            owner.ReserveForOperation(domain, request, out var token, out var receipt, out var reason));
        Assert.Null(token);
        Assert.Null(receipt);
        Assert.Equal("itemUidProducerIntentOwnsOperation", reason);
        Assert.Equal(before, File.ReadAllBytes(files.State));
    }

    [Fact]
    public void UnrelatedLegacyOperationsKeepTheirTokensAndDoNotDowngradeVersionThree()
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap();
        var domain = OperationDomain(files);
        var payload = ProducerPayload();
        var request = ProducerRequest(payload);
        PrepareProducer(owner, domain, request, payload);
        var ordinary = ReserveOperation(owner, domain, OperationRequest(1));
        Assert.True(ordinary.Token.TryTake(out var ids));
        Assert.Equal(new uint[] { 1001 }, ids);
        Assert.Equal(3, JsonNode.Parse(File.ReadAllBytes(files.State))!["Version"]!.GetValue<int>());
        Assert.Equal(1002U, AllocateProducer(owner, domain, request, payload).Allocation!.First);
    }

    [Theory]
    [InlineData("writer")]
    [InlineData("account")]
    [InlineData("character")]
    [InlineData("count")]
    [InlineData("payload")]
    [InlineData("domain")]
    public void ProducerConflictingReplayCannotAdvanceOrReplaceIntent(string change)
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap();
        var domain = OperationDomain(files);
        var payload = ProducerPayload();
        var request = ProducerRequest(payload);
        PrepareProducer(owner, domain, request, payload);
        var allocation = AllocateProducer(owner, domain, request, payload).Allocation;
        var before = File.ReadAllBytes(files.State);
        var changed = change switch
        {
            "writer" => request with { Writer = "another-producer" },
            "account" => request with { AccountId = 21 },
            "character" => request with { CharacterUid = 22 },
            "count" => request with { Count = 4 },
            "payload" => request with { PayloadSha256 = ProducerRequest(ProducerPayload("changed")).PayloadSha256 },
            _ => request,
        };
        var sentPayload = change == "payload" ? ProducerPayload("changed") : payload;
        var sentDomain = change == "domain" ? Guid.NewGuid() : domain;
        Assert.False(owner.TryPrepareProducerIntent(sentDomain, changed, sentPayload, out var snapshot, out _));
        Assert.Null(snapshot);
        Assert.False(owner.TryReserveProducerIntent(sentDomain, changed, sentPayload, out snapshot, out _));
        Assert.Null(snapshot);
        Assert.Equal(before, File.ReadAllBytes(files.State));
        Assert.Equal(allocation, AllocateProducer(owner, domain, request, payload).Allocation);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("oversize")]
    [InlineData("null")]
    [InlineData("digest")]
    [InlineData("operation")]
    [InlineData("count")]
    [InlineData("account")]
    [InlineData("character")]
    [InlineData("interruption")]
    public void InvalidProducerInputDoesNotUpgradeOrReserve(string invalid)
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap();
        var domain = OperationDomain(files);
        byte[] payload = invalid switch
        {
            "empty" => [],
            "oversize" => new byte[LegacyItemUidRegistry.MaximumProducerPayloadBytes + 1],
            _ => ProducerPayload(),
        };
        var request = ProducerRequest(payload);
        request = invalid switch
        {
            "digest" => request with { PayloadSha256 = new string('a', 64) },
            "operation" => request with { OperationId = Guid.Empty },
            "count" => request with { Count = 0 },
            "account" => request with { AccountId = 0 },
            "character" => request with { CharacterUid = 0 },
            _ => request,
        };
        var before = File.ReadAllBytes(files.State);
        Assert.False(owner.TryPrepareProducerIntent(domain, request, invalid == "null" ? null! : payload,
            invalid == "interruption" ? (LegacyItemUidInterruption)99 : LegacyItemUidInterruption.None,
            out var snapshot, out var reason));
        Assert.Null(snapshot);
        Assert.NotEmpty(reason);
        Assert.Equal(before, File.ReadAllBytes(files.State));
        Assert.False(File.Exists(files.Pending));
        Assert.Equal(new uint[] { 1001 }, Fixture.Reserve(owner, 1));
    }
}

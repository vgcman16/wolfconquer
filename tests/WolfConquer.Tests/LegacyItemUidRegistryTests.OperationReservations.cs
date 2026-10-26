using System.Text.Json.Nodes;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyItemUidRegistryTests
{
    private static LegacyItemUidReservationRequest OperationRequest(int count = 3) =>
        new(Guid.NewGuid(), "package-rewards", 11, 12, new string('a', 64), count);

    private static Guid OperationDomain(Fixture files) =>
        Guid.ParseExact(JsonNode.Parse(File.ReadAllBytes(files.State))!["Domain"]!.GetValue<string>(), "N");

    private static (LegacyItemUidRegistry.Reservation Token, LegacyItemUidReservationReceipt Receipt)
        ReserveOperation(LegacyItemUidRegistry owner, Guid domain, LegacyItemUidReservationRequest request)
    {
        Assert.Equal(LegacyItemUidReservationOutcome.Reserved,
            owner.ReserveForOperation(domain, request, out var token, out var receipt, out var reason));
        Assert.Equal(string.Empty, reason);
        return (Assert.IsType<LegacyItemUidRegistry.Reservation>(token),
            Assert.IsType<LegacyItemUidReservationReceipt>(receipt));
    }

    private static LegacyItemUidReservationReceipt RecordedOperation(LegacyItemUidRegistry owner,
        Guid domain, LegacyItemUidReservationRequest request)
    {
        Assert.Equal(LegacyItemUidReservationOutcome.AlreadyRecorded,
            owner.ReserveForOperation(domain, request, out var token, out var receipt, out var reason));
        Assert.Null(token);
        Assert.Equal(string.Empty, reason);
        return Assert.IsType<LegacyItemUidReservationReceipt>(receipt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OperationReplayNeverCreatesAnotherMaterializationToken(bool consumeOriginal)
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap();
        var domain = OperationDomain(files);
        var request = OperationRequest();
        var (token, receipt) = ReserveOperation(owner, domain, request);
        Assert.Equal(new LegacyItemUidReservationReceipt(domain, request.OperationId, request.Writer,
            11, 12, request.PayloadSha256, 1, 1001, 3), receipt);
        var durable = File.ReadAllBytes(files.State);
        Assert.Equal(2, JsonNode.Parse(durable)!["Version"]!.GetValue<int>());
        if (consumeOriginal)
        {
            Assert.True(token.TryTake(out var ids));
            Assert.Equal(new uint[] { 1001, 1002, 1003 }, ids);
        }
        Assert.Equal(receipt, RecordedOperation(owner, domain, request));
        Assert.Equal(durable, File.ReadAllBytes(files.State));
        Assert.False(File.Exists(files.Pending));
        if (!consumeOriginal)
        {
            Assert.True(token.TryTake(out var ids));
            Assert.Equal(new uint[] { 1001, 1002, 1003 }, ids);
        }
        Assert.False(token.TryTake(out var replay));
        Assert.Empty(replay);
        Assert.Equal(new uint[] { 1004 }, Fixture.Reserve(owner, 1));
    }

    [Fact]
    public void OperationReplaySurvivesOwnerRestartAndRetainsExactDomainBinding()
    {
        using var files = new Fixture();
        var request = OperationRequest();
        LegacyItemUidReservationReceipt first;
        Guid domain;
        using (var owner = files.Bootstrap())
        {
            domain = OperationDomain(files);
            first = ReserveOperation(owner, domain, request).Receipt;
            Assert.False(LegacyItemUidRegistry.TryOpen(files.Options(), out var second, out _));
            Assert.Null(second);
        }
        using var reopened = files.Open();
        Assert.Equal(first, RecordedOperation(reopened, domain, request));
        var snapshot = File.ReadAllBytes(files.State);
        Assert.Equal(LegacyItemUidReservationOutcome.Rejected,
            reopened.ReserveForOperation(Guid.NewGuid(), request, out var token, out var receipt, out var reason));
        Assert.Null(token);
        Assert.Null(receipt);
        Assert.Equal("itemUidDomainMismatch", reason);
        Assert.Equal(snapshot, File.ReadAllBytes(files.State));
        var other = ReserveOperation(reopened, domain, request with { OperationId = Guid.NewGuid() });
        Assert.Equal((uint)1004, other.Receipt.First);
        Assert.Equal((ulong)2, other.Receipt.Generation);
    }

    [Theory]
    [InlineData("writer")]
    [InlineData("account")]
    [InlineData("character")]
    [InlineData("payload")]
    [InlineData("count")]
    public void RecordedOperationRejectsDifferentRequestWithoutAllocating(string change)
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap();
        var domain = OperationDomain(files);
        var request = OperationRequest();
        var first = ReserveOperation(owner, domain, request).Receipt;
        var snapshot = File.ReadAllBytes(files.State);
        var conflict = change switch
        {
            "writer" => request with { Writer = "other-producer" },
            "account" => request with { AccountId = 13 },
            "character" => request with { CharacterUid = 14 },
            "payload" => request with { PayloadSha256 = new string('b', 64) },
            "count" => request with { Count = 4 },
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };
        Assert.Equal(LegacyItemUidReservationOutcome.Rejected,
            owner.ReserveForOperation(domain, conflict, out var token, out var receipt, out var reason));
        Assert.Null(token);
        Assert.Null(receipt);
        Assert.Equal("itemUidOperationConflict", reason);
        Assert.Equal(snapshot, File.ReadAllBytes(files.State));
        Assert.Equal(first, RecordedOperation(owner, domain, request));
        Assert.Equal(new uint[] { 1004 }, Fixture.Reserve(owner, 1));
    }

    [Theory]
    [InlineData("emptyOperation")]
    [InlineData("emptyWriter")]
    [InlineData("longWriter")]
    [InlineData("pathWriter")]
    [InlineData("unicodeWriter")]
    [InlineData("account")]
    [InlineData("character")]
    [InlineData("shortDigest")]
    [InlineData("upperDigest")]
    [InlineData("zeroCount")]
    [InlineData("negativeCount")]
    [InlineData("hugeCount")]
    [InlineData("nullRequest")]
    [InlineData("emptyDomain")]
    [InlineData("interruption")]
    public void InvalidOperationRequestDoesNotAdvanceOrUpgradeTheLedger(string invalid)
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap();
        var domain = OperationDomain(files);
        var request = OperationRequest();
        request = invalid switch
        {
            "emptyOperation" => request with { OperationId = Guid.Empty },
            "emptyWriter" => request with { Writer = "" },
            "longWriter" => request with { Writer = new string('x', 65) },
            "pathWriter" => request with { Writer = "../package" },
            "unicodeWriter" => request with { Writer = "package狼" },
            "account" => request with { AccountId = 0 },
            "character" => request with { CharacterUid = 0 },
            "shortDigest" => request with { PayloadSha256 = "a" },
            "upperDigest" => request with { PayloadSha256 = new string('A', 64) },
            "zeroCount" => request with { Count = 0 },
            "negativeCount" => request with { Count = -1 },
            "hugeCount" => request with { Count = int.MaxValue },
            "nullRequest" => null!,
            _ => request,
        };
        var snapshot = File.ReadAllBytes(files.State);
        var interruption = invalid == "interruption" ? (LegacyItemUidInterruption)99 : LegacyItemUidInterruption.None;
        Assert.Equal(LegacyItemUidReservationOutcome.Rejected,
            owner.ReserveForOperation(invalid == "emptyDomain" ? Guid.Empty : domain, request, interruption,
                out var token, out var receipt, out var reason));
        Assert.Null(token);
        Assert.Null(receipt);
        Assert.NotEmpty(reason);
        Assert.Equal(snapshot, File.ReadAllBytes(files.State));
        Assert.False(File.Exists(files.Pending));
        Assert.Equal(new uint[] { 1001 }, Fixture.Reserve(owner, 1));
    }

    [Fact]
    public void LegacyRangesAndKeyedRangesShareOneForwardOnlyHighWater()
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap();
        Assert.Equal(new uint[] { 1001, 1002 }, Fixture.Reserve(owner, 2));
        Assert.Equal(1, JsonNode.Parse(File.ReadAllBytes(files.State))!["Version"]!.GetValue<int>());
        var domain = OperationDomain(files);
        var request = OperationRequest();
        var first = ReserveOperation(owner, domain, request).Receipt;
        Assert.Equal((uint)1003, first.First);
        Assert.Equal((ulong)2, first.Generation);
        Assert.Equal(new uint[] { 1006, 1007 }, Fixture.Reserve(owner, 2));
        Assert.Equal(first, RecordedOperation(owner, domain, request));
        Assert.Equal((uint)1008,
            ReserveOperation(owner, domain, OperationRequest(1)).Receipt.First);
    }
}

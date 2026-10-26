using System.Text.Json.Nodes;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyItemUidRegistryTests
{
    [Theory]
    [InlineData("unknownRoot")]
    [InlineData("unknownReceipt")]
    [InlineData("missingReceiptField")]
    [InlineData("duplicateReceiptProperty")]
    [InlineData("missingReceipts")]
    [InlineData("emptyReceipts")]
    [InlineData("nullReceipt")]
    [InlineData("tooManyReceipts")]
    [InlineData("zeroCount")]
    [InlineData("hugeCount")]
    [InlineData("zeroFirst")]
    [InlineData("overlap")]
    [InlineData("pastHighWater")]
    [InlineData("duplicateOperation")]
    [InlineData("noncanonicalOperation")]
    [InlineData("emptyOperation")]
    [InlineData("zeroGeneration")]
    [InlineData("futureGeneration")]
    [InlineData("duplicateGeneration")]
    [InlineData("invalidWriter")]
    [InlineData("zeroAccount")]
    [InlineData("zeroCharacter")]
    [InlineData("invalidDigest")]
    [InlineData("stringVersion")]
    [InlineData("v1WithReceipts")]
    public void CorruptOperationStateRefusesOpenWithoutRewritingOrReset(string corruption)
    {
        using var files = new Fixture();
        using (var owner = files.Bootstrap())
        {
            var domain = OperationDomain(files);
            ReserveOperation(owner, domain, OperationRequest());
            ReserveOperation(owner, domain, OperationRequest(2));
        }
        var state = JsonNode.Parse(File.ReadAllBytes(files.State))!.AsObject();
        var operations = state["Operations"]!.AsArray();
        var first = operations[0]!.AsObject();
        switch (corruption)
        {
            case "unknownRoot": state["Accept"] = true; break;
            case "unknownReceipt": first["Accept"] = true; break;
            case "missingReceiptField": first.Remove("Writer"); break;
            case "missingReceipts": state.Remove("Operations"); break;
            case "emptyReceipts": state["Operations"] = new JsonArray(); break;
            case "nullReceipt": operations[0] = null; break;
            case "tooManyReceipts":
                state["Operations"] = new JsonArray(Enumerable.Range(0,
                    LegacyItemUidRegistry.MaximumOperationReceipts + 1).Select(_ => (JsonNode?)null).ToArray());
                break;
            case "zeroCount": first["Count"] = 0; break;
            case "hugeCount": first["Count"] = int.MaxValue; break;
            case "zeroFirst": first["First"] = 0; break;
            case "overlap": operations[1]!["First"] = first["First"]!.GetValue<uint>(); break;
            case "pastHighWater": first["Count"] = 100; break;
            case "duplicateOperation": operations[1]!["OperationId"] = first["OperationId"]!.GetValue<string>(); break;
            case "noncanonicalOperation": first["OperationId"] = Guid.NewGuid().ToString("D"); break;
            case "emptyOperation": first["OperationId"] = Guid.Empty.ToString("N"); break;
            case "zeroGeneration": first["Generation"] = 0; break;
            case "futureGeneration": first["Generation"] = 99; break;
            case "duplicateGeneration": operations[1]!["Generation"] = 1; break;
            case "invalidWriter": first["Writer"] = "../producer"; break;
            case "zeroAccount": first["AccountId"] = 0; break;
            case "zeroCharacter": first["CharacterUid"] = 0; break;
            case "invalidDigest": first["PayloadSha256"] = "unknown"; break;
            case "stringVersion": state["Version"] = "2"; break;
            case "v1WithReceipts": state["Version"] = 1; break;
            case "duplicateReceiptProperty": break;
            default: throw new ArgumentOutOfRangeException(nameof(corruption));
        }
        var text = state.ToJsonString();
        if (corruption == "duplicateReceiptProperty")
            text = text.Replace("\"Writer\":\"package-rewards\"",
                "\"Writer\":\"package-rewards\",\"Writer\":\"package-rewards\"", StringComparison.Ordinal);
        File.WriteAllText(files.State, text);
        var bytes = File.ReadAllBytes(files.State);
        Assert.False(LegacyItemUidRegistry.TryOpen(files.Options(), out var reopened, out var reason));
        Assert.Null(reopened);
        Assert.StartsWith(LegacyItemUidRegistry.RecoveryRequired, reason);
        Assert.Equal(bytes, File.ReadAllBytes(files.State));
        Assert.False(File.Exists(files.Pending));
        Assert.False(LegacyItemUidRegistry.TryBootstrap(files.Options(), out var reset, out _));
        Assert.Null(reset);
        Assert.Equal(bytes, File.ReadAllBytes(files.State));
    }

    [Fact]
    public void BoundedFullReceiptCodecRefusesFreshOperationsButAllowsExactReconciliation()
    {
        using var files = new Fixture();
        Guid domain;
        var request = OperationRequest(1);
        using (var owner = files.Bootstrap())
        {
            domain = OperationDomain(files);
            ReserveOperation(owner, domain, request);
        }
        // Explicit synthetic codec fixture, not a claim that these operations
        // actually granted rewards. All ranges/identities remain disjoint.
        var state = JsonNode.Parse(File.ReadAllBytes(files.State))!;
        var original = state["Operations"]![0]!.DeepClone();
        var operations = new JsonArray();
        for (var index = 0; index < LegacyItemUidRegistry.MaximumOperationReceipts; index++)
        {
            var row = original.DeepClone();
            row["OperationId"] = index == 0 ? request.OperationId.ToString("N") : Guid.NewGuid().ToString("N");
            row["Generation"] = (ulong)index + 1;
            row["First"] = 1001U + (uint)index;
            operations.Add(row);
        }
        state["Operations"] = operations;
        state["Generation"] = (ulong)LegacyItemUidRegistry.MaximumOperationReceipts;
        state["HighWater"] = 1000U + (uint)LegacyItemUidRegistry.MaximumOperationReceipts;
        File.WriteAllText(files.State, state.ToJsonString());
        var bytes = File.ReadAllBytes(files.State);
        using var reopened = files.Open();
        Assert.Equal((uint)1001, RecordedOperation(reopened, domain, request).First);
        Assert.Equal(LegacyItemUidReservationOutcome.Rejected,
            reopened.ReserveForOperation(domain, OperationRequest(1), out var token, out var receipt, out var reason));
        Assert.Null(token);
        Assert.Null(receipt);
        Assert.Equal("itemUidOperationLedgerFull", reason);
        Assert.Equal(bytes, File.ReadAllBytes(files.State));
        Assert.False(File.Exists(files.Pending));
    }

    [Fact]
    public void GenerationExhaustionNeverWrapsAndDoesNotEraseRecordedOperation()
    {
        using var files = new Fixture();
        Guid domain;
        var request = OperationRequest();
        using (var owner = files.Bootstrap())
        {
            domain = OperationDomain(files);
            ReserveOperation(owner, domain, request);
        }
        var state = JsonNode.Parse(File.ReadAllBytes(files.State))!;
        state["Generation"] = ulong.MaxValue;
        File.WriteAllText(files.State, state.ToJsonString());
        var bytes = File.ReadAllBytes(files.State);
        using var reopened = files.Open();
        Assert.Equal((uint)1001, RecordedOperation(reopened, domain, request).First);
        Assert.Equal(LegacyItemUidReservationOutcome.Rejected,
            reopened.ReserveForOperation(domain, OperationRequest(1), out var token, out var receipt, out var reason));
        Assert.Null(token);
        Assert.Null(receipt);
        Assert.Equal("itemUidNamespaceExhausted", reason);
        Assert.Equal(bytes, File.ReadAllBytes(files.State));
    }

    [Fact]
    public void MaximumLegitimateKeyedRangeMaterializesExactlyOnceWithoutWrap()
    {
        using var files = new Fixture();
        using var owner = files.Bootstrap(0);
        var domain = OperationDomain(files);
        var request = OperationRequest(ushort.MaxValue);
        var reserved = ReserveOperation(owner, domain, request);
        Assert.True(reserved.Token.TryTake(out var ids));
        Assert.Equal(ushort.MaxValue, ids.Length);
        Assert.Equal((uint)1, ids[0]);
        Assert.Equal((uint)ushort.MaxValue, ids[^1]);
        Assert.Equal(reserved.Receipt, RecordedOperation(owner, domain, request));
        Assert.False(reserved.Token.TryTake(out var replay));
        Assert.Empty(replay);
        Assert.Equal(new uint[] { 65536 }, Fixture.Reserve(owner, 1));
    }
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LocalRuntimeInspectionTests
{
    private static readonly Guid Domain = Guid.Parse("a9f2d312-af87-4556-8aa9-e573b6d753c7");
    private static LegacyRuntimeInspectionRequest Request(Guid domain, uint account, uint uid) => new(1, Guid.NewGuid(), domain, account, uid);

    [Fact]
    public void SeparateStrictProtocolRejectsMissingDuplicateUnknownAndOutOfRangeRequests()
    {
        var request = Request(Domain, 1, 2); var bytes = LocalRuntimeInspectionProtocol.RequestBytes(request);
        Assert.Equal(request, LocalRuntimeInspectionProtocol.ParseRequest(bytes));
        var text = Encoding.UTF8.GetString(bytes);
        foreach (var bad in new[] { "{}", "[]", text.Replace("\"schemaVersion\":1", "\"schemaVersion\":0", StringComparison.Ordinal),
            text.Replace("\"accountId\":1", "\"accountId\":0", StringComparison.Ordinal),
            text.Replace("\"accountId\":1", "\"accountId\":1,\"accountId\":2", StringComparison.Ordinal),
            text.Replace("\"accountId\"", "\"AccountId\"", StringComparison.Ordinal),
            "{\"password\":\"never-logged\"," + text[1..], text.Replace("\"characterUid\":2", "\"characterUid\":4294967296", StringComparison.Ordinal) })
            Assert.Throws<JsonException>(() => LocalRuntimeInspectionProtocol.ParseRequest(Encoding.UTF8.GetBytes(bad)));
        Assert.Throws<JsonException>(() => LocalRuntimeInspectionProtocol.ParseRequest(new byte[LocalRuntimeInspectionProtocol.MaximumRequestBytes + 1]));
        Assert.False(LocalRuntimeInspectionProtocol.IsPipeName(LocalOperatorMailPipe.NameFor(Domain)));
        var name = LocalRuntimeInspectionProtocol.NameFor(Domain);
        Assert.True(LocalRuntimeInspectionProtocol.IsPipeName(name));
        Assert.False(LocalRuntimeInspectionProtocol.IsPipeName(name.ToUpperInvariant()));
        Assert.False(LocalRuntimeInspectionProtocol.IsPipeName("\\\\server\\pipe\\" + name));
        Assert.NotEqual(LocalOperatorMailPipe.CreateResponseReceipt(request.QueryId), LocalRuntimeInspectionProtocol.ResponseReceipt(request.QueryId));
        Assert.Equal("WRI1", Encoding.ASCII.GetString(LocalRuntimeInspectionProtocol.ResponseReceipt(request.QueryId), 0, 4));
    }

    [Fact]
    public void NoResponseCanPromoteDiagnosticFlagsOrInventObservedRows()
    {
        var denied = new LegacyRuntimeInspectionResult(1, Guid.NewGuid(), LegacyRuntimeInspectionStatus.Rejected, "unavailable", null);
        var text = LocalRuntimeInspectionProtocol.FormatResult(denied);
        Assert.Equal(denied, LocalRuntimeInspectionProtocol.ParseResult(Encoding.UTF8.GetBytes(text)));
        foreach (var flag in new[] { "atomicNativeTupleProven", "liveStoreSynchronized", "readOnly" })
        {
            var node = JsonNode.Parse(text)!; node[flag] = flag != "readOnly";
            Assert.Throws<JsonException>(() => LocalRuntimeInspectionProtocol.ParseResult(Encoding.UTF8.GetBytes(node.ToJsonString())));
        }
        var observed = new LegacyRuntimeInspectionResult(1, Guid.NewGuid(), LegacyRuntimeInspectionStatus.Observed, "diagnostic",
            new(Domain, 1, 2, DateTimeOffset.UtcNow, "VisibleDx8D13", new(2, 1002, 1, 2, 1008, 1000, 120, 100, 50, 9, 3, 5),
                new(2, 9, 3), [], [], true));
        var roundtrip = LocalRuntimeInspectionProtocol.ParseResult(Encoding.UTF8.GetBytes(LocalRuntimeInspectionProtocol.FormatResult(observed)));
        Assert.False(roundtrip.AtomicNativeTupleProven); Assert.False(roundtrip.LiveStoreSynchronized);
        Assert.Contains("raw-item/other native writers", roundtrip.Snapshot!.ConsistencyBound);
        var missing = JsonNode.Parse(LocalRuntimeInspectionProtocol.FormatResult(observed))!;
        missing["snapshot"]!["nativeVitals"] = null;
        Assert.Throws<JsonException>(() => LocalRuntimeInspectionProtocol.ParseResult(Encoding.UTF8.GetBytes(missing.ToJsonString())));
        var wrong = JsonNode.Parse(LocalRuntimeInspectionProtocol.FormatResult(observed))!;
        wrong["snapshot"]!["selectedValuesEqual"] = false;
        Assert.Throws<JsonException>(() => LocalRuntimeInspectionProtocol.ParseResult(Encoding.UTF8.GetBytes(wrong.ToJsonString())));
        var overflow = JsonNode.Parse(LocalRuntimeInspectionProtocol.FormatResult(observed))!;
        var rows = new JsonArray();
        for (var index = 1; index <= LegacyRuntimeInspectionSnapshot.MaximumRows + 1; index++)
            rows.Add(new JsonObject { ["container"]="inventory",["uid"]=index,["itemId"]=1000000,["position"]=0,["warehouseId"]=0,
                ["plus"]=0,["stackSize"]=1,["durability"]=1,["maximumDurability"]=1,["socketOne"]=0,["socketTwo"]=0,["bound"]=0,["locked"]=0 });
        overflow["snapshot"]!["nativeItems"] = rows;
        Assert.Throws<JsonException>(() => LocalRuntimeInspectionProtocol.ParseResult(Encoding.UTF8.GetBytes(overflow.ToJsonString())));
        Assert.Throws<JsonException>(() => LocalRuntimeInspectionProtocol.ParseResult(new byte[LocalRuntimeInspectionProtocol.MaximumResponseBytes + 1]));
    }
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed partial class LocalRuntimeInspectionTests
{
    [Theory]
    [InlineData("nativeInventoryAndEquipmentNotInitialized")]
    [InlineData("nativeInventoryNotInitialized")]
    [InlineData("nativeEquipmentNotInitialized")]
    public void MissingNativeRootsStayUnavailableAndCannotAcquireAnEqualityResult(string reason)
    {
        var original = DetachedResult([], []);
        var unavailable = original with { Snapshot = original.Snapshot! with
            { NativeItems = null, NativeItemsUnavailableReason = reason, SelectedValuesEqual = null } };
        var observed = DecodeDiagnostic(unavailable);
        Assert.Null(observed.Snapshot!.NativeItems); Assert.Null(observed.Snapshot.SelectedValuesEqual);
        Assert.Equal(reason, observed.Snapshot.NativeItemsUnavailableReason);
        Assert.False(observed.AtomicNativeTupleProven); Assert.False(observed.LiveStoreSynchronized);
        foreach (var invalid in new[] { "comparison", "reasonMissing", "reasonUnknown", "inventedEmpty" })
        {
            var node = JsonNode.Parse(LocalRuntimeInspectionProtocol.FormatResult(unavailable))!;
            var snapshot = node["snapshot"]!;
            if (invalid == "comparison") snapshot["selectedValuesEqual"] = true;
            else if (invalid == "reasonMissing") snapshot["nativeItemsUnavailableReason"] = null;
            else if (invalid == "reasonUnknown") snapshot["nativeItemsUnavailableReason"] = "unknownOwner";
            else snapshot["nativeItems"] = new JsonArray();
            Assert.Throws<JsonException>(() => LocalRuntimeInspectionProtocol.ParseResult(Encoding.UTF8.GetBytes(node.ToJsonString())));
        }
    }
}

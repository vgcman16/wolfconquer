using System.Text.Json;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoAdditionResolveInspectionCommandTests
{
    private static void AssertSuccess(Result result, Inputs fixture)
    {
        Assert.Equal(0, result.ExitCode); Assert.Empty(result.Error);
        using var document = JsonDocument.Parse(result.Output);
        var value = document.RootElement;
        AssertProperties(value, ["FormatVersion", "ClientVersion", "Status", "ResolveFieldCount", "AdditionRowCount",
            "AdditionNumericColumnCount", "PlainInfoSha256", "PackedAdditionSha256", "DecodedAdditionSha256"]);
        Assert.Equal(1, value.GetProperty("FormatVersion").GetInt32());
        Assert.Equal(7937, value.GetProperty("ClientVersion").GetInt32());
        Assert.Equal("parsed", value.GetProperty("Status").GetString());
        Assert.Equal(3, value.GetProperty("ResolveFieldCount").GetInt32());
        Assert.Equal(2, value.GetProperty("AdditionRowCount").GetInt32());
        Assert.Equal(5, value.GetProperty("AdditionNumericColumnCount").GetInt32());
        Assert.Equal(Pin(fixture.Info), value.GetProperty("PlainInfoSha256").GetString());
        Assert.Equal(Pin(fixture.Packed), value.GetProperty("PackedAdditionSha256").GetString());
        Assert.Equal(Pin(System.Text.Encoding.ASCII.GetBytes(Addition)), value.GetProperty("DecodedAdditionSha256").GetString());
        foreach (var name in new[] { "PlainInfoSha256", "PackedAdditionSha256", "DecodedAdditionSha256" })
            Assert.Matches("^[0-9a-f]{64}$", value.GetProperty(name).GetString()!);
        AssertNoDisclosure(result, fixture);
    }

    private static void AssertRefusal(Result result, Inputs fixture, string failure)
    {
        Assert.Equal(1, result.ExitCode); Assert.Empty(result.Output);
        using var document = JsonDocument.Parse(result.Error);
        var value = document.RootElement;
        AssertProperties(value, ["FormatVersion", "ClientVersion", "Status", "Failure"]);
        Assert.Equal(1, value.GetProperty("FormatVersion").GetInt32());
        Assert.Equal(7937, value.GetProperty("ClientVersion").GetInt32());
        Assert.Equal("refused", value.GetProperty("Status").GetString());
        Assert.Equal(failure, value.GetProperty("Failure").GetString());
        AssertNoDisclosure(result, fixture);
    }

    private static void AssertProperties(JsonElement value, string[] expected)
    {
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        Assert.Equal(expected.Concat(BoundaryFlags).Order(StringComparer.Ordinal),
            value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        foreach (var flag in BoundaryFlags) Assert.False(value.GetProperty(flag).GetBoolean());
    }

    private static void AssertNoDisclosure(Result result, Inputs fixture)
    {
        var all = result.Output + result.Error;
        foreach (var hidden in new[] { fixture.Root, fixture.InfoPath, fixture.AdditionPath,
            Path.GetFileName(fixture.InfoPath), Path.GetFileName(fixture.AdditionPath), PrivateMarker,
            "CommonNum", "GoldNum", "MaterialID", "RawLine", "NumericColumns",
            "3141592653", "2718281828", "1618033988", "2700000001", "2700000002", "2600000001", "2600000002", "@@" })
            Assert.DoesNotContain(hidden, all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Unhandled exception", all, StringComparison.OrdinalIgnoreCase);
        foreach (var json in new[] { result.Output, result.Error })
        {
            if (json.Length == 0) continue;
            using var document = JsonDocument.Parse(json);
            AssertNoRowIdDisclosure(document.RootElement, root: true);
        }
    }

    private static void AssertNoRowIdDisclosure(JsonElement value, bool root = false)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                Assert.DoesNotContain("RowId", property.Name, StringComparison.OrdinalIgnoreCase);
                // Only this exact declared static code at the root Failure
                // property is diagnostic metadata rather than a row disclosure.
                if (root && property.Name == "Failure" && property.Value.ValueKind == JsonValueKind.String &&
                    property.Value.GetString() == "AdditionParser:DuplicateRowId") continue;
                AssertNoRowIdDisclosure(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in value.EnumerateArray()) AssertNoRowIdDisclosure(element);
        }
        else if (value.ValueKind == JsonValueKind.String)
            Assert.DoesNotContain("RowId", value.GetString()!, StringComparison.OrdinalIgnoreCase);
    }
}

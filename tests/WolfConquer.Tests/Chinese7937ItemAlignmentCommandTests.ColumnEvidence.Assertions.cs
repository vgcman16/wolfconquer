using System.Text;
using System.Text.Json;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937ItemAlignmentCommandTests
{
    private JsonDocument ReadColumnEvidence(Result result)
    {
        Assert.Equal(0, result.Exit); Assert.Equal("", result.Error);
        Assert.InRange(Encoding.UTF8.GetByteCount(result.Output), 1, 65536);
        var json = JsonDocument.Parse(result.Output);
        var report = json.RootElement;
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "FormatVersion", "ClientVersion", "Status", "ReferenceRows", "CurrentRows", "SharedIdentities",
            "ReferenceOnlyIdentities", "CurrentOnlyIdentities", "FramedVectorBytes", "ReferenceFields", "CurrentFields",
            "ComparedReferenceColumns", "ColumnsWithoutExactCandidate", "ColumnsWithOneExactCandidate",
            "ColumnsWithMultipleExactCandidates", "ReferenceSha256", "CurrentSha256", "IndexBase", "ExcludedIdentityColumn",
            "ColumnEvidence", "SemanticRolesProved", "RuntimeBinding", "GameplayActivated", "InputsWritten", "OutputFilesWritten",
        };
        foreach (var property in report.EnumerateObject())
        {
            Assert.Contains(property.Name, allowed);
            if (property.Name == "ColumnEvidence") continue;
            Assert.DoesNotContain(property.Value.ValueKind, new[] { JsonValueKind.Object, JsonValueKind.Array });
        }
        Assert.Equal(2, report.GetProperty("FormatVersion").GetInt32());
        Assert.Equal(7937, report.GetProperty("ClientVersion").GetInt32());
        Assert.Equal(0, report.GetProperty("IndexBase").GetInt32()); Assert.Equal(0, report.GetProperty("ExcludedIdentityColumn").GetInt32());
        Assert.Equal(65, report.GetProperty("ReferenceFields").GetInt32()); Assert.Equal(67, report.GetProperty("CurrentFields").GetInt32());
        foreach (var flag in new[] { "SemanticRolesProved", "RuntimeBinding", "GameplayActivated", "InputsWritten", "OutputFilesWritten" })
            Assert.False(report.GetProperty(flag).GetBoolean());
        var columns = report.GetProperty("ColumnEvidence");
        Assert.InRange(columns.GetArrayLength(), 0, 64);
        Assert.Equal(report.GetProperty("ComparedReferenceColumns").GetInt32(), columns.GetArrayLength());
        var index = 0;
        foreach (var column in columns.EnumerateArray())
        {
            Assert.Equal(new[] { "CurrentColumns", "ReferenceColumn", "SharingReferenceColumns", "Status" },
                column.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
            Assert.Equal(++index, column.GetProperty("ReferenceColumn").GetInt32());
            var candidates = ReadOrdinals(column.GetProperty("CurrentColumns"), 66);
            var sharing = ReadOrdinals(column.GetProperty("SharingReferenceColumns"), 64);
            Assert.Contains(column.GetProperty("Status").GetString(), new[]
            { "MissingExactCandidate", "SingleExactCandidate", "AmbiguousCandidates", "InsufficientDistinctTargetColumns" });
            if (candidates.Length == 0) Assert.Empty(sharing);
            else Assert.Contains(index, sharing);
        }
        RequireNoEvidenceStrings(report);
        return json;
    }

    private static int[] ReadOrdinals(JsonElement array, int maximum)
    {
        Assert.InRange(array.GetArrayLength(), 0, maximum);
        var values = array.EnumerateArray().Select(value => value.GetInt32()).ToArray();
        Assert.All(values, value => Assert.InRange(value, 1, maximum));
        Assert.Equal(values.Distinct().Order(), values);
        return values;
    }

    private void RequireNoEvidenceStrings(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject()) RequireNoEvidenceStrings(property.Value);
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var value in element.EnumerateArray()) RequireNoEvidenceStrings(value);
        else if (element.ValueKind == JsonValueKind.String)
            foreach (var forbidden in new[] { root, "private-source", "private-current", "private-reference", "secret-", "Unhandled exception" })
                Assert.DoesNotContain(forbidden, element.GetString()!, StringComparison.OrdinalIgnoreCase);
    }
}

using System.Text;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937ItemAlignmentCommandTests
{
    private Task<Result> InspectColumns(Inputs inputs) =>
        Run(Command, "7937", inputs.Reference, inputs.ReferencePin, inputs.Current, inputs.CurrentPin, "--column-evidence");

    [Fact]
    public async Task ExplicitReportHasEveryIndependentOrdinalWhileDefaultSchemaStaysUnchanged()
    {
        var inputs = await WriteInputs(ClientItemCatalog7937AlignmentTests.UniquePair());
        using var aggregate = ReadResult(await Inspect(inputs), success: true);
        Assert.False(aggregate.RootElement.TryGetProperty("ColumnEvidence", out _));
        using var json = ReadColumnEvidence(await InspectColumns(inputs));
        var report = json.RootElement;
        Assert.Equal("UniqueCompleteByteAlignment", report.GetProperty("Status").GetString());
        Assert.Equal(inputs.ReferencePin, report.GetProperty("ReferenceSha256").GetString());
        Assert.Equal(inputs.CurrentPin, report.GetProperty("CurrentSha256").GetString());
        var expected = ClientItemCatalog7937AlignmentTests.EvidenceTargets();
        var columns = report.GetProperty("ColumnEvidence");
        Assert.Equal(64, columns.GetArrayLength());
        for (var index = 0; index < 64; index++)
        {
            Assert.Equal("SingleExactCandidate", columns[index].GetProperty("Status").GetString());
            Assert.Equal(new[] { expected[index] }, ReadOrdinals(columns[index].GetProperty("CurrentColumns"), 66));
            Assert.Equal(new[] { index + 1 }, ReadOrdinals(columns[index].GetProperty("SharingReferenceColumns"), 64));
        }
        await Preserved(inputs);
    }

    [Fact]
    public async Task MixedMissingAndInjectivityEvidenceSurvivesGlobalMissingPrecedence()
    {
        var inputs = await WriteInputs(ClientItemCatalog7937AlignmentTests.MixedEvidencePair());
        using var json = ReadColumnEvidence(await InspectColumns(inputs));
        Assert.Equal("MissingExactColumnMatches", json.RootElement.GetProperty("Status").GetString());
        var columns = json.RootElement.GetProperty("ColumnEvidence");
        for (var index = 0; index < 64; index++)
        {
            var missing = index < 2;
            int[] expectedCandidates = missing ? [] : Enumerable.Range(1, 61).ToArray();
            int[] expectedSharing = missing ? [] : Enumerable.Range(3, 62).ToArray();
            Assert.Equal(missing ? "MissingExactCandidate" : "InsufficientDistinctTargetColumns", columns[index].GetProperty("Status").GetString());
            Assert.Equal(expectedCandidates, ReadOrdinals(columns[index].GetProperty("CurrentColumns"), 66));
            Assert.Equal(expectedSharing, ReadOrdinals(columns[index].GetProperty("SharingReferenceColumns"), 64));
        }
        await Preserved(inputs);
    }

    [Fact]
    public async Task MaximumAmbiguousSchemaIsFiniteAndDoesNotChooseAnyCandidate()
    {
        var inputs = await WriteInputs(new(ClientItemCatalog7937AlignmentTests.Row(ClientItemCatalog7937AlignmentTests.Fields("123456789", 65)),
            ClientItemCatalog7937AlignmentTests.Row(ClientItemCatalog7937AlignmentTests.Fields("123456789", 67))));
        var result = await InspectColumns(inputs);
        using var json = ReadColumnEvidence(result);
        Assert.InRange(Encoding.UTF8.GetByteCount(result.Output), 1, 65536);
        Assert.Equal("AmbiguousCompleteByteAlignments", json.RootElement.GetProperty("Status").GetString());
        foreach (var column in json.RootElement.GetProperty("ColumnEvidence").EnumerateArray())
        {
            Assert.Equal("AmbiguousCandidates", column.GetProperty("Status").GetString());
            Assert.Equal(Enumerable.Range(1, 66), ReadOrdinals(column.GetProperty("CurrentColumns"), 66));
            Assert.Equal(Enumerable.Range(1, 64), ReadOrdinals(column.GetProperty("SharingReferenceColumns"), 64));
        }
        await Preserved(inputs);
    }

    [Fact]
    public async Task NoSharedIdsHasNoFabricatedColumnEvidence()
    {
        var inputs = await WriteInputs(new(ClientItemCatalog7937AlignmentTests.Row(ClientItemCatalog7937AlignmentTests.Fields("1", 65)),
            ClientItemCatalog7937AlignmentTests.Row(ClientItemCatalog7937AlignmentTests.Fields("2", 67))));
        using var json = ReadColumnEvidence(await InspectColumns(inputs));
        Assert.Equal("NoSharedIdentities", json.RootElement.GetProperty("Status").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("ColumnEvidence").GetArrayLength());
        await Preserved(inputs);
    }

    [Theory]
    [InlineData("unknown")] [InlineData("duplicate")] [InlineData("pin")]
    public async Task ReportModeRefusalsContainNoPartialColumnsOrPrivatePayload(string scenario)
    {
        var inputs = await WriteInputs(ClientItemCatalog7937AlignmentTests.UniquePair());
        string[] args = [Command, "7937", inputs.Reference, scenario == "pin" ? new string('0', 64) : inputs.ReferencePin,
            inputs.Current, inputs.CurrentPin, scenario == "unknown" ? "--columns" : "--column-evidence"];
        if (scenario == "duplicate") args = [.. args, "--column-evidence"];
        using var json = ReadResult(await Run(args), success: false);
        Assert.Equal(scenario == "pin" ? "PinMismatch" : "Arguments", json.RootElement.GetProperty("Failure").GetString());
        Assert.False(json.RootElement.TryGetProperty("ColumnEvidence", out _));
        await Preserved(inputs);
    }
}

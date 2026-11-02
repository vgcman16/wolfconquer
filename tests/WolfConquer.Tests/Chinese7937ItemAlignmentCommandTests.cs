using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937ItemAlignmentCommandTests
{
    [Fact]
    public async Task ActualCommandReportsCompleteUniqueByteEvidenceWithoutMapsValuesOrWrites()
    {
        var inputs = await WriteInputs(ClientItemCatalog7937AlignmentTests.UniquePair());
        var referenceStamp = File.GetLastWriteTimeUtc(inputs.Reference); var currentStamp = File.GetLastWriteTimeUtc(inputs.Current);
        using var json = ReadResult(await Inspect(inputs), success: true);
        var result = json.RootElement;
        Assert.Equal("UniqueCompleteByteAlignment", result.GetProperty("Status").GetString());
        Assert.Equal(2, result.GetProperty("ReferenceRows").GetInt32()); Assert.Equal(3, result.GetProperty("CurrentRows").GetInt32());
        Assert.Equal(2, result.GetProperty("SharedIdentities").GetInt32()); Assert.Equal(1, result.GetProperty("CurrentOnlyIdentities").GetInt32());
        Assert.Equal(65, result.GetProperty("ReferenceFields").GetInt32()); Assert.Equal(67, result.GetProperty("CurrentFields").GetInt32());
        Assert.Equal(64, result.GetProperty("ComparedReferenceColumns").GetInt32());
        Assert.Equal(64, result.GetProperty("ColumnsWithOneExactCandidate").GetInt32());
        Assert.Equal(0, result.GetProperty("ColumnsWithoutExactCandidate").GetInt32());
        Assert.Equal(0, result.GetProperty("ColumnsWithMultipleExactCandidates").GetInt32());
        Assert.Equal(inputs.ReferencePin, result.GetProperty("ReferenceSha256").GetString());
        Assert.Equal(inputs.CurrentPin, result.GetProperty("CurrentSha256").GetString());
        await Preserved(inputs);
        Assert.Equal(referenceStamp, File.GetLastWriteTimeUtc(inputs.Reference)); Assert.Equal(currentStamp, File.GetLastWriteTimeUtc(inputs.Current));
    }

    [Fact]
    public async Task LastSharedIdentityMismatchIsAggregateMissingEvidenceRatherThanAChosenMap()
    {
        var inputs = await WriteInputs(ClientItemCatalog7937AlignmentTests.UniquePair(lateMismatch: true, sharedIdentities: 257));
        using var json = ReadResult(await Inspect(inputs), success: true);
        Assert.Equal("MissingExactColumnMatches", json.RootElement.GetProperty("Status").GetString());
        Assert.Equal(257, json.RootElement.GetProperty("SharedIdentities").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("ColumnsWithoutExactCandidate").GetInt32());
        Assert.Equal(63, json.RootElement.GetProperty("ColumnsWithOneExactCandidate").GetInt32());
        await Preserved(inputs);
    }

    [Fact]
    public async Task NoSharedIdsReportsNoEvidenceAndDoesNotFallBackToOrdinalAssumptions()
    {
        var inputs = await WriteInputs(new(ClientItemCatalog7937AlignmentTests.Row(ClientItemCatalog7937AlignmentTests.Fields("1", 65)),
            ClientItemCatalog7937AlignmentTests.Row(ClientItemCatalog7937AlignmentTests.Fields("2", 67))));
        using var json = ReadResult(await Inspect(inputs), success: true);
        Assert.Equal("NoSharedIdentities", json.RootElement.GetProperty("Status").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("SharedIdentities").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("ComparedReferenceColumns").GetInt32());
        await Preserved(inputs);
    }
}

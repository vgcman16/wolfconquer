using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class ClientItemCatalog7937AlignmentTests
{
    // Independent ordinal golden, not derived from the product projection.
    internal static int[] EvidenceTargets() =>
        [66,65,64,63,62,61,60,59,58,57,56,55,54,53,52,51,50,49,48,47,46,45,44,43,42,
         40,39,38,37,36,35,34,33,32,31,30,29,28,27,26,25,24,23,22,21,20,19,18,17,16,15,14,13,12,11,10,9,8,6,5,4,3,2,1];

    internal static CatalogPair MixedEvidencePair()
    {
        var reference = Fields("123456789", 65);
        var current = Fields("123456789", 67);
        reference[1] = "secret-missing-A"u8.ToArray(); reference[2] = "secret-missing-B"u8.ToArray();
        for (var column = 62; column < 67; column++) current[column] = "secret-different"u8.ToArray();
        return new(Row(reference), Row(current));
    }

    [Fact]
    public void EvidenceProjectsEveryUniqueOrdinalAndOwnsItsCopies()
    {
        var result = Analyze(UniquePair());
        var evidence = result.CreateColumnEvidence();
        var expected = EvidenceTargets();
        Assert.Equal(64, evidence.Count);
        for (var index = 0; index < evidence.Count; index++)
        {
            Assert.Equal(index + 1, evidence[index].ReferenceColumn);
            Assert.Equal(new[] { expected[index] }, evidence[index].CurrentColumns.ToArray());
            Assert.Equal(new[] { index + 1 }, evidence[index].SharingReferenceColumns.ToArray());
            Assert.Equal(ClientItemColumnEvidenceStatus.SingleExactCandidate, evidence[index].Status);
        }
        var copy = evidence[0].CurrentColumns.ToArray(); copy[0] = 0;
        var sharing = evidence[0].SharingReferenceColumns.ToArray(); sharing[0] = 0;
        Assert.Throws<NotSupportedException>(() => ((IList<ClientItemColumnEvidence>)evidence).Clear());
        Assert.Equal(new[] { 66 }, evidence[0].CurrentColumns.ToArray());
        Assert.Equal(new[] { 1 }, evidence[0].SharingReferenceColumns.ToArray());
        Assert.Equal(result.UniqueByteMapping.ToArray(), Analyze(UniquePair()).UniqueByteMapping.ToArray());
    }

    [Fact]
    public void FullSetLateMismatchReportsMissingWithoutGroupingEmptyCandidateSets()
    {
        var result = Analyze(UniquePair(lateMismatch: true, sharedIdentities: 257));
        var evidence = result.CreateColumnEvidence();
        Assert.Equal(ClientItemCatalogAlignmentStatus.MissingExactColumnMatches, result.Status);
        Assert.Equal(ClientItemColumnEvidenceStatus.MissingExactCandidate, evidence[0].Status);
        Assert.Empty(evidence[0].CurrentColumns.ToArray()); Assert.Empty(evidence[0].SharingReferenceColumns.ToArray());
        Assert.All(evidence.Skip(1), column => Assert.Equal(ClientItemColumnEvidenceStatus.SingleExactCandidate, column.Status));
        Assert.Empty(result.UniqueByteMapping.ToArray());
    }

    [Fact]
    public void MissingGlobalPrecedenceDoesNotHideIndependentInjectivityConflicts()
    {
        var result = Analyze(MixedEvidencePair());
        var evidence = result.CreateColumnEvidence();
        Assert.Equal(ClientItemCatalogAlignmentStatus.MissingExactColumnMatches, result.Status);
        foreach (var missing in evidence.Take(2))
        {
            Assert.Equal(ClientItemColumnEvidenceStatus.MissingExactCandidate, missing.Status);
            Assert.Empty(missing.CurrentColumns.ToArray()); Assert.Empty(missing.SharingReferenceColumns.ToArray());
        }
        foreach (var conflict in evidence.Skip(2))
        {
            Assert.Equal(ClientItemColumnEvidenceStatus.InsufficientDistinctTargetColumns, conflict.Status);
            Assert.Equal(Enumerable.Range(1, 61), conflict.CurrentColumns.ToArray());
            Assert.Equal(Enumerable.Range(3, 62), conflict.SharingReferenceColumns.ToArray());
        }
        Assert.Empty(result.UniqueByteMapping.ToArray());
    }

    [Fact]
    public void AmbiguousAndNoSharedResultsCannotBecomeChosenMappings()
    {
        var result = Analyze(new(Row(Fields("1", 65)), Row(Fields("1", 67))));
        Assert.Equal(ClientItemCatalogAlignmentStatus.AmbiguousCompleteByteAlignments, result.Status);
        Assert.All(result.CreateColumnEvidence(), column =>
        {
            Assert.Equal(ClientItemColumnEvidenceStatus.AmbiguousCandidates, column.Status);
            Assert.Equal(Enumerable.Range(1, 66), column.CurrentColumns.ToArray());
            Assert.Equal(Enumerable.Range(1, 64), column.SharingReferenceColumns.ToArray());
        });
        Assert.Empty(result.UniqueByteMapping.ToArray());
        result = Analyze(new(Row(Fields("1", 65)), Row(Fields("2", 67))));
        Assert.Equal(ClientItemCatalogAlignmentStatus.NoSharedIdentities, result.Status);
        Assert.Empty(result.CreateColumnEvidence()); Assert.Empty(result.UniqueByteMapping.ToArray());
    }
}

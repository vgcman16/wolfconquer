using System.Runtime.InteropServices;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class ClientItemCatalog7937AlignmentTests
{
    [Fact]
    public void CompleteSharedSetFindsNonAppendedReversedByteMappingWithoutSemantics()
    {
        var result = Analyze(UniquePair());
        Assert.Equal(ClientItemCatalogAlignmentStatus.UniqueCompleteByteAlignment, result.Status);
        Assert.Equal(2, result.ReferenceRows); Assert.Equal(3, result.CurrentRows);
        Assert.Equal(2, result.SharedIdentities); Assert.Equal(0, result.ReferenceOnlyIdentities); Assert.Equal(1, result.CurrentOnlyIdentities);
        Assert.Equal(64, result.Columns.Count);
        // Independent zero-based ordinal golden: opaque columns7 and41 are new.
        int[] expected = [66,65,64,63,62,61,60,59,58,57,56,55,54,53,52,51,50,49,48,47,46,45,44,43,42,
            40,39,38,37,36,35,34,33,32,31,30,29,28,27,26,25,24,23,22,21,20,19,18,17,16,15,14,13,12,11,10,9,8,6,5,4,3,2,1];
        Assert.Equal(Enumerable.Range(1, 64), result.Columns.Select(column => column.ReferenceColumn));
        Assert.Equal(expected, result.Columns.Select(column => Assert.Single(column.CurrentColumns.ToArray())));
        Assert.Equal(expected.Select((target, index) => new ClientItemColumnMatch(index + 1, target)), result.UniqueByteMapping.ToArray());
    }

    [Fact]
    public void MismatchOnTheLastSharedIdentityCannotBeHiddenByEarlierAgreement()
    {
        var result = Analyze(UniquePair(lateMismatch: true, sharedIdentities: 257));
        Assert.Equal(ClientItemCatalogAlignmentStatus.MissingExactColumnMatches, result.Status);
        Assert.Equal(257, result.SharedIdentities); Assert.Empty(result.Columns[0].CurrentColumns.ToArray());
        Assert.Equal(63, result.Columns.Count(column => column.CurrentColumns.Length == 1));
        Assert.Empty(result.UniqueByteMapping.ToArray());
    }

    [Fact]
    public void LengthFramingPreventsEqualConcatenatedBytesFromBecomingColumnEvidence()
    {
        var old1 = Fields("1", 65); var old2 = Fields("2", 65);
        var new1 = Fields("1", 67); var new2 = Fields("2", 67);
        old1[1] = "a"u8.ToArray(); old2[1] = "bc"u8.ToArray();
        new1[1] = "ab"u8.ToArray(); new2[1] = "c"u8.ToArray();
        var result = Analyze(new([.. Row(old1), .. Row(old2)], [.. Row(new1), .. Row(new2)]));
        Assert.Equal(ClientItemCatalogAlignmentStatus.MissingExactColumnMatches, result.Status);
        Assert.Empty(result.Columns[0].CurrentColumns.ToArray()); Assert.Empty(result.UniqueByteMapping.ToArray());
    }

    [Fact]
    public void EqualVectorsRemainAmbiguousAndTooFewDistinctTargetsRemainConflicting()
    {
        var reference = Fields("1", 65);
        var current = Fields("1", 67);
        var ambiguous = Analyze(new(Row(reference), Row(current)));
        Assert.Equal(ClientItemCatalogAlignmentStatus.AmbiguousCompleteByteAlignments, ambiguous.Status);
        Assert.All(ambiguous.Columns, column => Assert.Equal(66, column.CurrentColumns.Length));
        Assert.Empty(ambiguous.UniqueByteMapping.ToArray());
        for (var column = 64; column < 67; column++) current[column] = "different"u8.ToArray();
        var insufficient = Analyze(new(Row(reference), Row(current)));
        Assert.Equal(ClientItemCatalogAlignmentStatus.InsufficientDistinctTargetColumns, insufficient.Status);
        Assert.All(insufficient.Columns, column => Assert.Equal(63, column.CurrentColumns.Length));
        Assert.Empty(insufficient.UniqueByteMapping.ToArray());
    }

    [Fact]
    public void NoSharedIdentitiesReportsNoEvidenceAndNoEmptySuccessfulMapping()
    {
        var result = Analyze(new(Row(Fields("1", 65)), Row(Fields("2", 67))));
        Assert.Equal(ClientItemCatalogAlignmentStatus.NoSharedIdentities, result.Status);
        Assert.Equal(0, result.SharedIdentities); Assert.Equal(0, result.FramedVectorBytes);
        Assert.Equal(1, result.ReferenceOnlyIdentities); Assert.Equal(1, result.CurrentOnlyIdentities);
        Assert.Empty(result.Columns); Assert.Empty(result.UniqueByteMapping.ToArray());
    }

    [Fact]
    public void OpaqueBytesAreNeitherDecodedNorNumericallyNormalized()
    {
        var reference = Fields("01", 65); var current = Fields("1", 67);
        reference[1] = "01"u8.ToArray(); current[1] = "1"u8.ToArray();
        reference[2] = [0xd6, 0xd0]; current[9] = [0xd6, 0xd0];
        reference[3] = []; current[10] = [];
        var result = Analyze(new(Row(reference), Row(current)));
        Assert.Empty(result.Columns[0].CurrentColumns.ToArray());
        Assert.Equal(new[] { 9 }, result.Columns[1].CurrentColumns.ToArray());
        Assert.Equal(new[] { 10 }, result.Columns[2].CurrentColumns.ToArray());
        Assert.Equal(1, result.SharedIdentities);
    }

    [Fact]
    public void PublicFieldCopiesInputReuseAndResultCopiesCannotMutateTheEvidence()
    {
        var bytes = UniquePair();
        var reference = ClientItemCatalogDocument.Parse(bytes.Reference);
        var current = Chinese7937ItemCatalogDocument.ParseDecoded(bytes.Current, 7937);
        Assert.True(ClientItemCatalog7937Alignment.TryAnalyze(reference, current, out var first, out _));
        var result = Assert.IsType<ClientItemCatalog7937Alignment>(first);
        Array.Clear(bytes.Reference); Array.Clear(bytes.Current);
        Assert.True(MemoryMarshal.TryGetArray(reference.Rows[0].GetField(1), out var oldCopy));
        Assert.True(MemoryMarshal.TryGetArray(current.Rows[0].GetField(1), out var newCopy));
        Array.Clear(oldCopy.Array!); Array.Clear(newCopy.Array!);
        var targets = result.Columns[0].CurrentColumns.ToArray(); targets[0] = 0;
        var map = result.UniqueByteMapping.ToArray(); map[0] = default;
        Assert.Throws<NotSupportedException>(() => ((IList<ClientItemColumnCandidates>)result.Columns).Clear());
        Assert.True(ClientItemCatalog7937Alignment.TryAnalyze(reference, current, out var second, out _));
        Assert.Equal(result.UniqueByteMapping.ToArray(), second!.UniqueByteMapping.ToArray());
        Assert.Equal(new[] { 66 }, result.Columns[0].CurrentColumns.ToArray());
        Assert.Equal(new ClientItemColumnMatch(1, 66), result.UniqueByteMapping[0]);
    }
}

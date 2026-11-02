using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class ClientItemCatalog7937AlignmentTests
{
    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(268435457)]
    public void InvalidOrRaisedVectorBudgetsProduceNoEvidence(int budget)
    {
        var bytes = UniquePair();
        var reference = ClientItemCatalogDocument.Parse(bytes.Reference);
        var current = Chinese7937ItemCatalogDocument.ParseDecoded(bytes.Current, 7937);
        Assert.False(ClientItemCatalog7937Alignment.TryAnalyze(reference, current, out var result, out var failure, budget));
        Assert.Null(result); Assert.Equal(ClientItemCatalogAlignmentFailure.InvalidBudget, failure);
    }

    [Fact]
    public void ExactFramedBudgetIncludesAll130NonIdentityColumnsAcrossAllSharedRows()
    {
        var reference = ClientItemCatalogDocument.Parse([.. Row(Fields("0", 65)), .. Row(Fields("4294967295", 65))]);
        var current = Chinese7937ItemCatalogDocument.ParseDecoded([.. Row(Fields("4294967295", 67)), .. Row(Fields("000", 67))], 7937);
        // Two identities × (64+66) fields × (4-byte length + one byte) =1300.
        Assert.True(ClientItemCatalog7937Alignment.TryAnalyze(reference, current, out var result, out _, 1300));
        Assert.Equal(1300, result!.FramedVectorBytes); Assert.Equal(2, result.SharedIdentities);
        Assert.False(ClientItemCatalog7937Alignment.TryAnalyze(reference, current, out result, out var failure, 1299));
        Assert.Null(result); Assert.Equal(ClientItemCatalogAlignmentFailure.VectorBounds, failure);
    }

    [Fact]
    public void MissingParsedOwnersDoNotProduceAnEmptyAcceptingResult()
    {
        var bytes = UniquePair();
        var reference = ClientItemCatalogDocument.Parse(bytes.Reference);
        var current = Chinese7937ItemCatalogDocument.ParseDecoded(bytes.Current, 7937);
        foreach (var pair in new[] { (Reference: (ClientItemCatalogDocument?)null, Current: current), (Reference: reference, Current: (Chinese7937ItemCatalogDocument?)null) })
        {
            Assert.False(ClientItemCatalog7937Alignment.TryAnalyze(pair.Reference, pair.Current, out var result, out var failure));
            Assert.Null(result); Assert.Equal(ClientItemCatalogAlignmentFailure.RequiredDocument, failure);
        }
    }

    [Fact]
    public void DuplicateCanonicalSourcesAndWrongCurrentWidthRefuseBeforeComparison()
    {
        Assert.Throws<InvalidDataException>(() => ClientItemCatalogDocument.Parse([.. Row(Fields("1", 65)), .. Row(Fields("01", 65))]));
        Assert.Throws<InvalidDataException>(() => Chinese7937ItemCatalogDocument.ParseDecoded([.. Row(Fields("1", 67)), .. Row(Fields("01", 67))], 7937));
        Assert.Throws<InvalidDataException>(() => Chinese7937ItemCatalogDocument.ParseDecoded(Row(Fields("1", 65)), 7937));
    }

    [Fact]
    public void HistoricalDocumentAboveTheCompleteByteLimitRefusesBeforeVectorPreparation()
    {
        // The historical document's genuine immutable edit API has no64MiB
        // cap. This consumer must enforce its own bound, including delimiters.
        var reference = ClientItemCatalogDocument.Parse(Row(Fields("1", 65)));
        var oversizedField = new byte[64 * 1024 * 1024]; Array.Fill(oversizedField, (byte)'x');
        reference = reference.WithField(1, oversizedField);
        var current = Chinese7937ItemCatalogDocument.ParseDecoded(Row(Fields("1", 67)), 7937);
        Assert.False(ClientItemCatalog7937Alignment.TryAnalyze(reference, current, out var result, out var failure));
        Assert.Null(result); Assert.Equal(ClientItemCatalogAlignmentFailure.InputBounds, failure);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompleteCatalogRowLimitRefusesEvenWhenTheSharedSetWouldBeTiny(bool currentTooLarge)
    {
        using var bytes = new MemoryStream();
        var width = currentTooLarge ? 67 : 65;
        for (var id = 0; id <= 100000; id++)
        {
            bytes.Write(Encoding.ASCII.GetBytes(id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            bytes.Write("@@"u8);
            for (var column = 1; column < width; column++) bytes.Write("@@"u8);
            bytes.Write("\r\n"u8);
        }
        var reference = ClientItemCatalogDocument.Parse(currentTooLarge ? Row(Fields("1", 65)) : bytes.ToArray());
        var current = Chinese7937ItemCatalogDocument.ParseDecoded(currentTooLarge ? bytes.ToArray() : Row(Fields("1", 67)), 7937);
        Assert.False(ClientItemCatalog7937Alignment.TryAnalyze(reference, current, out var result, out var failure));
        Assert.Null(result); Assert.Equal(ClientItemCatalogAlignmentFailure.InputBounds, failure);
    }
}

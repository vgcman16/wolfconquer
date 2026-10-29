using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class Chinese7937ItemCatalogTests
{
    internal static byte[] Row(string id = "42", int fields = 67, byte[]? last = null, string ending = "\r\n")
    {
        using var output = new MemoryStream();
        for (var index = 0; index < fields; index++)
        {
            output.Write(index == fields - 1 && last is not null ? last :
                Encoding.ASCII.GetBytes(index == 0 ? id : index == 1 ? "Synthetic7937" : "0"));
            output.Write("@@"u8);
        }
        output.Write(Encoding.ASCII.GetBytes(ending)); return output.ToArray();
    }

    [Fact]
    public void Exact67FieldRowsRetainAllBytesAndTheExistingOfflineIdentityEnvelope()
    {
        var bytes = Row("4294967295").Concat(Row("0")).Concat(Row("00042", last: [0xd6, 0xd0])).ToArray();
        var parsed = Chinese7937ItemCatalogDocument.ParseDecoded(bytes, 7937);
        Assert.Equal(new uint[] { uint.MaxValue, 0, 42 }, parsed.Rows.Select(row => row.ItemId));
        Assert.All(parsed.Rows, row => Assert.Equal(67, row.FieldCount));
        Assert.Equal(new byte[] { 0xd6, 0xd0 }, parsed.Rows[2].GetField(66).ToArray());
        Assert.Equal("00042"u8.ToArray(), parsed.Rows[2].GetField(0).ToArray());
        Assert.Equal(bytes, parsed.ToArray());
        var packed = ClientItemCatalogCodec.Encode(bytes);
        Assert.Equal(bytes, Chinese7937ItemCatalogDocument.ParsePacked(packed, 7937).ToArray());
    }

    [Theory]
    [InlineData(65)] [InlineData(66)] [InlineData(68)]
    public void OtherFieldCountsAreNotAcceptedAsThe7937Envelope(int fields) =>
        Assert.Throws<InvalidDataException>(() => Chinese7937ItemCatalogDocument.ParseDecoded(Row(fields: fields), 7937));

    [Theory]
    [InlineData(7931)] [InlineData(7935)] [InlineData(7938)]
    public void OtherDeclaredVersionsCannotBorrowThisSchema(int version) =>
        Assert.Throws<InvalidDataException>(() => Chinese7937ItemCatalogDocument.ParseDecoded(Row(), version));

    [Theory]
    [InlineData("")] [InlineData("\n")] [InlineData("\r")]
    public void UnobservedLineEndingExtensionsRefuse(string ending) =>
        Assert.Throws<InvalidDataException>(() => Chinese7937ItemCatalogDocument.ParseDecoded(Row(ending: ending), 7937));

    [Theory]
    [InlineData("")] [InlineData("-1")] [InlineData("1.0")] [InlineData("4294967296")]
    public void InvalidIdentityHasNoConversionFallback(string id) =>
        Assert.Throws<InvalidDataException>(() => Chinese7937ItemCatalogDocument.ParseDecoded(Row(id), 7937));

    [Fact]
    public void HeaderBomEmptyNulAndCanonicalDuplicateRowsNeverGetSkippedOrRepaired()
    {
        foreach (var bytes in new[] { "Header\r\n"u8.ToArray().Concat(Row()).ToArray(),
            new byte[] { 0xef, 0xbb, 0xbf }.Concat(Row()).ToArray(),
            "\r\n"u8.ToArray().Concat(Row()).ToArray(), Row(last: [0]), Row("1").Concat(Row("01")).ToArray() })
            Assert.Throws<InvalidDataException>(() => Chinese7937ItemCatalogDocument.ParseDecoded(bytes, 7937));
        Assert.Throws<InvalidDataException>(() => Chinese7937ItemCatalogDocument.ParseDecoded([], 7937));
    }

    [Fact]
    public void InputFieldsReconstructionAndRowCollectionDoNotExposeMutableStorage()
    {
        var bytes = Row(); var expected = bytes.ToArray();
        var parsed = Chinese7937ItemCatalogDocument.ParseDecoded(bytes, 7937); Array.Clear(bytes);
        var field = parsed.Rows[0].GetField(66).ToArray(); Array.Clear(field);
        var output = parsed.ToArray(); Array.Clear(output);
        Assert.Equal(expected, parsed.ToArray()); Assert.Equal("0"u8.ToArray(), parsed.Rows[0].GetField(66).ToArray());
        Assert.Throws<NotSupportedException>(() => ((IList<Chinese7937ItemCatalogRow>)parsed.Rows).Clear());
        Assert.Throws<ArgumentOutOfRangeException>(() => parsed.Rows[0].GetField(67));
    }

    [Fact]
    public void Historical65DocumentRemainsStrictAndIndependent()
    {
        Assert.Single(ClientItemCatalogDocument.Parse(Row(fields: 65)).Rows);
        Assert.Throws<InvalidDataException>(() => ClientItemCatalogDocument.Parse(Row()));
    }
}

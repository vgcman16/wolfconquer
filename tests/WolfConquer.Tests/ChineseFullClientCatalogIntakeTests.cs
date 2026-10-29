using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class ChineseFullClientCatalogIntakeTests
{
    private static byte[] ItemRows(params string[] ids)
    {
        using var output = new MemoryStream();
        foreach (var id in ids)
            output.Write(ItemRow(id, 65));
        return output.ToArray();
    }

    private static byte[] ItemRow(string id, int fields)
    {
        using var output = new MemoryStream();
        for (var index = 0; index < fields; index++)
        {
            output.Write(Encoding.ASCII.GetBytes(index == 0 ? id : index == 1 ? "SyntheticPrivateName" : "0"));
            output.Write("@@"u8);
        }
        output.Write("\r\n"u8); return output.ToArray();
    }

    private static ChineseFullClientCatalogSnapshot Capture(byte[]? main = null, byte[]? sub = null) =>
        ChineseFullClientCatalogIntake.ParseCaptured(7937, "7937\r\n"u8,
            main ?? ClientItemCatalogCodec.Encode(ItemRows("42")),
            sub ?? ClientItemCatalogCodec.Encode(ItemRows("7")), [0xff, 0, 1], [0xfe, 2, 3]);

    [Fact]
    public void ItemDocumentsKeepUnsignedIdEnvelopeSourceOrderAndSeparateCatalogs()
    {
        var decoded = ItemRows("4294967295", "0", "00042");
        var snapshot = Capture(ClientItemCatalogCodec.Encode(decoded));
        Assert.Equal(7937, snapshot.ClientVersion); Assert.False(snapshot.HasUnsupportedItems);
        Assert.Equal(4, snapshot.Findings.Count);
        var main = snapshot.Findings[0]; var sub = snapshot.Findings[1];
        Assert.Equal(ChineseFullClientCatalogState.ParsedKnownItemSchema, main.State);
        Assert.Equal(new uint[] { uint.MaxValue, 0, 42 }, main.Items!.Rows.Select(row => row.ItemId));
        Assert.Equal(decoded, main.Items.ToArray());
        Assert.Equal(7U, Assert.Single(sub.Items!.Rows).ItemId);
        Assert.Null(snapshot.DecodedEchoEvidence);
        Assert.All(snapshot.Findings.Skip(2), row =>
        {
            Assert.Equal(ChineseFullClientCatalogState.OpaqueRequiresExplicitDecoder, row.State);
            Assert.Null(row.Items); Assert.Null(row.DecodedSha256);
        });
    }

    [Theory]
    [InlineData("7937.0")] [InlineData("7.937e3")] [InlineData("7937\r")]
    [InlineData("0000")] [InlineData("-7937")] [InlineData("7937  ")]
    [InlineData("4294967295")] [InlineData("")]
    public void MalformedVersionMarkerIsNotCoercedOrDefaulted(string text) =>
        Assert.Throws<InvalidDataException>(() => ChineseFullClientCatalogIntake.ParseCaptured(
            7937, Encoding.ASCII.GetBytes(text), [1], [1], [1], [1]));

    [Fact]
    public void ExactExpectedVersionIsRequiredBeforeOtherCatalogInterpretation() =>
        Assert.Throws<InvalidDataException>(() => ChineseFullClientCatalogIntake.ParseCaptured(
            7935, "7937\n"u8, [1], [1], [1], [1]));

    [Theory]
    [InlineData(64)] [InlineData(66)]
    public void ServerOrOtherFieldCountNeverBecomesAClientDocument(int fields)
    {
        var snapshot = Capture(ClientItemCatalogCodec.Encode(ItemRow("42", fields)));
        Assert.True(snapshot.HasUnsupportedItems); Assert.Null(snapshot.Findings[0].Items);
        Assert.Equal(ChineseFullClientCatalogState.UnsupportedItemSchema, snapshot.Findings[0].State);
        Assert.Equal(7U, Assert.Single(snapshot.Findings[1].Items!.Rows).ItemId);
    }

    [Theory]
    [InlineData("4294967296")] [InlineData("-1")] [InlineData("1.0")]
    public void InvalidItemIdentityRetainsAnUnsupportedFinding(string id) =>
        Assert.Null(Capture(ClientItemCatalogCodec.Encode(ItemRows(id))).Findings[0].Items);

    [Fact]
    public void CanonicalDuplicateIdsRefuseAndCannotAlterPreviousSnapshot()
    {
        var old = Capture();
        var rejected = Capture(ClientItemCatalogCodec.Encode(ItemRows("1", "01")));
        Assert.Null(rejected.Findings[0].Items); Assert.True(rejected.HasUnsupportedItems);
        Assert.Equal(42U, Assert.Single(old.Findings[0].Items!.Rows).ItemId);
    }

    [Fact]
    public void NonIdBytesStayOpaqueRatherThanBeingTranscodedOrMappedToServerFields()
    {
        var decoded = ItemRows("42"); decoded[4] = 0xff; decoded[5] = 0x80;
        var snapshot = Capture(ClientItemCatalogCodec.Encode(decoded));
        Assert.Equal(decoded, snapshot.Findings[0].Items!.ToArray());
        Assert.Equal(new byte[] { 0xff, 0x80 }, snapshot.Findings[0].Items!.Rows[0].GetField(1).Span[..2].ToArray());
    }

    [Fact]
    public void CapturedBuffersAndReturnedFieldCopiesCannotMutateTheSnapshot()
    {
        var packed = ClientItemCatalogCodec.Encode(ItemRows("42")); var original = packed.ToArray();
        var snapshot = Capture(packed); Array.Fill(packed, (byte)0xff);
        var fields = snapshot.Findings[0].Items!.Rows[0].GetField(1).ToArray(); Array.Clear(fields);
        Assert.Equal("SyntheticPrivateName", Encoding.ASCII.GetString(snapshot.Findings[0].Items!.Rows[0].GetField(1).Span));
        Assert.Equal(original, ClientItemCatalogCodec.Encode(snapshot.Findings[0].Items!.ToArray()));
        Assert.Throws<NotSupportedException>(() => ((IList<ChineseFullClientCatalogFinding>)snapshot.Findings).Clear());
    }

    [Fact]
    public void FindingsDoNotExportNamesOrPromoteOpaqueCatalogsToGameplayProof()
    {
        var report = ChineseFullClientCatalogIntake.WriteFindingsMarkdown(Capture());
        Assert.Contains("ParsedKnownItemSchema", report, StringComparison.Ordinal);
        Assert.Contains("OpaqueRequiresExplicitDecoder", report, StringComparison.Ordinal);
        Assert.DoesNotContain("SyntheticPrivateName", report, StringComparison.Ordinal);
        Assert.Contains("not gameplay completion", report, StringComparison.Ordinal);
        Assert.Contains("No game, Lua, executable", report, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyAndOversizedSelectedInputsHaveNoFallback()
    {
        Assert.Throws<InvalidDataException>(() => Capture(Array.Empty<byte>()));
        Assert.Throws<InvalidDataException>(() => ChineseFullClientCatalogIntake.ParseCaptured(
            7937, "7937"u8, [1], [1], new byte[ChineseFullClientCatalogIntake.MaximumOpaqueBytes + 1], [1]));
    }
}

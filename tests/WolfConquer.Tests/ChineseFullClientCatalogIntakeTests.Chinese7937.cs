using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class ChineseFullClientCatalogIntakeTests
{
    [Fact]
    public void Observed7937SchemaUsesAnExplicitTypedFindingWithoutDowngradingTo65OrServer66()
    {
        var packed = ClientItemCatalogCodec.Encode(Chinese7937ItemCatalogTests.Row());
        var snapshot = Capture(packed, packed);
        Assert.False(snapshot.HasUnsupportedItems);
        Assert.All(snapshot.Findings.Take(2), row =>
        {
            Assert.Equal(ChineseFullClientCatalogState.ParsedChinese7937ItemEnvelope, row.State);
            Assert.Null(row.Items); Assert.Equal(42U, Assert.Single(row.Chinese7937Items!.Rows).ItemId);
            Assert.Equal(67, row.Chinese7937Items.Rows[0].FieldCount);
        });
        var report = ChineseFullClientCatalogIntake.WriteFindingsMarkdown(snapshot);
        Assert.Contains("ParsedChinese7937ItemEnvelope", report, StringComparison.Ordinal);
        Assert.Contains("1 / 67", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Synthetic7937", report, StringComparison.Ordinal);
        Assert.Null(snapshot.DecodedEchoEvidence);
    }

    [Fact]
    public void VersionAndUnknownWidthCannotTriggerASilentSchemaFallback()
    {
        var packed = ClientItemCatalogCodec.Encode(Chinese7937ItemCatalogTests.Row());
        var foreign = ChineseFullClientCatalogIntake.ParseCaptured(7935, "7935\r\n"u8, packed, packed, [1], [1]);
        Assert.True(foreign.HasUnsupportedItems);
        Assert.All(foreign.Findings.Take(2), row => { Assert.Null(row.Items); Assert.Null(row.Chinese7937Items); });
        var unknown = Capture(ClientItemCatalogCodec.Encode(Chinese7937ItemCatalogTests.Row(fields: 68)));
        Assert.Equal(ChineseFullClientCatalogState.UnsupportedItemSchema, unknown.Findings[0].State);
        Assert.Null(unknown.Findings[0].Items); Assert.Null(unknown.Findings[0].Chinese7937Items);
    }
}

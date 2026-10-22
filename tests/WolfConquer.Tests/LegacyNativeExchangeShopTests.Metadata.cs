using System.Globalization;
using Goods = WolfConquer.Core.Data.LegacyNativeExchangeShopGoods;
using GoodsEx = WolfConquer.Core.Data.LegacyNativeExchangeShopGoodsEx;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    private static void LoadMetadata(bool ex)
    {
        if (ex) GoodsEx.Load();
        else Goods.Load();
    }

    private static object MetadataOwner(bool ex) => ex
        ? GoodsEx.exchange_shop_goods_ex : Goods.exchange_shop_goods;

    private static int MetadataCount(bool ex) => ex
        ? GoodsEx.exchange_shop_goods_ex.Count : Goods.exchange_shop_goods.Count;

    private static uint[] Fields(bool ex, uint id)
    {
        if (ex)
        {
            var row = GoodsEx.exchange_shop_goods_ex[id];
            return [row.id, row.itemtypeid, row.shopid, row.amount, row.monopoly,
                row.cost_type, row.cost_value, row.weight, row.server_type_flag, row.start_date, row.end_date];
        }
        var value = Goods.exchange_shop_goods[id];
        return [value.id, value.itemtypeid, value.shopid, value.amount, value.monopoly,
            value.material1, value.material_amount1, value.material2, value.material_amount2,
            value.material3, value.material_amount3, value.material4, value.material_amount4];
    }

    private static uint[] Numbers(bool ex, uint id) =>
        [id, .. Enumerable.Range(2, ex ? 10 : 12).Select(value => (uint)value)];

    private static string MetadataLine(bool ex, uint id) => string.Join("@@", Numbers(ex, id));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingAndUnreadableFilePreservePriorTableBecauseReadPrecedesPublication(bool ex)
    {
        using var state = new State();
        using var files = new OwnedFiles();
        state.Metadata(ex, 91, 91);
        object old = MetadataOwner(ex);
        LoadMetadata(ex);
        Assert.Same(old, MetadataOwner(ex));
        Assert.Equal(1, MetadataCount(ex));
        string name = ex ? "exchange_shop_goods_ex.txt" : "exchange_shop_goods.txt";
        Assert.Equal("!File.Exists: " + files.Root + "\\" + name + Environment.NewLine,
            state.ConsoleText.ToString());
        state.ConsoleText.GetStringBuilder().Clear();
        files.Text(ex, MetadataLine(ex, 17));
        using (File.Open(files.PathName(ex), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            LoadMetadata(ex);
        Assert.Same(old, MetadataOwner(ex));
        Assert.Equal(1, MetadataCount(ex));
        Assert.Contains("System.IO.IOException", state.ConsoleText.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadableEmptyFilePublishesFreshEmptyTableWithoutTouchingOtherCatalog(bool ex)
    {
        using var state = new State();
        using var files = new OwnedFiles();
        state.Metadata(ex, 91, 91);
        object old = MetadataOwner(ex);
        object other = MetadataOwner(!ex);
        files.Text(ex, "");
        LoadMetadata(ex);
        Assert.NotSame(old, MetadataOwner(ex));
        Assert.Equal(0, MetadataCount(ex));
        Assert.Same(other, MetadataOwner(!ex));
        Assert.Empty(state.ConsoleText.ToString());
        Assert.True(ex ? state.ExRows.ContainsKey(91) : state.GoodsRows.ContainsKey(91));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoaderReadsAllNativeColumnsWithCurrentCultureAndRemoveEmptyDelimiterBehavior(bool ex)
    {
        using var state = new State();
        using var files = new OwnedFiles();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = culture;
        uint[] values = Numbers(ex, uint.MaxValue);
        string row = "@@" + string.Join("@@@@", values.Select(value => " p" + value + " ")) + "@@999@@";
        files.Text(ex, row);
        LoadMetadata(ex);
        Assert.Equal(1, MetadataCount(ex));
        Assert.Equal(values, Fields(ex, uint.MaxValue));
        Assert.Empty(state.ConsoleText.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MalformedLaterRowLeavesNewPublishedValidPrefixAndLogsCaughtParseFault(bool ex)
    {
        using var state = new State();
        using var files = new OwnedFiles();
        state.Metadata(ex, 91, 91);
        object old = MetadataOwner(ex);
        files.Text(ex, MetadataLine(ex, 17) + Environment.NewLine + "19@@-1@@3");
        LoadMetadata(ex);
        Assert.NotSame(old, MetadataOwner(ex));
        Assert.Equal(1, MetadataCount(ex));
        Assert.Equal(Numbers(ex, 17), Fields(ex, 17));
        Assert.Contains("System.OverflowException", state.ConsoleText.ToString());
        Assert.True(ex ? state.ExRows.ContainsKey(91) : state.GoodsRows.ContainsKey(91));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateMetadataKeyStopsAtConcreteAddAndKeepsOnlyFirstPhysicalRecord(bool ex)
    {
        using var state = new State();
        using var files = new OwnedFiles();
        string second = MetadataLine(ex, 17).Replace("@@2@@", "@@99@@", StringComparison.Ordinal);
        files.Text(ex, MetadataLine(ex, 17) + Environment.NewLine + second + Environment.NewLine + MetadataLine(ex, 19));
        LoadMetadata(ex);
        Assert.Equal(1, MetadataCount(ex));
        Assert.Equal(Numbers(ex, 17), Fields(ex, 17));
        Assert.Contains("System.ArgumentException", state.ConsoleText.ToString());
    }
}

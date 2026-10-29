using System.Text;
using WolfConquer.Core.Client;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7935EchoCatalogTests
{
    private const Chinese7935EchoCatalogVersion Current = Chinese7935EchoCatalogVersion.Catalog7931;
    private static byte[] Module(string rows) => Encoding.UTF8.GetBytes(
        "local ret = {}; ret.cq_xuanbao_huying_extra_attr = {" + rows + "}; return ret;");
    private static string Row(int id = 1, string type = "4", string value = "10", string extraType = "6", string extra = "17") =>
        "[" + id + "]={attr_type=" + type + ",attr_value=" + value + ",extra_attr_type=" + extraType + ",extra_attr_value=" + extra + "}";
    private static Chinese7935EchoCatalogSnapshot Parse(byte[] bytes, Chinese7935EchoCatalogVersion version = Current)
    {
        Assert.True(Chinese7935DecodedEchoCatalog.TryParseDecoded(bytes, version, out var snapshot, out var failure), failure.ToString());
        return Assert.IsType<Chinese7935EchoCatalogSnapshot>(snapshot);
    }
    private static void Refuses(byte[] bytes, Chinese7935EchoCatalogFailure? expected = null)
    {
        Assert.False(Chinese7935DecodedEchoCatalog.TryParseDecoded(bytes, Current, out var snapshot, out var failure));
        Assert.Null(snapshot); Assert.NotEqual(Chinese7935EchoCatalogFailure.None, failure);
        if (expected is not null) Assert.Equal(expected.Value, failure);
    }

    [Theory]
    [InlineData(7930)] [InlineData(7931)]
    public void ExplicitSupportedDeclaredVersionsKeepRealEmptyExport(int version)
    {
        var snapshot = Parse(Module(""), (Chinese7935EchoCatalogVersion)version);
        Assert.Equal((Chinese7935EchoCatalogVersion)version, snapshot.DeclaredVersion);
        Assert.Equal(0, snapshot.Count); Assert.Empty(snapshot.CopyThresholdRows()); Assert.False(snapshot.TryGetRow(1, out _));
    }

    [Fact]
    public void CompleteNamedFieldSetsKeepSourceRowsWithoutInventingContiguityOrLevels()
    {
        var input = Module(Row(9) + ",[2]={extra_attr_value=29,extra_attr_type=6,attr_value=20,attr_type=4,},");
        var snapshot = Parse(input);
        Assert.Equal(new[] { 9, 2 }, snapshot.Rows.ToArray().Select(row => row.Id));
        Assert.True(snapshot.TryGetRow(2, out var row)); Assert.Equal(new Chinese7935EchoCatalogRow(2, 4, 20, 6, 29), row);
        Assert.Equal(29U, Chinese7935EchoType6Thresholds.Calculate(snapshot.CopyThresholdRows(), [new(4, 20)]));
    }

    [Fact]
    public void DifferentRowIdsRetainDuplicatePrimaryKeysAndThresholds()
    {
        var snapshot = Parse(Module(Row(1, extra: "17") + "," + Row(2, extra: "29")));
        Assert.Equal(2, snapshot.Count);
        Assert.Equal(29U, Chinese7935EchoType6Thresholds.Calculate(snapshot.CopyThresholdRows(), [new(4, 10)]));
        Assert.Equal(0U, Chinese7935EchoType6Thresholds.Calculate(snapshot.CopyThresholdRows(), []));
    }

    [Fact]
    public void CommentsAndUtf8BomAreDataTriviaAndNeverStatements()
    {
        var text = "-- header\r\n--[==[ f(); hidden ret.nope = 8; ]==]\r\n" + Encoding.UTF8.GetString(Module(Row())) + "-- tail";
        var bytes = new byte[] { 0xef, 0xbb, 0xbf }.Concat(Encoding.UTF8.GetBytes(text)).ToArray();
        Assert.Equal(1, Parse(bytes).Count);
    }

    [Fact]
    public void InputAndProjectionMutationCannotChangeThePublishedSnapshot()
    {
        var bytes = Module(Row()); var snapshot = Parse(bytes); var digest = snapshot.ContentSha256;
        Array.Fill(bytes, (byte)'!'); var projection = snapshot.CopyThresholdRows(); projection[0] = new(99, 0, 6, 999);
        Assert.True(snapshot.TryGetRow(1, out var row)); Assert.Equal(17U, row.ExtraAttributeValue);
        Assert.Equal(17U, snapshot.CopyThresholdRows()[0].Bonus); Assert.Equal(digest, snapshot.ContentSha256);
        Assert.NotSame(projection, snapshot.CopyThresholdRows());
    }

    [Fact]
    public void NativeBackedWidthsIncludeSignedKeyMaximumAndUnsignedValueMaximum()
    {
        var snapshot = Parse(Module(Row(int.MaxValue, "2147483647", "4294967295", "2147483647", "4294967295")));
        Assert.Equal(new Chinese7935EchoCatalogRow(int.MaxValue, int.MaxValue, uint.MaxValue, int.MaxValue, uint.MaxValue), snapshot.Rows[0]);
        Assert.Equal(new Chinese7935EchoCatalogRow(1, 0, 0, 0, 0), Parse(Module(Row(1, "0", "0", "0", "0"))).Rows[0]);
    }
}

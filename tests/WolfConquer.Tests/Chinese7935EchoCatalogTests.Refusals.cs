using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7935EchoCatalogTests
{
    [Theory]
    [InlineData(0)] [InlineData(7899)] [InlineData(7935)] [InlineData(int.MaxValue)]
    public void UnsupportedDeclaredVersionNeverProducesASnapshot(int version)
    {
        Assert.False(Chinese7935DecodedEchoCatalog.TryParseDecoded(Module(""), (Chinese7935EchoCatalogVersion)version,
            out var snapshot, out var failure));
        Assert.Null(snapshot); Assert.Equal(Chinese7935EchoCatalogFailure.UnsupportedVersion, failure);
    }

    [Theory]
    [InlineData("0", "4", "10", "6", "17")]
    [InlineData("2147483648", "4", "10", "6", "17")]
    [InlineData("1", "2147483648", "10", "6", "17")]
    [InlineData("1", "4", "4294967296", "6", "17")]
    [InlineData("1", "4", "10", "2147483648", "17")]
    [InlineData("1", "4", "10", "6", "4294967296")]
    public void RowAndFieldWidthOverflowRefusesWithoutWrapping(string id, string type, string value, string extraType, string extra)
    {
        var row = "[" + id + "]={attr_type=" + type + ",attr_value=" + value + ",extra_attr_type=" + extraType + ",extra_attr_value=" + extra + "}";
        Refuses(Module(row), Chinese7935EchoCatalogFailure.NumericWidth);
    }

    [Theory]
    [InlineData("-1")] [InlineData("1.0")] [InlineData("1e2")] [InlineData("0x10")]
    [InlineData("nil")] [InlineData("true")] [InlineData("other")] [InlineData("'text'")]
    [InlineData("f()")] [InlineData("1+2")] [InlineData("{inner=1}")]
    public void ExecutableOrUnestablishedValueFormsNeverBecomeDefaults(string expression) => Refuses(Module(Row(value: expression)));

    [Fact]
    public void DuplicateCanonicalRowKeysAndDuplicateNamedFieldsRefuse()
    {
        Refuses(Module(Row() + "," + Row().Replace("[1]", "[01]", StringComparison.Ordinal)), Chinese7935EchoCatalogFailure.DuplicateRow);
        Refuses(Module(Row().Replace("extra_attr_value=17", "attr_type=4", StringComparison.Ordinal)), Chinese7935EchoCatalogFailure.DuplicateField);
    }

    [Theory]
    [InlineData("attr_type=4,extra_attr_type=6,extra_attr_value=17")]
    [InlineData("attr_type=4,attr_value=10,extra_attr_type=6,extra_attr_value=17,slot=5")]
    [InlineData("Attr_type=4,attr_value=10,extra_attr_type=6,extra_attr_value=17")]
    [InlineData("[1]=4,attr_value=10,extra_attr_type=6,extra_attr_value=17")]
    public void MissingUnknownCaseChangedOrIndexedFieldsRefuse(string fields) =>
        Refuses(Module("[1]={" + fields + "}"), Chinese7935EchoCatalogFailure.RowSchema);

    [Fact]
    public void WrongWrapperAliasesExportsAndTrailingStatementsRefuse()
    {
        var good = Encoding.UTF8.GetString(Module(Row()));
        foreach (var bad in new[]
        {
            good.Replace("local ret", "local other", StringComparison.Ordinal),
            good.Replace("return ret", "return other", StringComparison.Ordinal),
            good.Replace("cq_xuanbao_huying_extra_attr", "OtherRows", StringComparison.Ordinal),
            good.Replace("local ret = {}", "local ret = {seed=1}", StringComparison.Ordinal),
            good + " f();", good + ";", good.Replace("local ret", "function ret", StringComparison.Ordinal),
            good.Replace("attr_value=10,", "attr_value=10 ", StringComparison.Ordinal),
        }) Refuses(Encoding.UTF8.GetBytes(bad));
    }

    [Fact]
    public void EveryProperPrefixOfCompleteSyntheticModuleRefuses()
    {
        var bytes = Module(Row());
        for (var length = 0; length < bytes.Length; length++) Refuses(bytes[..length]);
    }

    [Fact]
    public void ExplicitOfflineBoundsKeepCompleteMaximumRowsAndRefuseNextRow()
    {
        var rows = string.Join(',', Enumerable.Range(1, Chinese7935DecodedEchoCatalog.MaximumRows).Select(id => Row(id)));
        Assert.Equal(Chinese7935DecodedEchoCatalog.MaximumRows, Parse(Module(rows)).Count);
        Refuses(Module(rows + "," + Row(Chinese7935DecodedEchoCatalog.MaximumRows + 1)), Chinese7935EchoCatalogFailure.LiteralBounds);
        Refuses(Module(Row(value: "9223372036854775808")), Chinese7935EchoCatalogFailure.LiteralBounds);
        Refuses(Module(Row(value: "00000000000000000001")), Chinese7935EchoCatalogFailure.LiteralBounds);
        Refuses(Encoding.UTF8.GetBytes("local " + new string('a', 129) + " = {};"), Chinese7935EchoCatalogFailure.LiteralBounds);
    }

    [Fact]
    public void ByteEncodingAndCommentBoundariesNeverPublishPartialRows()
    {
        var good = Module(Row()); var padded = good.Concat(new byte[Chinese7935DecodedEchoCatalog.MaximumBytes - good.Length]
            .Select(_ => (byte)' ')).ToArray();
        Assert.Equal(1, Parse(padded).Count);
        Refuses(padded.Concat(new[] { (byte)' ' }).ToArray(), Chinese7935EchoCatalogFailure.InputBounds);
        Refuses([0xff], Chinese7935EchoCatalogFailure.InvalidUtf8);
        Refuses(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(Encoding.UTF8.GetString(good))).ToArray(), Chinese7935EchoCatalogFailure.InvalidUtf8);
        Refuses(good.Concat(new byte[] { 0 }).ToArray());
        Refuses(good.Concat(Encoding.UTF8.GetBytes(" --[==[unterminated")).ToArray());
    }
}

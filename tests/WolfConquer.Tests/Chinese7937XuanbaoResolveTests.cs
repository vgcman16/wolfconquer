using System.Security.Cryptography;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoResolveTests
{
    [Fact]
    public void ExactSelectedRelationshipsCaptureImmutableFieldsAndExplicitBooleanBranch()
    {
        var source = Chinese7937XuanbaoResolveFixture.Gbk(Chinese7937XuanbaoResolveFixture.Module());
        var pin = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
        Assert.True(Chinese7937DecodedXuanbaoInfo.TryParseResolve(source, Chinese7937XuanbaoInfoVersion.Catalog7937,
            out var parsed, out var failure), failure.ToString());
        var snapshot = Assert.IsType<Chinese7937XuanbaoResolveSnapshot>(parsed);
        Assert.Equal(Chinese7937XuanbaoInfoFailure.None, failure);
        Assert.Equal(7937, snapshot.ClientVersion);
        Assert.Equal(Chinese7937XuanbaoInfoVersion.Catalog7937, snapshot.DeclaredVersion);
        Assert.Equal(pin, snapshot.ContentSha256);
        Assert.Equal(7U, snapshot.CommonNumberField); Assert.Equal(17U, snapshot.GoldNumberField);
        Assert.Equal(123U, snapshot.MaterialIdField);
        Assert.Equal(7U, snapshot.SelectNumber(false)); Assert.Equal(17U, snapshot.SelectNumber(true));
        var fields = snapshot.Fields.ToArray(); fields[0] = new("changed", 999);
        Array.Clear(source);
        Assert.Equal(new Chinese7937XuanbaoInfoField("CommonNum", 7), snapshot.Fields[0]);
        Assert.Equal(pin, snapshot.ContentSha256); Assert.Equal(17U, snapshot.SelectNumber(true));
    }

    [Fact]
    public void ZeroAndFullUInt32ValuesAreDataWithoutAnInferredSentinelOrDefault()
    {
        var snapshot = Chinese7937XuanbaoResolveFixture.Parse(Chinese7937XuanbaoResolveFixture.Module("0", "4294967295", "0"));
        Assert.Equal(0U, snapshot.SelectNumber(false)); Assert.Equal(uint.MaxValue, snapshot.SelectNumber(true));
        Assert.Equal(0U, snapshot.MaterialIdField);
    }

    [Fact]
    public void FieldSourceOrderIsRetainedWithoutChangingNamedBranchSelection()
    {
        var snapshot = Chinese7937XuanbaoResolveFixture.Parse(Chinese7937XuanbaoResolveFixture.ModuleWithFields(
            "MaterialID=123, GoldNum=17, CommonNum=7,"));
        Assert.Equal(new[] { "MaterialID", "GoldNum", "CommonNum" }, snapshot.Fields.ToArray().Select(row => row.Name));
        Assert.Equal(7U, snapshot.SelectNumber(false)); Assert.Equal(17U, snapshot.SelectNumber(true));
    }

    [Fact]
    public void CommentsStringsAndOtherDirectModuleAliasesDoNotSupplySelectedEvidence()
    {
        var source = "-- synthetic GetXuanBaoResolveNum if end\nlocal label='XuanBaoResolve = nil'\n" +
            Chinese7937XuanbaoResolveFixture.Module();
        source = source.Replace("return XuanBao\n", "UnrelatedAlias = XuanBao.UnrelatedGetter\nreturn XuanBao;\n", StringComparison.Ordinal);
        Assert.Equal(123U, Chinese7937XuanbaoResolveFixture.Parse(source).MaterialIdField);
        Chinese7937XuanbaoResolveFixture.Refused(source.Replace(Chinese7937XuanbaoResolveFixture.NumberGetter, "", StringComparison.Ordinal));
    }

    [Fact]
    public void LaterRefusalCannotChangeAnEarlierOwnedSnapshot()
    {
        var snapshot = Chinese7937XuanbaoResolveFixture.Parse(Chinese7937XuanbaoResolveFixture.Module());
        var fields = snapshot.Fields.ToArray(); var pin = snapshot.ContentSha256;
        Chinese7937XuanbaoResolveFixture.Refused(Chinese7937XuanbaoResolveFixture.Module("4294967296"));
        Assert.Equal(fields, snapshot.Fields.ToArray()); Assert.Equal(pin, snapshot.ContentSha256);
    }

    [Fact]
    public void UnsupportedVersionAndCaptureBoundsPublishNoSnapshot()
    {
        var source = Chinese7937XuanbaoResolveFixture.Gbk(Chinese7937XuanbaoResolveFixture.Module());
        Assert.False(Chinese7937DecodedXuanbaoInfo.TryParseResolve(source, Chinese7937XuanbaoInfoVersion.Catalog7931,
            out var snapshot, out var failure));
        Assert.Null(snapshot); Assert.Equal(Chinese7937XuanbaoInfoFailure.UnsupportedVersion, failure);
        foreach (var invalid in new[] { Array.Empty<byte>(), new byte[Chinese7937DecodedXuanbaoInfo.MaximumBytes + 1] })
        {
            Assert.False(Chinese7937DecodedXuanbaoInfo.TryParseResolve(invalid, Chinese7937XuanbaoInfoVersion.Catalog7937,
                out snapshot, out failure));
            Assert.Null(snapshot); Assert.Equal(Chinese7937XuanbaoInfoFailure.InputBounds, failure);
        }
    }

    [Fact]
    public void InvalidGbkBomNulAndLexerBoundsRefuseWithoutPartialData()
    {
        foreach (var bytes in new byte[][] { [0x81], [0xef, 0xbb, 0xbf], [0xff, 0xfe], [0xfe, 0xff] })
        {
            Assert.False(Chinese7937DecodedXuanbaoInfo.TryParseResolve(bytes, Chinese7937XuanbaoInfoVersion.Catalog7937,
                out var snapshot, out var failure));
            Assert.Null(snapshot); Assert.Equal(Chinese7937XuanbaoInfoFailure.InvalidGbk, failure);
        }
        Assert.Equal(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax,
            Chinese7937XuanbaoResolveFixture.Refused(Chinese7937XuanbaoResolveFixture.Module() + "\0"));
        Assert.Equal(Chinese7937XuanbaoInfoFailure.LiteralBounds, Chinese7937XuanbaoResolveFixture.Refused(
            new string(';', Chinese7937DecodedXuanbaoInfo.MaximumTokens) + Chinese7937XuanbaoResolveFixture.Module()));
        Assert.Equal(Chinese7937XuanbaoInfoFailure.LiteralBounds, Chinese7937XuanbaoResolveFixture.Refused(
            "local " + new string('a', Chinese7937DecodedXuanbaoInfo.MaximumIdentifierLength + 1) + "=nil\n" + Chinese7937XuanbaoResolveFixture.Module()));
    }
}

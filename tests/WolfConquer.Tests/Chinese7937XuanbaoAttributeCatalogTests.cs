using System.Collections;
using System.Security.Cryptography;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoAttributeCatalogTests
{
    private const Chinese7937XuanbaoInfoVersion Current = Chinese7937XuanbaoInfoVersion.Catalog7937;

    [Fact]
    public void ParsedCatalogRetainsSourceOrderAndSparseZeroFalsePresence()
    {
        var text = Chinese7937XuanbaoAttributeFixture.Module(
            "[20] = { [9] = { ScoreLimit8=4294967295, ScoreLimit0=0, }, [1] = { ScoreLimit3=4, }, },\n" +
            "[10] = { [2] = { ScoreLimit7=6, }, },",
            "[20] = { [9] = false, [1] = true, }, [10] = { [2] = true, },");
        var catalog = Chinese7937XuanbaoAttributeFixture.Parse(text);
        Assert.Equal(7937, catalog.ClientVersion);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Chinese7937XuanbaoAttributeFixture.Gbk(text))).ToLowerInvariant(), catalog.ContentSha256);
        Assert.Equal(new uint[] { 20, 10 }, catalog.GoldLimitItemTypes);
        Assert.Equal(new[] { new Chinese7937XuanbaoScoreLimit(20, 9, 8, uint.MaxValue),
            new Chinese7937XuanbaoScoreLimit(20, 9, 0, 0), new Chinese7937XuanbaoScoreLimit(20, 1, 3, 4),
            new Chinese7937XuanbaoScoreLimit(10, 2, 7, 6) }, catalog.ScoreLimits);
        Assert.Equal(new[] { new Chinese7937XuanbaoGoldLimitFlag(20, 9, false),
            new Chinese7937XuanbaoGoldLimitFlag(20, 1, true), new Chinese7937XuanbaoGoldLimitFlag(10, 2, true) }, catalog.GoldLimitFlags);
        Assert.True(catalog.TryGetScoreLimit(20, 9, 0, out var zero)); Assert.Equal(0U, zero);
        Assert.True(catalog.TryGetScoreLimit(20, 9, 8, out var maximum)); Assert.Equal(uint.MaxValue, maximum);
        Assert.False(catalog.TryGetScoreLimit(20, 9, 1, out _));
        Assert.True(catalog.TryGetGoldLimitFlag(20, 9, out var presentFalse)); Assert.False(presentFalse);
        Assert.False(catalog.TryGetGoldLimitFlag(20, 8, out var absent)); Assert.False(absent);
        Assert.True(catalog.HasGoldLimitItem(20)); Assert.False(catalog.HasGoldLimitItem(99));
        Assert.False(catalog.TryGetScoreLimit(99, 9, 0, out _));
        Assert.False(catalog.TryGetScoreLimit(20, 99, 0, out _));
    }

    [Fact]
    public void UInt32ItemAndAttributeKeysDoNotBecomeSignedOrImplicitlyAbsent()
    {
        var catalog = Chinese7937XuanbaoAttributeFixture.Parse(Chinese7937XuanbaoAttributeFixture.Module(
            "[0] = { [4294967295] = { ScoreLimit0=0, }, }, [4294967295] = { [0] = { ScoreLimit8=1, }, },",
            "[0] = { [4294967295] = false, }, [4294967295] = { [0] = true, },"));
        Assert.True(catalog.TryGetScoreLimit(0, uint.MaxValue, 0, out var value)); Assert.Equal(0U, value);
        Assert.True(catalog.TryGetGoldLimitFlag(0, uint.MaxValue, out var flag)); Assert.False(flag);
        Assert.True(catalog.TryGetGoldLimitFlag(uint.MaxValue, 0, out flag)); Assert.True(flag);
        Assert.True(catalog.HasGoldLimitItem(0));
    }

    [Fact]
    public void CallerBytesAndCollectionsCannotMutateReturnedData()
    {
        var bytes = Chinese7937XuanbaoAttributeFixture.Gbk(Chinese7937XuanbaoAttributeFixture.Module());
        Assert.True(Chinese7937DecodedXuanbaoInfo.TryParseAttributeCatalog(bytes, Current, out var result, out _));
        var catalog = Assert.IsType<Chinese7937XuanbaoAttributeCatalog>(result);
        var digest = catalog.ContentSha256;
        Array.Fill(bytes, (byte)'!');
        Assert.Throws<NotSupportedException>(() => { ((IList)catalog.ScoreLimits)[0] = default(Chinese7937XuanbaoScoreLimit); });
        Assert.Throws<NotSupportedException>(() => ((IList)catalog.GoldLimitFlags).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList)catalog.GoldLimitItemTypes).Clear());
        Assert.Equal(digest, catalog.ContentSha256);
        Assert.True(catalog.TryGetScoreLimit(10, 1, 8, out var value)); Assert.Equal(100U, value);
        Assert.True(catalog.TryGetGoldLimitFlag(10, 1, out var flag)); Assert.True(flag);
    }

    [Fact]
    public void CommentsStringsAndUnrelatedLuaDoNotBecomeSelectedBindingsOrExecution()
    {
        var prefix = "-- 合成数据，不执行Lua。\n--[=[ local XuanBaoLimitAttri = {} ]=]\n" +
            "local function Opaque()\nlocal fake = 'XuanBaoScoreGradeStep = nil'\n" +
            "local other = [=[GetXuanBaoGoldLimitAttri = bad]=]\nreturn fake,other\nend\n";
        var catalog = Chinese7937XuanbaoAttributeFixture.Parse(prefix + Chinese7937XuanbaoAttributeFixture.Module());
        Assert.Equal(2, catalog.ScoreLimits.Count);
        Assert.Equal(2, catalog.GoldLimitFlags.Count);
    }

    [Fact]
    public void LaterRejectedParseDoesNotChangeEarlierCatalog()
    {
        var catalog = Chinese7937XuanbaoAttributeFixture.Parse(Chinese7937XuanbaoAttributeFixture.Module());
        var digest = catalog.ContentSha256;
        Refuses(Chinese7937XuanbaoAttributeFixture.Module(goldRows: "[10] = { [1] = 0, },"));
        Assert.Equal(digest, catalog.ContentSha256);
        Assert.True(catalog.TryGetGoldLimitFlag(10, 1, out var flag)); Assert.True(flag);
    }

    [Fact]
    public void FiniteFieldBoundsAllowEdgeAndRefuseAnAdditionalGoldFlag()
    {
        var edge = string.Join("\n", Enumerable.Range(1, Chinese7937DecodedXuanbaoInfo.MaximumFields)
            .Select(index => "[" + index + "] = { [1] = true, },"));
        Assert.Equal(Chinese7937DecodedXuanbaoInfo.MaximumFields,
            Chinese7937XuanbaoAttributeFixture.Parse(Chinese7937XuanbaoAttributeFixture.Module(goldRows: edge)).GoldLimitFlags.Count);
        Refuses(Chinese7937XuanbaoAttributeFixture.Module(goldRows: edge + "\n[2000] = { [1] = true, },"),
            Chinese7937XuanbaoInfoFailure.LiteralBounds);
    }

    [Fact]
    public void FiniteByteAndTokenBoundsDoNotPublishPartialCatalogs()
    {
        var bytes = Chinese7937XuanbaoAttributeFixture.Gbk(Chinese7937XuanbaoAttributeFixture.Module());
        var padded = new byte[Chinese7937DecodedXuanbaoInfo.MaximumBytes];
        Array.Fill(padded, (byte)' '); bytes.CopyTo(padded, 0);
        Assert.True(Chinese7937DecodedXuanbaoInfo.TryParseAttributeCatalog(padded, Current, out var catalog, out _));
        Assert.NotNull(catalog);
        Refuses([.. padded, (byte)' '], Chinese7937XuanbaoInfoFailure.InputBounds);
        Refuses(new string(';', Chinese7937DecodedXuanbaoInfo.MaximumTokens + 1) + Chinese7937XuanbaoAttributeFixture.Module(),
            Chinese7937XuanbaoInfoFailure.LiteralBounds);
    }
}

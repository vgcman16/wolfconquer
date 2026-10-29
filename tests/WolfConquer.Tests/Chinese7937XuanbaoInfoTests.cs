using System.Security.Cryptography;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoInfoTests
{
    private const Chinese7937XuanbaoInfoVersion Current = Chinese7937XuanbaoInfoVersion.Catalog7937;
    private const string Getter = "local function GetXuanBaoEchoInfo(key)\n    return XuanBaoEchoInfo[key]\nend\n";
    private const string Unload = "local function Unload()\n    XuanBaoEchoInfo = nil\n    OpaqueOther = nil\nend\n";
    private const string Export = "local XuanBao = {\n    Other = function(value) if value then return {a=1,b=2} end return nil end,\n    GetXuanBaoEchoInfo = GetXuanBaoEchoInfo,\n    Unload = Unload,\n}\n";
    private const string Alias = "XuanBao_GetXuanBaoEchoInfo = XuanBao.GetXuanBaoEchoInfo\n";
    private static string Literal(string fields) => "local XuanBaoEchoInfo = {\n" + fields + "\n}\n";
    private static string Module(string fields = "[\"alpha\"] = 7,") => Literal(fields) + Getter + Unload + Export + Alias;
    private static byte[] Gbk(string text) => ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936).GetBytes(text);
    private static Chinese7937XuanbaoInfoSnapshot Parse(string text, Chinese7937XuanbaoInfoVersion version = Current)
    {
        Assert.True(Chinese7937DecodedXuanbaoInfo.TryParseDecoded(Gbk(text), version, out var snapshot, out var failure), failure.ToString());
        return Assert.IsType<Chinese7937XuanbaoInfoSnapshot>(snapshot);
    }
    private static void Refuses(string text, Chinese7937XuanbaoInfoFailure? expected = null) => Refuses(Gbk(text), expected);
    private static void Refuses(byte[] bytes, Chinese7937XuanbaoInfoFailure? expected = null)
    {
        Assert.False(Chinese7937DecodedXuanbaoInfo.TryParseDecoded(bytes, Current, out var snapshot, out var failure));
        Assert.Null(snapshot); Assert.NotEqual(Chinese7937XuanbaoInfoFailure.None, failure);
        if (expected is not null) Assert.Equal(expected.Value, failure);
    }

    [Theory]
    [InlineData(7931)] [InlineData(7937)]
    public void CompleteSyntheticRelationshipPreservesDeclaredVersionAndUInt32Data(int version)
    {
        var snapshot = Parse(Module("[\"last\"] = 4294967295,\n[\"first\"] = 0,"), (Chinese7937XuanbaoInfoVersion)version);
        Assert.Equal((Chinese7937XuanbaoInfoVersion)version, snapshot.DeclaredVersion);
        Assert.Equal(new[] { "last", "first" }, snapshot.Fields.ToArray().Select(field => field.Name));
        Assert.True(snapshot.TryGetValue("last", out var last)); Assert.Equal(uint.MaxValue, last);
        Assert.True(snapshot.TryGetValue("first", out var first)); Assert.Equal(0U, first);
    }

    [Fact]
    public void ExactCaseAndMissingLookupRemainDistinctWithoutFallbackValues()
    {
        var snapshot = Parse(Module("[\"Alpha\"] = 2,\n[\"alpha\"] = 3,"));
        Assert.True(snapshot.TryGetValue("Alpha", out var upper)); Assert.Equal(2U, upper);
        Assert.True(snapshot.TryGetValue("alpha", out var lower)); Assert.Equal(3U, lower);
        Assert.False(snapshot.TryGetValue("ALPHA", out var absent)); Assert.Equal(0U, absent);
        Assert.False(snapshot.TryGetValue(null, out _)); Assert.Equal(2, snapshot.Count);
    }

    [Fact]
    public void CommentsStringsAndUnrelatedLuaAreOpaqueRatherThanEvaluated()
    {
        var prefix = "-- GBK注释：这是合成测试，不执行Lua。\n" +
            "--[==[ local XuanBaoEchoInfo = {malformed}; XuanBao_GetXuanBaoEchoInfo() ]==]\n" +
            "local function Opaque()\n" +
            " local fake = [=[local function GetXuanBaoEchoInfo(key) evil() end]=]\n" +
            " local quoted = 'XuanBaoEchoInfo = nil'\n" +
            " local n = 0\n for i=1,2 do n=n+i end\n while n<3 do n=n+1 end\n" +
            " repeat n=n-1 until n==0\n return fake,quoted\nend\n";
        var snapshot = Parse(prefix + Module("-- selected comment\n[\"alpha\"] = 7, -- 数据\n"));
        Assert.True(snapshot.TryGetValue("alpha", out var value)); Assert.Equal(7U, value);
        Assert.Equal(1, snapshot.Count);
    }

    [Fact]
    public void InputAndCopiedRowsCannotMutateAnExistingSnapshot()
    {
        var bytes = Gbk(Module()); var expected = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.True(Chinese7937DecodedXuanbaoInfo.TryParseDecoded(bytes, Current, out var parsed, out _));
        var snapshot = Assert.IsType<Chinese7937XuanbaoInfoSnapshot>(parsed);
        Array.Fill(bytes, (byte)'!'); var copy = snapshot.Fields.ToArray(); copy[0] = new("changed", 999);
        Assert.Equal(expected, snapshot.ContentSha256);
        Assert.True(snapshot.TryGetValue("alpha", out var value)); Assert.Equal(7U, value);
        Assert.Equal(new Chinese7937XuanbaoInfoField("alpha", 7), snapshot.Fields[0]);
    }

    [Fact]
    public void FiniteFieldAndNameBoundsIncludeTheirExactUpperEdge()
    {
        var key = new string('a', Chinese7937DecodedXuanbaoInfo.MaximumIdentifierLength);
        Assert.True(Parse(Module("[\"" + key + "\"] = 1,")).TryGetValue(key, out _));
        var fields = string.Join("\n", Enumerable.Range(0, Chinese7937DecodedXuanbaoInfo.MaximumFields).Select(index => "[\"f" + index + "\"] = 1,"));
        Assert.Equal(Chinese7937DecodedXuanbaoInfo.MaximumFields, Parse(Module(fields)).Count);
        Refuses(Module(fields + "\n[\"overflow\"] = 1,"), Chinese7937XuanbaoInfoFailure.LiteralBounds);
        Refuses(Module("[\"" + key + "a\"] = 1,"), Chinese7937XuanbaoInfoFailure.LiteralBounds);
    }

    [Fact]
    public void InputAndTokenBoundsRefuseBeforePublishingAndAllowExactByteCap()
    {
        var module = Module(); var exact = Gbk(module + new string(' ', Chinese7937DecodedXuanbaoInfo.MaximumBytes - Gbk(module).Length));
        Assert.True(Chinese7937DecodedXuanbaoInfo.TryParseDecoded(exact, Current, out var snapshot, out _));
        Assert.NotNull(snapshot);
        Refuses([.. exact, (byte)' '], Chinese7937XuanbaoInfoFailure.InputBounds);
        Refuses(new string(';', Chinese7937DecodedXuanbaoInfo.MaximumTokens + 1) + "\n" + module,
            Chinese7937XuanbaoInfoFailure.LiteralBounds);
    }

    [Fact]
    public void StrictGbkHasNoUnicodeBomOrMalformedByteRepair()
    {
        Refuses([0x81], Chinese7937XuanbaoInfoFailure.InvalidGbk);
        Refuses([0xef, 0xbb, 0xbf, .. Gbk(Module())], Chinese7937XuanbaoInfoFailure.InvalidGbk);
        Refuses([0xff, 0xfe, .. Gbk(Module())], Chinese7937XuanbaoInfoFailure.InvalidGbk);
        Refuses([.. Gbk(Module()), 0], Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
        Refuses([], Chinese7937XuanbaoInfoFailure.InputBounds);
        Assert.False(Chinese7937DecodedXuanbaoInfo.TryParseDecoded(Gbk(Module()), (Chinese7937XuanbaoInfoVersion)0,
            out var snapshot, out var failure));
        Assert.Null(snapshot); Assert.Equal(Chinese7937XuanbaoInfoFailure.UnsupportedVersion, failure);
    }
}

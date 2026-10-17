using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientStringFileTests
{
    [Fact]
    public void RoundTripsCp936FileBytesWithoutChanges()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var bytes = encoding.GetBytes("[NpcType1]\r\nName=杂货店老板\r\nLookColor=0xffffffff\r\n");

        var file = ClientStringFile.Parse(bytes, encoding);

        Assert.Equal(bytes, file.ToBytes());
    }

    [Fact]
    public void RoundTripsUtf8FileBytesWithoutChanges()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8);
        var bytes = encoding.GetBytes("STR_BID_SYSTEM_NAME=拍卖行\r\nSTR_BID_EMONEY_NAME=#77\r\n");

        var file = ClientStringFile.Parse(bytes, encoding);

        Assert.Equal(bytes, file.ToBytes());
    }

    [Fact]
    public void ParsesKeyValuesWhileSkippingSectionsAndComments()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var bytes = encoding.GetBytes(";comment\r\n//also comment\r\n[program]\r\npatch = update.exe\r\nConnectedErr = 本地网络连接异常\r\n");

        var file = ClientStringFile.Parse(bytes, encoding, ";", "//");

        Assert.Collection(
            file.KeyValues(),
            patch =>
            {
                Assert.Equal("patch", patch.Key);
                Assert.Equal("update.exe", patch.Value);
            },
            connectedErr =>
            {
                Assert.Equal("ConnectedErr", connectedErr.Key);
                Assert.Equal("本地网络连接异常", connectedErr.Value);
            });
    }

    [Fact]
    public void SetValuePreservesSpacingLineEndingsAndEncoding()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var file = ClientStringFile.Parse(encoding.GetBytes("\tpatch = update.exe\r\nConnectedErr = 本地网络连接异常\r\n"), encoding);

        Assert.True(file.SetValue("patch", "WolfConquer.exe"));

        var rewritten = encoding.GetString(file.ToBytes());
        Assert.Equal("\tpatch = WolfConquer.exe\r\nConnectedErr = 本地网络连接异常\r\n", rewritten);
    }

    [Fact]
    public void SetValueRemovesPaddingFromBlankEqualsValues()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var file = ClientStringFile.Parse(encoding.GetBytes("Name=    \r\n"), encoding);

        Assert.True(file.SetValue("Name", "Scarecrow"));

        var rewritten = encoding.GetString(file.ToBytes());
        Assert.Equal("Name=Scarecrow\r\n", rewritten);
    }

    [Fact]
    public void TargetedReplacementsPreserveMixedLineEndings()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var file = ClientStringFile.Parse(encoding.GetBytes("Name=旧名\r\n10 old\n20 old\rLast=结尾"), encoding);

        Assert.True(file.SetValue("Name", "新名"));
        Assert.True(file.SetLine("20", "20 new"));

        var rewritten = encoding.GetString(file.ToBytes());
        Assert.Equal("Name=新名\r\n10 old\n20 new\rLast=结尾", rewritten);
    }

    [Fact]
    public void SetSectionValueOnlyUpdatesTheRequestedSection()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var file = ClientStringFile.Parse(encoding.GetBytes("[ChatTips0001]\r\nnote=old\r\n[ChatTips0002]\r\nnote=old\r\n"), encoding);

        Assert.True(file.SetSectionValue("ChatTips0002", "note", "new"));

        var rewritten = encoding.GetString(file.ToBytes());
        Assert.Equal("[ChatTips0001]\r\nnote=old\r\n[ChatTips0002]\r\nnote=new\r\n", rewritten);
    }

    [Fact]
    public void SetValueDefaultsToFirstDuplicateOccurrence()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var file = ClientStringFile.Parse(encoding.GetBytes("DESC=first\r\nDESC=second\r\n"), encoding);

        Assert.True(file.SetValue("DESC", "updated"));

        var rewritten = encoding.GetString(file.ToBytes());
        Assert.Equal("DESC=updated\r\nDESC=second\r\n", rewritten);
    }

    [Fact]
    public void SetValueCanUpdateSpecificDuplicateOccurrence()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var file = ClientStringFile.Parse(encoding.GetBytes("DESC=first\r\nDESC=second\r\nDESC=third\r\n"), encoding);

        Assert.True(file.SetValue("DESC", "updated", occurrence: 2));

        var rewritten = encoding.GetString(file.ToBytes());
        Assert.Equal("DESC=first\r\nDESC=updated\r\nDESC=third\r\n", rewritten);
    }

    [Fact]
    public void SetSectionValueCanUpdateSpecificDuplicateOccurrence()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var file = ClientStringFile.Parse(encoding.GetBytes("[15]\r\nDESC=first\r\nDESC=second\r\n[16]\r\nDESC=third\r\n"), encoding);

        Assert.True(file.SetSectionValue("15", "DESC", "updated", occurrence: 2));

        var rewritten = encoding.GetString(file.ToBytes());
        Assert.Equal("[15]\r\nDESC=first\r\nDESC=updated\r\n[16]\r\nDESC=third\r\n", rewritten);
    }

    [Fact]
    public void SetSectionValueCanTargetKeyOccurrencesAcrossDuplicateSections()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var file = ClientStringFile.Parse(
            encoding.GetBytes(
                "[33]\r\n" +
                "Name=\u7b2c\u4e00\u4e2a\u6d3b\u52a8\r\n" +
                "Text1=\u7b2c\u4e00\u6bb5\r\n" +
                "[34]\r\n" +
                "Name=\u5176\u4ed6\u6d3b\u52a8\r\n" +
                "[33]\r\n" +
                "Name=\u7b2c\u4e8c\u4e2a\u6d3b\u52a8\r\n" +
                "Text1=\u7b2c\u4e8c\u6bb5\r\n"),
            encoding);

        Assert.True(file.SetSectionValue("33", "Name", "Second Activity", occurrence: 2));
        Assert.True(file.SetSectionValue("33", "Text1", "Second Description", occurrence: 2));

        var rewritten = encoding.GetString(file.ToBytes());
        Assert.Equal(
            "[33]\r\n" +
            "Name=\u7b2c\u4e00\u4e2a\u6d3b\u52a8\r\n" +
            "Text1=\u7b2c\u4e00\u6bb5\r\n" +
            "[34]\r\n" +
            "Name=\u5176\u4ed6\u6d3b\u52a8\r\n" +
            "[33]\r\n" +
            "Name=Second Activity\r\n" +
            "Text1=Second Description\r\n",
            rewritten);
    }

    [Fact]
    public void SetSectionValuePreservesWhitespaceSeparatedRows()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var file = ClientStringFile.Parse(encoding.GetBytes("[Type0]\r\n1 old\r\n2 old\r\n"), encoding);

        Assert.True(file.SetSectionValue("Type0", "2", "Deputy Leader"));

        var rewritten = encoding.GetString(file.ToBytes());
        Assert.Equal("[Type0]\r\n1 old\r\n2 Deputy Leader\r\n", rewritten);
    }

    [Fact]
    public void SetLineCanReplaceWholeWhitespaceSeparatedRows()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var file = ClientStringFile.Parse(encoding.GetBytes("  red 0xffff0000\r\ngreen 0xff00ff00\r\n"), encoding);

        Assert.True(file.SetLine("red", "Red 0xffff0000"));

        var rewritten = encoding.GetString(file.ToBytes());
        Assert.Equal("  Red 0xffff0000\r\ngreen 0xff00ff00\r\n", rewritten);
    }

    [Fact]
    public void SetLineCanReplaceWholeWhitespaceSeparatedRowsContainingEquals()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8);
        var file = ClientStringFile.Parse(encoding.GetBytes("9 old\r\n10 >= \u666e\u901a\r\n11 >= \u826f\u597d"), encoding);

        Assert.True(file.SetLine("10", "10 Peerless All stages at least Normal"));

        var rewritten = encoding.GetString(file.ToBytes());
        Assert.Equal("9 old\r\n10 Peerless All stages at least Normal\r\n11 >= \u826f\u597d", rewritten);
    }

    [Fact]
    public void SetLineCanReplaceExactRowsWithoutWhitespace()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var file = ClientStringFile.Parse(encoding.GetBytes("1,01,old\r\n1,02,other"), encoding);

        Assert.True(file.SetLine("1,01", "1,01,New Name"));

        var rewritten = encoding.GetString(file.ToBytes());
        Assert.Equal("1,01,New Name\r\n1,02,other", rewritten);
    }

    [Fact]
    public void SetLineUsesCommaKeyBoundary()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var file = ClientStringFile.Parse(encoding.GetBytes("1,010,wrong\r\n1,01,old"), encoding);

        Assert.True(file.SetLine("1,01", "1,01,new"));

        var rewritten = encoding.GetString(file.ToBytes());
        Assert.Equal("1,010,wrong\r\n1,01,new", rewritten);
    }

    [Fact]
    public void SetSectionValueReturnsFalseWhenOccurrenceIsMissing()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var file = ClientStringFile.Parse(encoding.GetBytes("[15]\r\nDESC=first\r\nDESC=second\r\n"), encoding);

        Assert.False(file.SetSectionValue("15", "DESC", "updated", occurrence: 3));

        var rewritten = encoding.GetString(file.ToBytes());
        Assert.Equal("[15]\r\nDESC=first\r\nDESC=second\r\n", rewritten);
    }

    [Fact]
    public void SetValueReturnsFalseWhenOccurrenceIsMissing()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var file = ClientStringFile.Parse(encoding.GetBytes("DESC=first\r\nDESC=second\r\n"), encoding);

        Assert.False(file.SetValue("DESC", "updated", occurrence: 3));

        var rewritten = encoding.GetString(file.ToBytes());
        Assert.Equal("DESC=first\r\nDESC=second\r\n", rewritten);
    }

    [Fact]
    public void PreservesDuplicateKeysAndEmptyValues()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8);
        var file = ClientStringFile.Parse(encoding.GetBytes("STR_EMPTY=\r\nSTR_EMPTY=second\r\n"), encoding);

        Assert.Collection(
            file.KeyValues(),
            first =>
            {
                Assert.Equal("STR_EMPTY", first.Key);
                Assert.Equal(string.Empty, first.Value);
            },
            second =>
            {
                Assert.Equal("STR_EMPTY", second.Key);
                Assert.Equal("second", second.Value);
            });
    }
}

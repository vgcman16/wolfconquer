using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class ClientConfigParserTests
{
    private const string RayzoRow = "2 TestSrv 3935 0222 0214 0001 0000 1 3 127.0.0.1 9220";
    private const string RealmRow = "101 Realm1 3935 0328 0198 0003 0003 1 0 198.51.100.16 9221";

    [Fact]
    public void ParseReadsLiveLikeRowsAndPreservesMetadata()
    {
        var records = ClientConfigParser.Parse(
            $"  \n{RayzoRow}\n{RealmRow}\n",
            "client_config.ini");

        Assert.Equal(2, records.Count);

        var rayzo = records[0];
        Assert.Equal(2, rayzo.ServerId);
        Assert.Equal("TestSrv", rayzo.ServerName);
        Assert.Equal(3935, rayzo.ClientBuild);
        Assert.Equal("0222", rayzo.LoginXRaw);
        Assert.Equal(222, rayzo.LoginX);
        Assert.Equal("0214", rayzo.LoginYRaw);
        Assert.Equal(214, rayzo.LoginY);
        Assert.Equal("0001", rayzo.MapXRaw);
        Assert.Equal(1, rayzo.MapX);
        Assert.Equal("0000", rayzo.MapYRaw);
        Assert.Equal(0, rayzo.MapY);
        Assert.Equal(1, rayzo.Region);
        Assert.Equal(3, rayzo.Status);
        Assert.Equal("127.0.0.1", rayzo.Host);
        Assert.Equal(9220, rayzo.Port);
        Assert.Equal("client_config.ini", rayzo.SourceFile);
        Assert.Equal(2, rayzo.LineNumber);
        Assert.Equal(RayzoRow, rayzo.RawLine);
        Assert.Equal(
            ["2", "TestSrv", "3935", "0222", "0214", "0001", "0000", "1", "3", "127.0.0.1", "9220"],
            rayzo.Fields);

        var realm = records[1];
        Assert.Equal(101, realm.ServerId);
        Assert.Equal("Realm1", realm.ServerName);
        Assert.Equal(9221, realm.Port);
        Assert.Equal(3, realm.MapX);
        Assert.Equal(3, realm.MapY);
        Assert.Equal(3, realm.LineNumber);
    }

    [Fact]
    public void ParseFileReadsFromDiskUsingFileNameAsSource()
    {
        var path = Path.Combine(Path.GetTempPath(), $"client_config-{Guid.NewGuid():N}.ini");
        File.WriteAllText(path, $"{RayzoRow}\n");

        try
        {
            var record = Assert.Single(ClientConfigParser.ParseFile(path));

            Assert.Equal(Path.GetFileName(path), record.SourceFile);
            Assert.Equal(1, record.LineNumber);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseFileRejectsMissingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-client-config-{Guid.NewGuid():N}.ini");

        Assert.Throws<FileNotFoundException>(() => ClientConfigParser.ParseFile(path));
    }

    [Theory]
    [InlineData("client_config.ini", 0)]
    [InlineData("client_config.ini", -1)]
    public void ParseRowRejectsInvalidLineNumber(string sourceFile, int lineNumber)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ClientConfigParser.ParseRow(sourceFile, lineNumber, RayzoRow));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ParseRowRejectsInvalidSourceFile(string sourceFile)
    {
        Assert.Throws<ArgumentException>(() => ClientConfigParser.ParseRow(sourceFile, 1, RayzoRow));
    }

    [Theory]
    [InlineData("2 TestSrv 3935 0222 0214 0001 0000 1 3 127.0.0.1")]
    [InlineData("2 TestSrv 3935 0222 0214 0001 0000 1 3 127.0.0.1 9220 extra")]
    public void ParseRowRejectsWrongFieldCount(string row)
    {
        Assert.Throws<FormatException>(() => ClientConfigParser.ParseRow("client_config.ini", 1, row));
    }

    [Theory]
    [InlineData("bad TestSrv 3935 0222 0214 0001 0000 1 3 127.0.0.1 9220")]
    [InlineData("2 TestSrv bad 0222 0214 0001 0000 1 3 127.0.0.1 9220")]
    [InlineData("2 TestSrv 3935 bad 0214 0001 0000 1 3 127.0.0.1 9220")]
    [InlineData("2 TestSrv 3935 0222 bad 0001 0000 1 3 127.0.0.1 9220")]
    [InlineData("2 TestSrv 3935 0222 0214 bad 0000 1 3 127.0.0.1 9220")]
    [InlineData("2 TestSrv 3935 0222 0214 0001 bad 1 3 127.0.0.1 9220")]
    [InlineData("2 TestSrv 3935 0222 0214 0001 0000 bad 3 127.0.0.1 9220")]
    [InlineData("2 TestSrv 3935 0222 0214 0001 0000 1 bad 127.0.0.1 9220")]
    [InlineData("2 TestSrv 3935 0222 0214 0001 0000 1 3 127.0.0.1 bad")]
    public void ParseRowRejectsInvalidNumericFields(string row)
    {
        Assert.Throws<FormatException>(() => ClientConfigParser.ParseRow("client_config.ini", 1, row));
    }
}

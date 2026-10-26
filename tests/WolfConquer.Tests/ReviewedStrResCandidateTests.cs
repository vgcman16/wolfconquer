using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ReviewedStrResCandidateTests
{
    private static readonly Encoding Encoding936 = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);

    [Theory]
    [InlineData("www.example.invalid/files")]
    [InlineData("https://example.invalid/client?id=7")]
    [InlineData(@"C:\Client\Play.exe")]
    [InlineData(@"\\host\share\client.exe")]
    [InlineData("127.0.0.1:9958")]
    [InlineData(@"ini\server38.dat")]
    [InlineData("ini/server38.dat")]
    [InlineData("<link|0xff0000ff|1|[https://example.invalid/help]|link>")]
    public void ReviewedHumanTextPreservesCompleteEmbeddedDataAndUntouchedPhysicalBytes(string fragment)
    {
        var before = Encoding936.GetBytes($";\u4fdd\u6301\r\n[0]\n  90001 = \u6253\u5f00{fragment}\r\n90002=\u4fdd\u7559\nTail=ASCII");
        var preserved = before.ToArray();
        ClientTextReplacement[] edits = [new("90001", $"Open {fragment}")];

        var candidate = SourceBackedStrResTranslator.BuildReviewedFileCandidate(before, edits);

        var expected = Encoding936.GetBytes($";\u4fdd\u6301\r\n[0]\n  90001 = Open {fragment}\r\n90002=\u4fdd\u7559\nTail=ASCII");
        Assert.Equal(expected, candidate.ToArray());
        Assert.Equal(preserved, before);
        Assert.Equal(new ClientPatchFileResult("ini/StrRes.ini", 1, 0, true), candidate.Result);
        Assert.Equal(new[] { "90001", "90002", "Tail" }, ClientStringFile.Parse(candidate.ToArray(), Encoding936).KeyValues().Select(row => row.Key));
        edits[0] = new("90001", "Caller later replaced the catalog");
        Assert.Equal(expected, candidate.ToArray());
        Assert.False(SourceBackedStrResTranslator.BuildReviewedFileCandidate(expected, [new("90001", $"Open {fragment}")]).Result.Changed);
    }

    [Theory]
    [InlineData("www.example.invalid/old", "www.example.invalid/new")]
    [InlineData("https://example.invalid/old", "https://example.invalid/new")]
    [InlineData(@"C:\Client\Play.exe", @"C:\Other\Play.exe")]
    [InlineData(@"\\host\share\old.exe", @"\\host\share\new.exe")]
    [InlineData("127.0.0.1:9958", "127.0.0.1:9959")]
    [InlineData(@"ini\server38.dat", @"ini\other.dat")]
    [InlineData("<link|0xff0000ff|1|[https://example.invalid/help]|link>", "<link|0xff0000ff|2|[https://example.invalid/help]|link>")]
    public void ReviewedHumanTextCannotChangeCompleteProtectedFragment(string beforeFragment, string afterFragment)
    {
        var bytes = Encoding936.GetBytes($"90001=\u6253\u5f00 {beforeFragment}\r\n");
        var original = bytes.ToArray();

        Assert.Throws<InvalidDataException>(() => SourceBackedStrResTranslator.BuildReviewedFileCandidate(bytes, [new("90001", $"Open {afterFragment}")]));

        Assert.Equal(original, bytes);
    }

    [Fact]
    public void UnicodeInsideAProtectedPathIsRetainedWhileItsHumanPrefixTranslates()
    {
        var bytes = Encoding936.GetBytes("90001=\u6253\u5f00 C:\\Client\\\u76ee\u5f55\\Play.exe\r\n");
        var candidate = SourceBackedStrResTranslator.BuildReviewedFileCandidate(bytes, [new("90001", "Open C:\\Client\\\u76ee\u5f55\\Play.exe")]);
        Assert.Equal(Encoding936.GetBytes("90001=Open C:\\Client\\\u76ee\u5f55\\Play.exe\r\n"), candidate.ToArray());
    }

    [Theory]
    [InlineData("SOFTWARE\\\u76ee\u5f55", "SOFTWARE\\English")]
    [InlineData("HKEY_CURRENT_USER\\\u76ee\u5f55", "HKEY_CURRENT_USER\\English")]
    [InlineData("Open C:\\Client\\\u76ee\u5f55\\Play.exe", "Launch C:\\Client\\\u76ee\u5f55\\Play.exe")]
    [InlineData("Already English", "Different English")]
    public void ConfigurationOrAlreadyEnglishHumanTextIsOutsideTheReviewedOperation(string originalValue, string replacement)
    {
        var bytes = Encoding936.GetBytes($"90001={originalValue}\r\n");
        Assert.Throws<InvalidDataException>(() => SourceBackedStrResTranslator.BuildReviewedFileCandidate(bytes, [new("90001", replacement)]));
    }
}

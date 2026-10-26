using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ReviewedStrResCompleteFragmentTests
{
    private static readonly Encoding Encoding936 = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);

    [Fact]
    public void LiteralLineEscapeBeforeWwwHumanTextIsNotAnUnrecognizedRelativePath()
    {
        var original = Encoding936.GetBytes("90001=\u67e5\u770b\u8bf4\u660e\\n\u8bf7\u5230www.example.invalid\u4e0b\u8f7d\u8d44\u6e90\r\n");
        const string replacement = "Read the notice\\nVisit www.example.invalid for files";

        var candidate = SourceBackedStrResTranslator.BuildReviewedFileCandidate(original, [new("90001", replacement)]);

        Assert.Equal(Encoding936.GetBytes("90001=" + replacement + "\r\n"), candidate.ToArray());
    }

    [Theory]
    [InlineData("https://example.invalid/\u5e2e\u52a9")]
    [InlineData("www.example.invalid/\u5e2e\u52a9")]
    [InlineData("ini/\u76ee\u5f55/server38.dat")]
    [InlineData(@"ini\目录\server38.dat")]
    [InlineData("127.0.0.1:9958/\u5e2e\u52a9")]
    [InlineData("127.0.0.1/\u76ee\u5f55/server38.dat?mode=1")]
    [InlineData(@"127.0.0.1\目录\server38.dat")]
    [InlineData("\"C:\\Program Files\\\u76ee\u5f55\\Play.exe\"")]
    public void CompleteUnicodeDataRemainsImmutableWhileSurroundingHumanTextTranslates(string fragment)
    {
        var original = Encoding936.GetBytes($";keep\r\n90001=\u6253\u5f00 {fragment}\r\n90002=\u4fdd\u7559\n");
        var preserved = original.ToArray();
        var replacement = $"Open {fragment}";

        var candidate = SourceBackedStrResTranslator.BuildReviewedFileCandidate(original, [new("90001", replacement)]);

        Assert.Equal(Encoding936.GetBytes($";keep\r\n90001=Open {fragment}\r\n90002=\u4fdd\u7559\n"), candidate.ToArray());
        Assert.Equal(preserved, original);
        Assert.Equal(1, candidate.Result.Applied);
        Assert.Equal(0, candidate.Result.Missing);
        Assert.False(SourceBackedStrResTranslator.BuildReviewedFileCandidate(candidate.ToArray(), [new("90001", replacement)]).Result.Changed);
    }

    [Theory]
    [InlineData("https://example.invalid/\u5e2e\u52a9", "https://example.invalid/")]
    [InlineData("https://example.invalid/\u5e2e\u52a9", "https://example.invalid/changed")]
    [InlineData("www.example.invalid/\u5e2e\u52a9", "www.example.invalid/")]
    [InlineData("www.example.invalid/\u5e2e\u52a9", "www.example.invalid/changed")]
    [InlineData("ini/\u76ee\u5f55/server38.dat", "")]
    [InlineData("ini/\u76ee\u5f55/server38.dat", "ini/other/server38.dat")]
    [InlineData("127.0.0.1:9958/\u5e2e\u52a9", "127.0.0.1:9958")]
    [InlineData("127.0.0.1:9958/\u5e2e\u52a9", "127.0.0.1:9958/changed")]
    [InlineData("127.0.0.1/\u76ee\u5f55/server38.dat?mode=1", "127.0.0.1/\u76ee\u5f55/server38.dat?mode=2")]
    [InlineData("\"C:\\Program Files\\\u76ee\u5f55\\Play.exe\"", "\"C:\\Program Files\\other\\Play.exe\"")]
    public void PrefixAgreementCannotAuthorizeDroppedOrChangedUnicodeSuffixes(string originalFragment, string replacementFragment)
    {
        var original = Encoding936.GetBytes($"90001=\u6253\u5f00 {originalFragment}\r\n");
        var preserved = original.ToArray();

        Assert.Throws<InvalidDataException>(() => SourceBackedStrResTranslator.BuildReviewedFileCandidate(original, [new("90001", $"Open {replacementFragment}")]));

        Assert.Equal(preserved, original);
    }

    [Theory]
    [InlineData(@"C:\Program Files\Play.exe")]
    [InlineData(@"\\host\Program Files\Play.exe")]
    public void AmbiguousUnquotedSpaceSeparatedPathsRequireSeparateReviewInsteadOfPartialMatching(string fragment)
    {
        var original = Encoding936.GetBytes($"90001=\u6253\u5f00 {fragment}\r\n");
        var preserved = original.ToArray();

        Assert.Throws<InvalidDataException>(() => SourceBackedStrResTranslator.BuildReviewedFileCandidate(original, [new("90001", $"Open {fragment}")]));

        Assert.Equal(preserved, original);
    }

    [Theory]
    [InlineData("www.\u4f8b\u5b50.\u6d4b\u8bd5")]
    [InlineData("\u76ee\u5f55/server38.dat")]
    public void UnrecognizedAddressOrRelativeFileFormsCannotDisappearAsOrdinaryHumanText(string fragment)
    {
        var original = Encoding936.GetBytes($"90001=\u6253\u5f00 {fragment}\r\n");
        var preserved = original.ToArray();

        Assert.Throws<InvalidDataException>(() => SourceBackedStrResTranslator.BuildReviewedFileCandidate(original, [new("90001", "Open")]));

        Assert.Equal(preserved, original);
    }
}

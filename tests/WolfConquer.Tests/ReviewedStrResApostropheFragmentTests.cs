using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ReviewedStrResApostropheFragmentTests
{
    private static readonly Encoding Encoding936 = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);

    [Theory]
    [InlineData("https://example.invalid/O'Brien")]
    [InlineData("www.example.invalid/O'Brien")]
    [InlineData(@"C:\O'Brien.exe")]
    [InlineData(@"\\host\O'Brien.exe")]
    [InlineData("ini/O'Brien.dat")]
    [InlineData("127.0.0.1/O'Brien")]
    public void InternalApostrophesRemainPartOfTheCompleteImmutableFragment(string fragment)
    {
        var before = Encoding936.GetBytes($"90001=\u6253\u5f00 {fragment}\r\n");
        var original = before.ToArray();
        var expected = Encoding936.GetBytes($"90001=Open {fragment}\r\n");

        var candidate = SourceBackedStrResTranslator.BuildReviewedFileCandidate(before, [new("90001", $"Open {fragment}")]);

        Assert.Equal(expected, candidate.ToArray());
        Assert.Equal(original, before);
        Assert.False(SourceBackedStrResTranslator.BuildReviewedFileCandidate(expected, [new("90001", $"Open {fragment}")]).Result.Changed);
    }

    [Theory]
    [InlineData("https://example.invalid/O'Brien", "https://example.invalid/O'Other")]
    [InlineData("www.example.invalid/O'Brien", "www.example.invalid/O'Other")]
    [InlineData(@"C:\O'Brien.exe", @"C:\O'Other.exe")]
    [InlineData(@"\\host\O'Brien.exe", @"\\host\O'Other.exe")]
    [InlineData("ini/O'Brien.dat", "ini/O'Other.dat")]
    [InlineData("127.0.0.1/O'Brien", "127.0.0.1/O'Other")]
    public void ASharedPrefixBeforeAnApostropheCannotAuthorizeAChangedSuffix(string fragment, string changed)
    {
        var before = Encoding936.GetBytes($"90001=\u6253\u5f00 {fragment}\r\n");
        var original = before.ToArray();

        Assert.Throws<InvalidDataException>(() =>
            SourceBackedStrResTranslator.BuildReviewedFileCandidate(before, [new("90001", $"Open {changed}")]));

        Assert.Equal(original, before);
    }
}

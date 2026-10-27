using System.Text;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8-bom")]
    [InlineData("utf16-le")]
    [InlineData("utf16-be")]
    [InlineData("utf32-le")]
    [InlineData("utf32-be")]
    public void LoadSafetyCompletePrestigeFilePreservesRecognizedStrictBomEncodingsAndNoBomUtf8(string format) =>
        RunOwnedPrestigeLoad(files =>
        {
            SeedPriorPrestigeGraph();
            var prior = new PublishedPrestigeGraph();
            var encoding = PrestigeLoadEncoding(format);
            var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(Text(FullRow))).ToArray();
            files.SeedBytes(bytes);
            Ranking.Load();
            var row = Assert.Single(Ranking.Ranks[Ranking.Type.Ninja].Items).Value;
            Assert.Equal(Values(Entry()), Values(row));
            Assert.Equal(new uint[] { 201, 202 }, row.Points);
            Assert.Same(row, Ranking.Ranks[Ranking.Type.World].Items[41]);
            prior.RequireEscapedOwnersUnchanged();
            Assert.Equal(bytes, File.ReadAllBytes(files.PathFor()));
        });

    private static Encoding PrestigeLoadEncoding(string format) => format switch
    {
        "utf8" => new UTF8Encoding(false, true),
        "utf8-bom" => new UTF8Encoding(true, true),
        "utf16-le" => new UnicodeEncoding(false, true, true),
        "utf16-be" => new UnicodeEncoding(true, true, true),
        "utf32-le" => new UTF32Encoding(false, true, true),
        "utf32-be" => new UTF32Encoding(true, true, true),
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static IEnumerable<object[]> InvalidPrestigeEncodedFiles()
    {
        yield return ["invalid-utf8-continuation", new byte[] { 0xC3, 0x28 }];
        yield return ["overlong-utf8", new byte[] { 0xC0, 0xAF }];
        yield return ["incomplete-utf8", new byte[] { 0xE2, 0x82 }];
        yield return ["surrogate-utf8", new byte[] { 0xED, 0xA0, 0x80 }];
        yield return ["invalid-utf8-after-bom", new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0x28 }];
        yield return ["invalid-utf8-after-complete-prefix", Encoding.UTF8.GetBytes(
            "Count=2" + Environment.NewLine + FullRow + Environment.NewLine + "99/")
            .Concat(new byte[] { 0xED, 0xA0, 0x80 }).ToArray()];
        yield return ["incomplete-utf16-le", new byte[] { 0xFF, 0xFE, 0x41 }];
        yield return ["unpaired-high-utf16-le", new byte[] { 0xFF, 0xFE, 0x00, 0xD8 }];
        yield return ["unpaired-low-utf16-be", new byte[] { 0xFE, 0xFF, 0xDC, 0x00 }];
        yield return ["outside-unicode-utf32-le", new byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x00, 0x00, 0x11, 0x00 }];
        yield return ["outside-unicode-utf32-be", new byte[] { 0x00, 0x00, 0xFE, 0xFF, 0x00, 0x11, 0x00, 0x00 }];
        yield return ["incomplete-utf32-le", new byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x41 }];
    }

    [Theory]
    [MemberData(nameof(InvalidPrestigeEncodedFiles))]
    public void LoadSafetyInvalidEncodedPrestigeFileCannotPublishReplacementCharactersOrMutateOldOwners(string _, byte[] bytes) =>
        RunOwnedPrestigeLoad(files =>
        {
            SeedPriorPrestigeGraph();
            var prior = new PublishedPrestigeGraph();
            files.SeedBytes(bytes);
            var failure = Assert.Throws<FormatException>(() => Ranking.Load());
            Assert.IsType<DecoderFallbackException>(failure.InnerException);
            Assert.Contains(files.PathFor(), failure.Message, StringComparison.Ordinal);
            prior.RequireUnchanged();
            Assert.Equal(bytes, File.ReadAllBytes(files.PathFor()));
        });
}

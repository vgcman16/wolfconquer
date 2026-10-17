using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterSayTemplateTranslatorTests
{
    [Fact]
    public void TranslateUsesStableTemplatesForSupportedPrefixes()
    {
        var target =
            "[4151]\r\n" +
            "color=0x0000ff\r\n" +
            "battle_1=\u6E2C\r\n" +
            "blood80_1=\u6E2C\r\n" +
            "blood50_1=\u6E2C\r\n" +
            "blood10_1=\u6E2C\r\n" +
            "die_1=\u6E2C\r\n";

        var result = LegacyMonsterSayTemplateTranslator.Translate(target, out var updated);

        Assert.True(result.Changed);
        Assert.Equal(6, result.KeyValueRowsScanned);
        Assert.Equal(5, result.SupportedKeyRows);
        Assert.Equal(5, result.ChangedRows);
        Assert.Contains("battle_1=Intruders! You dare enter my domain? Come and meet your fate!\r\n", updated);
        Assert.Contains("blood80_1=You overestimate yourselves. Kneel now, and I may leave you whole.\r\n", updated);
        Assert.Contains("blood50_1=Struggle all you want. You cannot shake me.\r\n", updated);
        Assert.Contains("blood10_1=My patience is gone. None of you will leave this place alive!\r\n", updated);
        Assert.Contains("die_1=This is not the end... I will return stronger.\r\n", updated);
    }

    [Fact]
    public void TranslatePreservesPrintfPlaceholders()
    {
        var target =
            "[4151]\n" +
            "kill_1=\u6E2C %s %d %% %u\n";

        var result = LegacyMonsterSayTemplateTranslator.Translate(target, out var updated);

        Assert.True(result.Changed);
        Assert.Equal(1, result.ChangedRows);
        Assert.Contains("kill_1=%s has fallen. Who else dares challenge me? %d %% %u\n", updated);
        Assert.Equal(1, CountOccurrences(updated, "%s"));
        Assert.Equal(1, CountOccurrences(updated, "%d"));
        Assert.Equal(1, CountOccurrences(updated, "%u"));
        Assert.Equal(1, CountOccurrences(updated, "%%"));
    }

    [Fact]
    public void TranslateSkipsUnknownCjkKeys()
    {
        var target =
            "[4151]\n" +
            "magic10001=\u6E2C\n" +
            "blood100_1=\u6E2C\n";

        var result = LegacyMonsterSayTemplateTranslator.Translate(target, out var updated);

        Assert.False(result.Changed);
        Assert.Equal(2, result.KeyValueRowsScanned);
        Assert.Equal(0, result.SupportedKeyRows);
        Assert.Equal(2, result.UnsupportedCjkRows);
        Assert.Equal(target, updated);
    }

    [Fact]
    public void TranslateSkipsSupportedRowsWithoutCjk()
    {
        var target =
            "[4151]\n" +
            "battle_1=Already angry.\n" +
            "kill_1=%s has already fallen.\n";

        var result = LegacyMonsterSayTemplateTranslator.Translate(target, out var updated);

        Assert.False(result.Changed);
        Assert.Equal(2, result.SupportedKeyRows);
        Assert.Equal(2, result.AlreadyEnglishRows);
        Assert.Equal(target, updated);
    }

    [Fact]
    public void PatchFileWritesUtf8WithoutBomAndIsIdempotent()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var targetPath = Path.Combine(tempDirectory, "monstersay.ini");
            var target =
                "[4151]\r\n" +
                ";battle_1=\u6E2C\r\n" +
                "battle_1=\u6E2C\r\n" +
                "kill_1=\u6E2C %s\r\n" +
                "magic10001=\u6E2C\r\n" +
                "die_1=This line is already English.\r\n";
            File.WriteAllText(targetPath, target, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var first = LegacyMonsterSayTemplateTranslator.PatchFile(targetPath);
            var firstBytes = File.ReadAllBytes(targetPath);
            var firstText = Encoding.UTF8.GetString(firstBytes);
            var second = LegacyMonsterSayTemplateTranslator.PatchFile(targetPath);
            var secondBytes = File.ReadAllBytes(targetPath);

            Assert.True(first.Changed);
            Assert.Equal(2, first.ChangedRows);
            Assert.False(HasUtf8Bom(firstBytes));
            Assert.Contains(";battle_1=\u6E2C\r\n", firstText);
            Assert.Contains("battle_1=Intruders! You dare enter my domain? Come and meet your fate!\r\n", firstText);
            Assert.Contains("kill_1=%s has fallen. Who else dares challenge me?\r\n", firstText);
            Assert.Contains("magic10001=\u6E2C\r\n", firstText);

            Assert.False(second.Changed);
            Assert.Equal(0, second.ChangedRows);
            Assert.Equal(firstBytes, secondBytes);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static int CountOccurrences(string value, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static bool HasUtf8Bom(byte[] bytes)
    {
        return bytes is [0xEF, 0xBB, 0xBF, ..];
    }
}

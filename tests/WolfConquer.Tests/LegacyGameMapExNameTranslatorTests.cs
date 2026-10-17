using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class LegacyGameMapExNameTranslatorTests
{
    [Fact]
    public void TranslateUsesReferenceNameByMapIdWithoutChangingOtherRows()
    {
        var target =
            "[1000]\r\n" +
            "Name=\u4e91\u95e8\u5173\r\n" +
            "MapDoc=map/desert.dmap\r\n" +
            "\r\n" +
            "[1002]\r\n" +
            "Name=CentralPlain\r\n";
        var reference =
            "[1000]\r\n" +
            "Name=Desert\r\n" +
            "\r\n" +
            "[1002]\r\n" +
            "Name=CentralPlain\r\n";

        var result = LegacyGameMapExNameTranslator.Translate(target, reference, out var updated);

        Assert.True(result.Changed);
        Assert.Equal(2, result.ReferenceMapsScanned);
        Assert.Equal(2, result.TargetMapsScanned);
        Assert.Equal(1, result.TranslatableNames);
        Assert.Equal(1, result.MatchedNames);
        Assert.Equal(1, result.ChangedNames);
        Assert.Equal(0, result.UnmatchedNames);
        Assert.Contains("[1000]\r\nName=Desert\r\nMapDoc=map/desert.dmap\r\n", updated, StringComparison.Ordinal);
        Assert.Contains("[1002]\r\nName=CentralPlain\r\n", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void TranslateTracksChineseNameWithoutReferenceAsUnmatched()
    {
        var target =
            "[9999]\n" +
            "Name=\u672a\u77e5\u5730\u56fe\n";
        var reference =
            "[1000]\n" +
            "Name=Desert\n";

        var result = LegacyGameMapExNameTranslator.Translate(target, reference, out var updated);

        Assert.False(result.Changed);
        Assert.Equal(1, result.TranslatableNames);
        Assert.Equal(0, result.MatchedNames);
        Assert.Equal(1, result.UnmatchedNames);
        Assert.Equal(target, updated);
    }

    [Fact]
    public void TranslateSkipsReferenceNameThatStillNeedsTranslation()
    {
        var target =
            "[1000]\n" +
            "Name=\u4e91\u95e8\u5173\n";
        var reference =
            "[1000]\n" +
            "Name=\u4e91\u95e8\u5173\n";

        var result = LegacyGameMapExNameTranslator.Translate(target, reference, out var updated);

        Assert.False(result.Changed);
        Assert.Equal(0, result.ReferenceMapsScanned);
        Assert.Equal(1, result.TranslatableNames);
        Assert.Equal(0, result.MatchedNames);
        Assert.Equal(1, result.UnmatchedNames);
        Assert.Equal(target, updated);
    }
}

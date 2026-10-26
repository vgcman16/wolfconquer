using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ReviewedStrResCandidateValidationTests
{
    private static readonly Encoding Encoding936 = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);

    [Fact]
    public void ReviewedFormattingUsesExactFieldWidthsOrderEscapesAndLiteralPercentMarkers()
    {
        var before = Encoding936.GetBytes("90001=%10d\u70b9\r\n90002=\u4e00%d\\n\u4e8c%s\n90003=\u589e\u52a0%d\uff05\r90004=\u6bd4\u4f8b50%\r\n");
        var candidate = SourceBackedStrResTranslator.BuildReviewedFileCandidate(before,
            [new("90001", "%10d points"), new("90002", "First %d\\nSecond %s"), new("90003", "Increase by %d%"), new("90004", "Rate (50%)")]);
        Assert.Equal(Encoding936.GetBytes("90001=%10d points\r\n90002=First %d\\nSecond %s\n90003=Increase by %d%\r90004=Rate (50%)\r\n"), candidate.ToArray());
        Assert.Equal(4, candidate.Result.Applied);
    }

    [Theory]
    [InlineData("%10d\u70b9", "%d points")]
    [InlineData("\u4e00%d\\n\u4e8c%s", "First %d Second %s")]
    [InlineData("\u4e00\\q\u4e8c", "First Second")]
    [InlineData("\u4e00%d\u4e8c%s", "First %s Second %d")]
    [InlineData("\u6bd4\u4f8b50%", "Rate 50")]
    [InlineData("\u4e2d", "Added <")]
    [InlineData("\u4e2d", "Added >")]
    [InlineData("\u4e2d", "Remaining \u4e2d")]
    [InlineData("\u4e2d", "Bad\r\nRow")]
    [InlineData("\u4e2d", "Bad\tRow")]
    [InlineData("\u4e2d", "Reward \U0001F680")]
    [InlineData("\u4e2d", "Reward\u2000")]
    [InlineData("\u4e2d", " ")]
    public void RejectedReviewedValuesNeverChangeOriginalBuffers(string originalValue, string replacement)
    {
        var bytes = Encoding936.GetBytes($"90001={originalValue}\r\n");
        var preserved = bytes.ToArray();
        Assert.Throws<InvalidDataException>(() => SourceBackedStrResTranslator.BuildReviewedFileCandidate(bytes, [new("90001", replacement)]));
        Assert.Equal(preserved, bytes);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate-target")]
    [InlineData("duplicate-target-alias")]
    [InlineData("duplicate-edit")]
    [InlineData("nonnumeric")]
    [InlineData("noncanonical-edit")]
    [InlineData("occurrence")]
    [InlineData("whole-line")]
    public void ReviewedCatalogRejectsAmbiguousOrOutOfScopeResourceSelection(string kind)
    {
        var text = "90001=\u4e2d\r\n" + (kind switch
        {
            "duplicate-target" => "90001=\u4e2d\r\n",
            "duplicate-target-alias" => "090001=\u4e2d\r\n",
            _ => ""
        });
        var bytes = Encoding936.GetBytes(text);
        ClientTextReplacement[] edits = kind switch
        {
            "missing" => [new("90002", "Missing")],
            "duplicate-edit" => [new("90001", "First"), new("90001", "Second")],
            "nonnumeric" => [new("ServerAddress", "Configuration")],
            "noncanonical-edit" => [new("090001", "Noncanonical")],
            "occurrence" => [new("90001", "Second", occurrence: 2)],
            "whole-line" => [new("90001", "90001=Whole", replaceLine: true)],
            _ => [new("90001", "Text")]
        };
        var preserved = bytes.ToArray();
        Assert.Throws<InvalidDataException>(() => SourceBackedStrResTranslator.BuildReviewedFileCandidate(bytes, edits));
        Assert.Equal(preserved, bytes);
    }
}

using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class EnglishClientStructuralDriftGuardTests
{
    [Fact]
    public void CompareTextReportsChangedSectionHeaderCountForIniLikeFiles()
    {
        var report = EnglishClientStructuralDriftGuard.CompareText(
            "ini/ChatTips.ini",
            "[Tip1]\r\nText=hello\r\n[Tip2]\r\nText=world\r\n",
            "[Tip1]\r\nText=hello\r\nText=world\r\n",
            new EnglishClientStructuralDriftOptions
            {
                ExpectInPlaceLineReplacement = false,
            });

        var issue = Assert.Single(report.Issues);
        Assert.True(report.HasHighRiskIssues);
        Assert.Equal(EnglishClientStructuralDriftIssueKind.SectionHeaderCountChanged, issue.Kind);
        Assert.Null(issue.LineNumber);
        Assert.Equal(2, issue.OriginalValue);
        Assert.Equal(1, issue.PatchedValue);
    }

    [Fact]
    public void CompareTextReportsChangedNonEmptyLineCountForInPlaceReplacement()
    {
        var report = EnglishClientStructuralDriftGuard.CompareText(
            "ini/message.ini",
            "Title=one\r\n\r\nBody=two\r\n",
            "Title=one\r\nInserted=unsafe\r\n\r\nBody=two\r\n",
            new EnglishClientStructuralDriftOptions
            {
                IsIniLike = false,
            });

        var issue = Assert.Single(report.Issues);
        Assert.Equal(EnglishClientStructuralDriftIssueKind.NonEmptyLineCountChanged, issue.Kind);
        Assert.Null(issue.LineNumber);
        Assert.Equal(2, issue.OriginalValue);
        Assert.Equal(3, issue.PatchedValue);
    }

    [Fact]
    public void CompareTextCanAllowLineCountGrowthWhenPatchIsNotInPlace()
    {
        var report = EnglishClientStructuralDriftGuard.CompareText(
            "help/readme.txt",
            "Title=one\r\n",
            "Title=one\r\nExtra=allowed\r\n",
            new EnglishClientStructuralDriftOptions
            {
                IsIniLike = false,
                ExpectInPlaceLineReplacement = false,
            });

        Assert.Empty(report.Issues);
        Assert.False(report.HasHighRiskIssues);
    }

    [Fact]
    public void CompareTextReportsPatchedLinesOverConfiguredByteLimit()
    {
        var report = EnglishClientStructuralDriftGuard.CompareText(
            "ini/Dialog.ini",
            "T=Old\r\n",
            "T=123456789\r\n",
            new EnglishClientStructuralDriftOptions
            {
                IsIniLike = false,
                MaxLineBytes = 8,
            });

        var issue = Assert.Single(report.Issues);
        Assert.Equal(EnglishClientStructuralDriftIssueKind.OversizedLine, issue.Kind);
        Assert.Equal(1, issue.LineNumber);
        Assert.Equal(5, issue.OriginalValue);
        Assert.Equal(11, issue.PatchedValue);
    }

    [Fact]
    public void CompareTextDoesNotReportUnchangedOriginalOversizedLines()
    {
        var report = EnglishClientStructuralDriftGuard.CompareText(
            "ini/Cn_Res.ini",
            "T=123456789\r\nU=123456789\r\n",
            "T=123456789\r\nU=12345\r\n",
            new EnglishClientStructuralDriftOptions
            {
                IsIniLike = false,
                MaxLineBytes = 8,
            });

        Assert.Empty(report.Issues);
        Assert.False(report.HasHighRiskIssues);
    }

    [Fact]
    public void CompareTextReportsOriginalOversizedLinesThatGrow()
    {
        var report = EnglishClientStructuralDriftGuard.CompareText(
            "ini/Cn_Res.ini",
            "T=123456789\r\n",
            "T=1234567890\r\n",
            new EnglishClientStructuralDriftOptions
            {
                IsIniLike = false,
                MaxLineBytes = 8,
            });

        var issue = Assert.Single(report.Issues);
        Assert.Equal(EnglishClientStructuralDriftIssueKind.OversizedLine, issue.Kind);
        Assert.Equal(1, issue.LineNumber);
        Assert.Equal(11, issue.OriginalValue);
        Assert.Equal(12, issue.PatchedValue);
    }

    [Fact]
    public void CompareBytesReportsChangedBomPresence()
    {
        var report = EnglishClientStructuralDriftGuard.CompareBytes(
            "ini/byte.ini",
            WithPreamble(Encoding.UTF8, "Name=Hero\r\n"),
            Encoding.UTF8.GetBytes("Name=Hero\r\n"),
            Encoding.UTF8,
            new EnglishClientStructuralDriftOptions
            {
                IsIniLike = false,
            });

        var issue = Assert.Single(report.Issues);
        Assert.Equal(EnglishClientStructuralDriftIssueKind.BomPresenceChanged, issue.Kind);
        Assert.Null(issue.LineNumber);
        Assert.Equal(1, issue.OriginalValue);
        Assert.Equal(0, issue.PatchedValue);
    }

    [Fact]
    public void CompareTextReportsChangedDominantLineEndingStyle()
    {
        var report = EnglishClientStructuralDriftGuard.CompareText(
            "ini/line-ending.ini",
            "Name=Hero\r\nTitle=Warrior\r\n",
            "Name=Hero\nTitle=Warrior\n",
            new EnglishClientStructuralDriftOptions
            {
                IsIniLike = false,
            });

        var issue = Assert.Single(report.Issues);
        Assert.Equal(EnglishClientStructuralDriftIssueKind.DominantLineEndingStyleChanged, issue.Kind);
        Assert.Null(issue.LineNumber);
        Assert.NotEqual(issue.OriginalValue, issue.PatchedValue);
        Assert.Contains("CRLF", issue.Message);
        Assert.Contains("LF", issue.Message);
    }

    [Fact]
    public void CompareBytesReportsChangedDominantLineEndingStyle()
    {
        var encoding = Encoding.ASCII;

        var report = EnglishClientStructuralDriftGuard.CompareBytes(
            "ini/line-ending-byte.ini",
            encoding.GetBytes("Name=Hero\r\nTitle=Warrior\r\n"),
            encoding.GetBytes("Name=Hero\nTitle=Warrior\n"),
            encoding,
            new EnglishClientStructuralDriftOptions
            {
                IsIniLike = false,
            });

        var issue = Assert.Single(report.Issues);
        Assert.Equal(EnglishClientStructuralDriftIssueKind.DominantLineEndingStyleChanged, issue.Kind);
        Assert.Null(issue.LineNumber);
    }

    [Fact]
    public void CompareTextReportsLostPrintfPlaceholdersAtMatchingLinePositions()
    {
        var report = EnglishClientStructuralDriftGuard.CompareText(
            "ini/GameAnnounce.ini",
            "Message=Player %s won %d points and has %I64u silver\r\nRepeat=%s gave %s %u\r\n",
            "Message=Player %s won %d points and has silver\r\nRepeat=%s gave player %u\r\n",
            new EnglishClientStructuralDriftOptions
            {
                IsIniLike = false,
            });

        Assert.Collection(
            report.Issues,
            lostI64 =>
            {
                Assert.Equal(EnglishClientStructuralDriftIssueKind.LostPrintfPlaceholder, lostI64.Kind);
                Assert.Equal(1, lostI64.LineNumber);
                Assert.Equal("%I64u", lostI64.Placeholder);
                Assert.Equal(1, lostI64.OriginalValue);
                Assert.Equal(0, lostI64.PatchedValue);
            },
            lostString =>
            {
                Assert.Equal(EnglishClientStructuralDriftIssueKind.LostPrintfPlaceholder, lostString.Kind);
                Assert.Equal(2, lostString.LineNumber);
                Assert.Equal("%s", lostString.Placeholder);
                Assert.Equal(2, lostString.OriginalValue);
                Assert.Equal(1, lostString.PatchedValue);
            });
    }

    [Fact]
    public void CompareTextReportsLostLiteralNewlineTokensAtMatchingLinePositions()
    {
        var report = EnglishClientStructuralDriftGuard.CompareText(
            "ini/Dialog.ini",
            "Message=First line\\nSecond line\r\nOther=Keep\\nToken\r\n",
            "Message=First line Second line\r\nOther=Keep\\nToken\r\n",
            new EnglishClientStructuralDriftOptions
            {
                IsIniLike = false,
            });

        var issue = Assert.Single(report.Issues);
        Assert.Equal(EnglishClientStructuralDriftIssueKind.LostLiteralNewlineToken, issue.Kind);
        Assert.Equal(1, issue.LineNumber);
        Assert.Equal("\\n", issue.Placeholder);
        Assert.Equal(1, issue.OriginalValue);
        Assert.Equal(0, issue.PatchedValue);
    }

    [Fact]
    public void CompareBytesUsesSuppliedEncodingForPlaceholderChecks()
    {
        var encoding = Encoding.ASCII;

        var report = EnglishClientStructuralDriftGuard.CompareBytes(
            "ini/byte.ini",
            encoding.GetBytes("Name=%s\r\n"),
            encoding.GetBytes("Name=lost\r\n"),
            encoding,
            new EnglishClientStructuralDriftOptions
            {
                IsIniLike = false,
            });

        var issue = Assert.Single(report.Issues);
        Assert.Equal(EnglishClientStructuralDriftIssueKind.LostPrintfPlaceholder, issue.Kind);
        Assert.Equal(1, issue.LineNumber);
        Assert.Equal("%s", issue.Placeholder);
    }

    [Fact]
    public void CompareTextDoesNotReportCleanInPlacePatch()
    {
        var report = EnglishClientStructuralDriftGuard.CompareText(
            "ini/Clean.ini",
            "[A]\r\nText=Player %s has %d points\r\n\r\n[B]\r\nText=Keep %u\r\n",
            "[A]\r\nText=Hero %s keeps %d points\r\n\r\n[B]\r\nText=Still keeps %u\r\n");

        Assert.Empty(report.Issues);
        Assert.False(report.HasHighRiskIssues);
    }

    private static byte[] WithPreamble(Encoding encoding, string text)
    {
        var preamble = encoding.GetPreamble();
        var content = encoding.GetBytes(text);
        var bytes = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, bytes, preamble.Length, content.Length);

        return bytes;
    }
}

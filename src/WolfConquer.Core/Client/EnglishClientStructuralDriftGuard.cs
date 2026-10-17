using System.Text;
using System.Text.RegularExpressions;

namespace WolfConquer.Core.Client;

public sealed record EnglishClientStructuralDriftOptions
{
    public const int DefaultMaxLineBytes = 512;

    public bool IsIniLike { get; init; } = true;

    public bool ExpectInPlaceLineReplacement { get; init; } = true;

    public int MaxLineBytes { get; init; } = DefaultMaxLineBytes;

    public Encoding TextEncoding { get; init; } = Encoding.UTF8;

    public bool RequireStableBomPresence { get; init; } = true;

    public bool RequireStableLineEndingStyle { get; init; } = true;

    public bool RequireStableLiteralNewlineTokens { get; init; } = true;
}

public sealed record EnglishClientStructuralDriftReport(
    string RelativePath,
    IReadOnlyList<EnglishClientStructuralDriftIssue> Issues)
{
    public bool HasHighRiskIssues => Issues.Count != 0;
}

public sealed record EnglishClientStructuralDriftIssue(
    EnglishClientStructuralDriftIssueKind Kind,
    int? LineNumber,
    string Message,
    int OriginalValue = 0,
    int PatchedValue = 0,
    string? Placeholder = null);

public enum EnglishClientStructuralDriftIssueKind
{
    SectionHeaderCountChanged,
    NonEmptyLineCountChanged,
    OversizedLine,
    LostPrintfPlaceholder,
    BomPresenceChanged,
    DominantLineEndingStyleChanged,
    LostLiteralNewlineToken,
}

public static class EnglishClientStructuralDriftGuard
{
    private const string LiteralNewlineToken = "\\n";

    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };
    private static readonly byte[] Utf16LittleEndianBom = { 0xFF, 0xFE };
    private static readonly byte[] Utf16BigEndianBom = { 0xFE, 0xFF };
    private static readonly byte[] Utf32LittleEndianBom = { 0xFF, 0xFE, 0x00, 0x00 };
    private static readonly byte[] Utf32BigEndianBom = { 0x00, 0x00, 0xFE, 0xFF };

    private static readonly Regex PrintfPlaceholderRegex = new(
        @"(?<!%)%(?:I64)?[sdu]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static EnglishClientStructuralDriftReport CompareText(
        string relativePath,
        string originalText,
        string patchedText,
        EnglishClientStructuralDriftOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(originalText);
        ArgumentNullException.ThrowIfNull(patchedText);

        var resolvedOptions = ResolveOptions(options);
        var originalLines = SplitLines(originalText);
        var patchedLines = SplitLines(patchedText);

        return CompareLines(
            relativePath,
            originalLines,
            patchedLines,
            LineByteLengths(originalLines, resolvedOptions.TextEncoding),
            LineByteLengths(patchedLines, resolvedOptions.TextEncoding),
            DetectDominantLineEndingStyle(originalText),
            DetectDominantLineEndingStyle(patchedText),
            originalBomPresent: null,
            patchedBomPresent: null,
            resolvedOptions);
    }

    public static EnglishClientStructuralDriftReport CompareBytes(
        string relativePath,
        byte[] originalBytes,
        byte[] patchedBytes,
        Encoding encoding,
        EnglishClientStructuralDriftOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(originalBytes);
        ArgumentNullException.ThrowIfNull(patchedBytes);
        ArgumentNullException.ThrowIfNull(encoding);

        var resolvedOptions = ResolveOptions(options);
        return CompareLines(
            relativePath,
            SplitLines(encoding.GetString(originalBytes)),
            SplitLines(encoding.GetString(patchedBytes)),
            SplitByteLineLengths(originalBytes),
            SplitByteLineLengths(patchedBytes),
            DetectDominantLineEndingStyle(originalBytes),
            DetectDominantLineEndingStyle(patchedBytes),
            HasByteOrderMark(originalBytes, encoding),
            HasByteOrderMark(patchedBytes, encoding),
            resolvedOptions);
    }

    private static EnglishClientStructuralDriftReport CompareLines(
        string relativePath,
        IReadOnlyList<string> originalLines,
        IReadOnlyList<string> patchedLines,
        IReadOnlyList<int> originalLineByteLengths,
        IReadOnlyList<int> patchedLineByteLengths,
        DominantLineEndingStyle originalLineEndingStyle,
        DominantLineEndingStyle patchedLineEndingStyle,
        bool? originalBomPresent,
        bool? patchedBomPresent,
        EnglishClientStructuralDriftOptions options)
    {
        var issues = new List<EnglishClientStructuralDriftIssue>();

        if (options.RequireStableBomPresence &&
            originalBomPresent.HasValue &&
            patchedBomPresent.HasValue &&
            originalBomPresent.Value != patchedBomPresent.Value)
        {
            issues.Add(new EnglishClientStructuralDriftIssue(
                EnglishClientStructuralDriftIssueKind.BomPresenceChanged,
                LineNumber: null,
                $"BOM presence changed from {DescribeBomPresence(originalBomPresent.Value)} to {DescribeBomPresence(patchedBomPresent.Value)}.",
                originalBomPresent.Value ? 1 : 0,
                patchedBomPresent.Value ? 1 : 0));
        }

        if (options.RequireStableLineEndingStyle &&
            originalLineEndingStyle != DominantLineEndingStyle.None &&
            patchedLineEndingStyle != DominantLineEndingStyle.None &&
            originalLineEndingStyle != patchedLineEndingStyle)
        {
            issues.Add(new EnglishClientStructuralDriftIssue(
                EnglishClientStructuralDriftIssueKind.DominantLineEndingStyleChanged,
                LineNumber: null,
                $"Dominant line-ending style changed from {DescribeLineEndingStyle(originalLineEndingStyle)} to {DescribeLineEndingStyle(patchedLineEndingStyle)}.",
                (int)originalLineEndingStyle,
                (int)patchedLineEndingStyle));
        }

        if (options.IsIniLike)
        {
            var originalSectionCount = CountSectionHeaders(originalLines);
            var patchedSectionCount = CountSectionHeaders(patchedLines);
            if (originalSectionCount != patchedSectionCount)
            {
                issues.Add(new EnglishClientStructuralDriftIssue(
                    EnglishClientStructuralDriftIssueKind.SectionHeaderCountChanged,
                    LineNumber: null,
                    $"Section header count changed from {originalSectionCount} to {patchedSectionCount}.",
                    originalSectionCount,
                    patchedSectionCount));
            }
        }

        if (options.ExpectInPlaceLineReplacement)
        {
            var originalNonEmptyLineCount = CountNonEmptyLines(originalLines);
            var patchedNonEmptyLineCount = CountNonEmptyLines(patchedLines);
            if (originalNonEmptyLineCount != patchedNonEmptyLineCount)
            {
                issues.Add(new EnglishClientStructuralDriftIssue(
                    EnglishClientStructuralDriftIssueKind.NonEmptyLineCountChanged,
                    LineNumber: null,
                    $"Non-empty line count changed from {originalNonEmptyLineCount} to {patchedNonEmptyLineCount}.",
                    originalNonEmptyLineCount,
                    patchedNonEmptyLineCount));
            }
        }

        for (var index = 0; index < patchedLineByteLengths.Count; index++)
        {
            var patchedLength = patchedLineByteLengths[index];
            var originalLength = index < originalLineByteLengths.Count
                ? originalLineByteLengths[index]
                : 0;
            var originalWasOversized = originalLength > options.MaxLineBytes;
            var patchedNewlyOversized = !originalWasOversized && patchedLength > options.MaxLineBytes;
            var oversizedLineGrew = originalWasOversized && patchedLength > originalLength;
            if (!patchedNewlyOversized && !oversizedLineGrew)
            {
                continue;
            }

            issues.Add(new EnglishClientStructuralDriftIssue(
                EnglishClientStructuralDriftIssueKind.OversizedLine,
                index + 1,
                $"Line {index + 1} is {patchedLength} bytes, above the {options.MaxLineBytes} byte threshold.",
                originalLength,
                patchedLength));
        }

        var matchingLineCount = Math.Min(originalLines.Count, patchedLines.Count);
        for (var index = 0; index < matchingLineCount; index++)
        {
            AddLostPlaceholderIssues(index, originalLines[index], patchedLines[index], issues);
            if (options.RequireStableLiteralNewlineTokens)
            {
                AddLostLiteralNewlineTokenIssues(index, originalLines[index], patchedLines[index], issues);
            }
        }

        return new EnglishClientStructuralDriftReport(relativePath, issues);
    }

    private static void AddLostPlaceholderIssues(
        int lineIndex,
        string originalLine,
        string patchedLine,
        List<EnglishClientStructuralDriftIssue> issues)
    {
        var originalPlaceholders = CountPrintfPlaceholders(originalLine);
        if (originalPlaceholders.Count == 0)
        {
            return;
        }

        var patchedPlaceholders = CountPrintfPlaceholders(patchedLine);
        foreach (var (placeholder, originalCount) in originalPlaceholders)
        {
            patchedPlaceholders.TryGetValue(placeholder, out var patchedCount);
            if (patchedCount >= originalCount)
            {
                continue;
            }

            issues.Add(new EnglishClientStructuralDriftIssue(
                EnglishClientStructuralDriftIssueKind.LostPrintfPlaceholder,
                lineIndex + 1,
                $"Line {lineIndex + 1} lost printf placeholder {placeholder}: original {originalCount}, patched {patchedCount}.",
                originalCount,
                patchedCount,
                placeholder));
        }
    }

    private static void AddLostLiteralNewlineTokenIssues(
        int lineIndex,
        string originalLine,
        string patchedLine,
        List<EnglishClientStructuralDriftIssue> issues)
    {
        var originalCount = CountLiteralNewlineTokens(originalLine);
        if (originalCount == 0)
        {
            return;
        }

        var patchedCount = CountLiteralNewlineTokens(patchedLine);
        if (patchedCount >= originalCount)
        {
            return;
        }

        issues.Add(new EnglishClientStructuralDriftIssue(
            EnglishClientStructuralDriftIssueKind.LostLiteralNewlineToken,
            lineIndex + 1,
            $"Line {lineIndex + 1} lost literal \\n tokens: original {originalCount}, patched {patchedCount}.",
            originalCount,
            patchedCount,
            LiteralNewlineToken));
    }

    private static EnglishClientStructuralDriftOptions ResolveOptions(EnglishClientStructuralDriftOptions? options)
    {
        options ??= new EnglishClientStructuralDriftOptions();
        ArgumentNullException.ThrowIfNull(options.TextEncoding);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxLineBytes);

        return options;
    }

    private static int CountSectionHeaders(IReadOnlyList<string> lines)
    {
        var count = 0;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 2 && trimmed[0] == '[' && trimmed[^1] == ']')
            {
                count++;
            }
        }

        return count;
    }

    private static int CountNonEmptyLines(IReadOnlyList<string> lines)
    {
        var count = 0;
        foreach (var line in lines)
        {
            if (line.Trim().Length != 0)
            {
                count++;
            }
        }

        return count;
    }

    private static Dictionary<string, int> CountPrintfPlaceholders(string line)
    {
        var placeholders = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match match in PrintfPlaceholderRegex.Matches(line))
        {
            var value = match.Value;
            placeholders.TryGetValue(value, out var count);
            placeholders[value] = count + 1;
        }

        return placeholders;
    }

    private static int CountLiteralNewlineTokens(string line)
    {
        var count = 0;
        var searchIndex = 0;
        while (searchIndex < line.Length)
        {
            var tokenIndex = line.IndexOf(LiteralNewlineToken, searchIndex, StringComparison.Ordinal);
            if (tokenIndex < 0)
            {
                break;
            }

            count++;
            searchIndex = tokenIndex + LiteralNewlineToken.Length;
        }

        return count;
    }

    private static IReadOnlyList<int> LineByteLengths(IReadOnlyList<string> lines, Encoding encoding)
    {
        var lengths = new int[lines.Count];
        for (var index = 0; index < lines.Count; index++)
        {
            lengths[index] = encoding.GetByteCount(lines[index]);
        }

        return lengths;
    }

    private static DominantLineEndingStyle DetectDominantLineEndingStyle(string text)
    {
        var crlfCount = 0;
        var lfCount = 0;
        var crCount = 0;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\r')
            {
                if (index + 1 < text.Length && text[index + 1] == '\n')
                {
                    crlfCount++;
                    index++;
                }
                else
                {
                    crCount++;
                }
            }
            else if (text[index] == '\n')
            {
                lfCount++;
            }
        }

        return SelectDominantLineEndingStyle(crlfCount, lfCount, crCount);
    }

    private static DominantLineEndingStyle DetectDominantLineEndingStyle(byte[] bytes)
    {
        var crlfCount = 0;
        var lfCount = 0;
        var crCount = 0;

        for (var index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] == '\r')
            {
                if (index + 1 < bytes.Length && bytes[index + 1] == '\n')
                {
                    crlfCount++;
                    index++;
                }
                else
                {
                    crCount++;
                }
            }
            else if (bytes[index] == '\n')
            {
                lfCount++;
            }
        }

        return SelectDominantLineEndingStyle(crlfCount, lfCount, crCount);
    }

    private static DominantLineEndingStyle SelectDominantLineEndingStyle(int crlfCount, int lfCount, int crCount)
    {
        var max = Math.Max(crlfCount, Math.Max(lfCount, crCount));
        if (max == 0)
        {
            return DominantLineEndingStyle.None;
        }

        var dominantStyleCount = 0;
        var dominantStyle = DominantLineEndingStyle.None;
        if (crlfCount == max)
        {
            dominantStyle = DominantLineEndingStyle.CrLf;
            dominantStyleCount++;
        }

        if (lfCount == max)
        {
            dominantStyle = DominantLineEndingStyle.Lf;
            dominantStyleCount++;
        }

        if (crCount == max)
        {
            dominantStyle = DominantLineEndingStyle.Cr;
            dominantStyleCount++;
        }

        return dominantStyleCount == 1
            ? dominantStyle
            : DominantLineEndingStyle.None;
    }

    private static string DescribeLineEndingStyle(DominantLineEndingStyle style)
    {
        return style switch
        {
            DominantLineEndingStyle.CrLf => "CRLF",
            DominantLineEndingStyle.Lf => "LF",
            DominantLineEndingStyle.Cr => "CR",
            _ => "none",
        };
    }

    private static string DescribeBomPresence(bool hasBom)
    {
        return hasBom ? "present" : "absent";
    }

    private static bool HasByteOrderMark(byte[] bytes, Encoding encoding)
    {
        var encodingPreamble = encoding.GetPreamble();
        if (encodingPreamble.Length != 0 && HasPrefix(bytes, encodingPreamble))
        {
            return true;
        }

        return HasPrefix(bytes, Utf8Bom) ||
            HasPrefix(bytes, Utf16LittleEndianBom) ||
            HasPrefix(bytes, Utf16BigEndianBom) ||
            HasPrefix(bytes, Utf32LittleEndianBom) ||
            HasPrefix(bytes, Utf32BigEndianBom);
    }

    private static bool HasPrefix(byte[] bytes, byte[] prefix)
    {
        if (bytes.Length < prefix.Length)
        {
            return false;
        }

        for (var index = 0; index < prefix.Length; index++)
        {
            if (bytes[index] != prefix[index])
            {
                return false;
            }
        }

        return true;
    }

    private enum DominantLineEndingStyle
    {
        None = 0,
        CrLf = 1,
        Lf = 2,
        Cr = 3,
    }

    private static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        var start = 0;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\r' && text[index] != '\n')
            {
                continue;
            }

            var endingLength = text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n'
                ? 2
                : 1;
            lines.Add(text[start..index]);
            index += endingLength - 1;
            start = index + 1;
        }

        if (start < text.Length)
        {
            lines.Add(text[start..]);
        }

        return lines;
    }

    private static IReadOnlyList<int> SplitByteLineLengths(byte[] bytes)
    {
        var lengths = new List<int>();
        var start = 0;

        for (var index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != '\r' && bytes[index] != '\n')
            {
                continue;
            }

            lengths.Add(index - start);
            if (bytes[index] == '\r' && index + 1 < bytes.Length && bytes[index + 1] == '\n')
            {
                index++;
            }

            start = index + 1;
        }

        if (start < bytes.Length)
        {
            lengths.Add(bytes.Length - start);
        }

        return lengths;
    }
}

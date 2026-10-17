using System.Text;

namespace WolfConquer.Core.Client;

public sealed record SourceBackedStrResTranslationResult(
    int SourceRows,
    int SourceKeys,
    int ConflictedSourceKeys,
    int TargetRows,
    int RowsNeedingTranslation,
    int TranslatedRows,
    int PlaceholderMismatchRows,
    int UnmatchedRows,
    int AlreadyTranslatedRows,
    int ChangedRows,
    int RemainingCjkRows,
    bool Changed);

public static class SourceBackedStrResTranslator
{
    public const string DefaultTargetPath = "client/ini/StrRes.ini";

    private static readonly Encoding StrResEncoding =
        ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);

    public static SourceBackedStrResTranslationResult PatchDefaultFile(
        IEnumerable<string> sourcePaths,
        bool includeTargetAsSource = true,
        IEnumerable<ClientTextReplacement>? catalogReplacements = null)
    {
        return PatchFile(
            DefaultTargetPath,
            sourcePaths,
            includeTargetAsSource,
            catalogReplacements);
    }

    public static SourceBackedStrResTranslationResult PatchFile(
        string targetPath,
        IEnumerable<string> sourcePaths,
        bool includeTargetAsSource = true,
        IEnumerable<ClientTextReplacement>? catalogReplacements = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var originalBytes = File.ReadAllBytes(targetPath);
        var originalText = StrResEncoding.GetString(originalBytes);
        var sourceTexts = ReadSourceTexts(sourcePaths);
        if (includeTargetAsSource)
        {
            sourceTexts = sourceTexts.Append(originalText);
        }

        var result = Translate(
            originalText,
            sourceTexts,
            out var updatedText,
            catalogReplacements);
        if (result.Changed)
        {
            ClientTextPatchWriter.WritePatchedTextPreservingUnchangedBytes(
                targetPath,
                originalBytes,
                originalText,
                updatedText,
                StrResEncoding);
        }

        return result;
    }

    public static SourceBackedStrResTranslationResult Translate(
        string targetText,
        IEnumerable<string> sourceTexts,
        out string updatedText,
        IEnumerable<ClientTextReplacement>? catalogReplacements = null)
    {
        ArgumentNullException.ThrowIfNull(targetText);
        ArgumentNullException.ThrowIfNull(sourceTexts);

        var catalog = BuildSourceCatalog(
            sourceTexts,
            catalogReplacements ?? []);
        var targetLines = SplitLines(targetText).ToArray();
        var targetRows = 0;
        var rowsNeedingTranslation = 0;
        var translatedRows = 0;
        var placeholderMismatchRows = 0;
        var unmatchedRows = 0;
        var alreadyTranslatedRows = 0;
        var changedRows = 0;
        var remainingCjkRows = 0;

        for (var index = 0; index < targetLines.Length; index++)
        {
            if (IsBlankOrSemicolonComment(targetLines[index].Text) ||
                !TryParseKeyValue(targetLines[index].Text, out var key, out var value, out var prefix, out var separator))
            {
                continue;
            }

            targetRows++;
            if (!NeedsTranslation(value))
            {
                alreadyTranslatedRows++;
                continue;
            }

            rowsNeedingTranslation++;
            if (!catalog.Values.TryGetValue(key, out var replacement))
            {
                unmatchedRows++;
                remainingCjkRows++;
                continue;
            }

            if (!IsReplacementCompatibleWithTarget(value, replacement))
            {
                placeholderMismatchRows++;
                remainingCjkRows++;
                continue;
            }

            translatedRows++;
            var translatedLine = prefix + key + separator + replacement;
            if (!StringComparer.Ordinal.Equals(targetLines[index].Text, translatedLine))
            {
                changedRows++;
                targetLines[index] = targetLines[index] with { Text = translatedLine };
            }
        }

        var builder = new StringBuilder(targetText.Length);
        foreach (var line in targetLines)
        {
            builder.Append(line.Text);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        return new SourceBackedStrResTranslationResult(
            catalog.SourceRows,
            catalog.Values.Count,
            catalog.ConflictedKeys,
            targetRows,
            rowsNeedingTranslation,
            translatedRows,
            placeholderMismatchRows,
            unmatchedRows,
            alreadyTranslatedRows,
            changedRows,
            remainingCjkRows,
            !StringComparer.Ordinal.Equals(targetText, updatedText));
    }

    private static IEnumerable<string> ReadSourceTexts(IEnumerable<string> sourcePaths)
    {
        foreach (var sourcePath in sourcePaths)
        {
            if (!ClientTextSourcePathFence.IsAllowedSourcePath(sourcePath) ||
                !File.Exists(sourcePath))
            {
                continue;
            }

            yield return DecodeSourceText(sourcePath, File.ReadAllBytes(sourcePath));
        }
    }

    private static string DecodeSourceText(string sourcePath, byte[] bytes)
    {
        if (Path.GetExtension(sourcePath).Equals(".cs", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return TrimUtf8Bom(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                    .GetString(bytes));
            }
            catch (DecoderFallbackException)
            {
                // Fall through to lenient CP936 so malformed source bytes become unsafe replacement chars.
            }
        }

        return GetLenientCodePage936Encoding().GetString(bytes);
    }

    private static string TrimUtf8Bom(string text)
    {
        return text.Length > 0 && text[0] == '\ufeff'
            ? text[1..]
            : text;
    }

    private static Encoding GetLenientCodePage936Encoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            936,
            EncoderFallback.ReplacementFallback,
            DecoderFallback.ReplacementFallback);
    }

    private static SourceCatalog BuildSourceCatalog(
        IEnumerable<string> sourceTexts,
        IEnumerable<ClientTextReplacement> sourceReplacements)
    {
        var candidates = new Dictionary<string, string>(StringComparer.Ordinal);
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        var sourceRows = 0;

        foreach (var replacement in sourceReplacements)
        {
            if (!IsLikelySourceKey(replacement.Key) ||
                !IsSourceBackedReplacement(replacement.Value))
            {
                continue;
            }

            sourceRows++;
            AddSourceCandidate(candidates, conflicts, replacement.Key, replacement.Value);
        }

        foreach (var sourceText in sourceTexts)
        {
            foreach (var line in SplitLines(sourceText))
            {
                if (!TryParseSourceRow(line.Text, out var key, out var value) ||
                    !IsLikelySourceKey(key) ||
                    !IsSourceBackedReplacement(value))
                {
                    continue;
                }

                sourceRows++;
                AddSourceCandidate(candidates, conflicts, key, value);
            }
        }

        return new SourceCatalog(sourceRows, candidates, conflicts.Count);
    }

    private static void AddSourceCandidate(
        Dictionary<string, string> candidates,
        HashSet<string> conflicts,
        string key,
        string value)
    {
        if (conflicts.Contains(key))
        {
            return;
        }

        if (candidates.TryGetValue(key, out var existing))
        {
            if (!StringComparer.Ordinal.Equals(existing, value))
            {
                candidates.Remove(key);
                conflicts.Add(key);
            }

            return;
        }

        candidates.Add(key, value);
    }

    private static bool TryParseSourceRow(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        if (IsBlankOrSemicolonComment(line))
        {
            return false;
        }

        if (TryParseCSharpClientTextReplacement(line, out key, out value))
        {
            return true;
        }

        return TryParseKeyValue(line, out key, out value, out _, out _);
    }

    private static bool TryParseKeyValue(
        string line,
        out string key,
        out string value,
        out string prefix,
        out string separator)
    {
        key = string.Empty;
        value = string.Empty;
        prefix = string.Empty;
        separator = string.Empty;

        var keyStart = 0;
        while (keyStart < line.Length && char.IsWhiteSpace(line[keyStart]))
        {
            keyStart++;
        }

        if (keyStart >= line.Length || line[keyStart] == ';')
        {
            return false;
        }

        var equalsIndex = line.IndexOf('=', keyStart);
        if (equalsIndex <= keyStart)
        {
            return false;
        }

        var keyEnd = equalsIndex;
        while (keyEnd > keyStart && char.IsWhiteSpace(line[keyEnd - 1]))
        {
            keyEnd--;
        }

        var valueStart = equalsIndex + 1;
        while (valueStart < line.Length && char.IsWhiteSpace(line[valueStart]))
        {
            valueStart++;
        }

        prefix = line[..keyStart];
        key = line[keyStart..keyEnd];
        separator = line[keyEnd..valueStart];
        value = line[valueStart..];
        return key.Length > 0;
    }

    private static bool TryParseCSharpClientTextReplacement(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var index = 0;
        SkipWhitespace(line, ref index);
        if (!TryReadToken(line, ref index, "new"))
        {
            return false;
        }

        SkipWhitespace(line, ref index);
        if (index < line.Length && line[index] != '(')
        {
            if (!TryReadCSharpTypeName(line, ref index))
            {
                return false;
            }

            SkipWhitespace(line, ref index);
        }

        if (!TryReadCharacter(line, ref index, '('))
        {
            return false;
        }

        SkipWhitespace(line, ref index);
        if (!TryReadCSharpQuotedString(line, ref index, out key))
        {
            return false;
        }

        SkipWhitespace(line, ref index);
        if (!TryReadCharacter(line, ref index, ','))
        {
            return false;
        }

        SkipWhitespace(line, ref index);
        return TryReadCSharpQuotedString(line, ref index, out value);
    }

    private static bool TryReadCSharpQuotedString(string text, ref int index, out string value)
    {
        value = string.Empty;
        if (!TryReadCharacter(text, ref index, '"'))
        {
            return false;
        }

        var builder = new StringBuilder();
        while (index < text.Length)
        {
            var character = text[index++];
            if (character == '"')
            {
                value = builder.ToString();
                return true;
            }

            if (character != '\\')
            {
                builder.Append(character);
                continue;
            }

            if (index >= text.Length)
            {
                return false;
            }

            var escaped = text[index++];
            builder.Append(escaped switch
            {
                'a' => "\\a",
                'b' => "\\b",
                'f' => "\\f",
                'n' => "\\n",
                'r' => "\\r",
                't' => "\\t",
                'v' => "\\v",
                '\\' => '\\',
                '"' => '"',
                '\'' => '\'',
                _ => "\\" + escaped.ToString()
            });
        }

        return false;
    }

    private static bool TryReadCSharpTypeName(string text, ref int index)
    {
        var start = index;
        while (index < text.Length &&
            (char.IsAsciiLetterOrDigit(text[index]) || text[index] is '_' or '.'))
        {
            index++;
        }

        return index > start;
    }

    private static bool TryReadToken(string text, ref int index, string token)
    {
        if (!text.AsSpan(index).StartsWith(token, StringComparison.Ordinal))
        {
            return false;
        }

        var end = index + token.Length;
        if (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] == '_'))
        {
            return false;
        }

        index = end;
        return true;
    }

    private static void SkipWhitespace(string text, ref int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }
    }

    private static bool TryReadCharacter(string text, ref int index, char expected)
    {
        if (index >= text.Length || text[index] != expected)
        {
            return false;
        }

        index++;
        return true;
    }

    private static bool IsSourceBackedReplacement(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length != 0 &&
            !NeedsTranslation(trimmed) &&
            !ContainsControlCharacter(trimmed);
    }

    private static bool IsReplacementCompatibleWithTarget(string targetValue, string replacement)
    {
        return SameTokenSequence(ExtractPrintfFormatTokens(targetValue), ExtractPrintfFormatTokens(replacement)) &&
            SameTokenSequence(ExtractEscapedFormattingTokens(targetValue), ExtractEscapedFormattingTokens(replacement));
    }

    private static IReadOnlyList<string> ExtractPrintfFormatTokens(string value)
    {
        var tokens = new List<string>();
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }

            if (index + 1 < value.Length && value[index + 1] == '%')
            {
                tokens.Add("%%");
                index++;
                continue;
            }

            var start = index;
            index++;
            while (index < value.Length && IsPrintfPlaceholderPart(value[index]))
            {
                if (IsPrintfSpecifier(value[index]))
                {
                    tokens.Add(value[start..(index + 1)]);
                    break;
                }

                index++;
            }
        }

        return tokens;
    }

    private static bool IsPrintfPlaceholderPart(char character)
    {
        return char.IsAsciiLetterOrDigit(character) ||
            character is '$' or '-' or '+' or '#' or '0' or ' ' or '\'' or '*' or '.' or ':';
    }

    private static bool IsPrintfSpecifier(char character)
    {
        return character is 'd' or 'i' or 'u' or 'o' or 'x' or 'X' or 'f' or 'F' or 'e' or 'E' or 'g' or 'G' or
            'a' or 'A' or 'c' or 'C' or 's' or 'S' or 'p' or 'n';
    }

    private static IReadOnlyList<string> ExtractEscapedFormattingTokens(string value)
    {
        var tokens = new List<string>();
        for (var index = 0; index + 1 < value.Length; index++)
        {
            if (value[index] == '\\' && value[index + 1] is 'n' or 'r' or 't')
            {
                tokens.Add(value.Substring(index, 2));
                index++;
            }
        }

        return tokens;
    }

    private static bool SameTokenSequence(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!StringComparer.Ordinal.Equals(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLikelySourceKey(string key)
    {
        if (key.Length == 0)
        {
            return false;
        }

        foreach (var character in key)
        {
            if (char.IsWhiteSpace(character) || character is '=' or '"' or '\'' or '[' or ']')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsBlankOrSemicolonComment(string line)
    {
        var index = 0;
        while (index < line.Length && char.IsWhiteSpace(line[index]))
        {
            index++;
        }

        return index >= line.Length || line[index] == ';';
    }

    private static bool NeedsTranslation(string value)
    {
        foreach (var character in value)
        {
            if (character == '\ufffd' || IsCjkOrFullwidth(character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCjkOrFullwidth(char character)
    {
        return character is >= '\u2e80' and <= '\u9fff' or
            >= '\uf900' and <= '\ufaff' or
            >= '\uff00' and <= '\uffef';
    }

    private static bool ContainsControlCharacter(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<ClientTextLine> SplitLines(string text)
    {
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
            yield return new ClientTextLine(text[start..index], text.Substring(index, endingLength));
            index += endingLength - 1;
            start = index + 1;
        }

        if (start < text.Length)
        {
            yield return new ClientTextLine(text[start..], string.Empty);
        }
    }

    private sealed record SourceCatalog(
        int SourceRows,
        IReadOnlyDictionary<string, string> Values,
        int ConflictedKeys);
}

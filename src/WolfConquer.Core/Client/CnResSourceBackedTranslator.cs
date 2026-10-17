using System.Text;

namespace WolfConquer.Core.Client;

public sealed record CnResSourceBackedTranslationResult(
    int SourceRows,
    int SourceKeys,
    int ConflictedSourceKeys,
    int TargetRows,
    int TranslatedRows,
    int UnmatchedRows,
    int ChangedRows,
    bool Changed);

public static class CnResSourceBackedTranslator
{
    public static CnResSourceBackedTranslationResult PatchUtf8File(
        string targetPath,
        IEnumerable<string> sourcePaths,
        bool includeTargetAsSource = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8);
        var originalBytes = File.ReadAllBytes(targetPath);
        var originalText = encoding.GetString(originalBytes);
        var sourceTexts = ReadSourceTexts(sourcePaths, encoding);
        if (includeTargetAsSource)
        {
            sourceTexts = sourceTexts.Append(originalText);
        }

        var result = Translate(originalText, sourceTexts, out var updatedText);
        if (result.Changed)
        {
            ClientTextPatchWriter.WritePatchedTextPreservingUnchangedBytes(
                targetPath,
                originalBytes,
                originalText,
                updatedText,
                encoding);
        }

        return result;
    }

    public static CnResSourceBackedTranslationResult Translate(
        string targetText,
        IEnumerable<string> sourceTexts,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(targetText);
        ArgumentNullException.ThrowIfNull(sourceTexts);

        var catalog = BuildSourceCatalog(sourceTexts);
        var targetLines = SplitLines(targetText).ToArray();
        var targetRows = 0;
        var translatedRows = 0;
        var unmatchedRows = 0;
        var changedRows = 0;

        for (var index = 0; index < targetLines.Length; index++)
        {
            if (!TryParseKeyValue(targetLines[index].Text, out var key, out var value, out var prefix, out var separator))
            {
                continue;
            }

            targetRows++;
            if (!NeedsTranslation(value))
            {
                continue;
            }

            if (!catalog.Values.TryGetValue(key, out var replacement))
            {
                unmatchedRows++;
                continue;
            }

            if (!IsReplacementCompatibleWithTarget(value, replacement))
            {
                unmatchedRows++;
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
        return new CnResSourceBackedTranslationResult(
            catalog.SourceRows,
            catalog.Values.Count,
            catalog.ConflictedKeys,
            targetRows,
            translatedRows,
            unmatchedRows,
            changedRows,
            !StringComparer.Ordinal.Equals(targetText, updatedText));
    }

    private static IEnumerable<string> ReadSourceTexts(IEnumerable<string> sourcePaths, Encoding encoding)
    {
        var sourceEncoding = GetSourceEncodingWithReplacementFallback(encoding);
        foreach (var sourcePath in sourcePaths)
        {
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            yield return sourceEncoding.GetString(File.ReadAllBytes(sourcePath));
        }
    }

    private static Encoding GetSourceEncodingWithReplacementFallback(Encoding encoding)
    {
        return encoding.CodePage == Encoding.UTF8.CodePage
            ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false)
            : Encoding.GetEncoding(
                encoding.CodePage,
                EncoderFallback.ReplacementFallback,
                DecoderFallback.ReplacementFallback);
    }

    private static SourceCatalog BuildSourceCatalog(IEnumerable<string> sourceTexts)
    {
        var candidates = new Dictionary<string, string>(StringComparer.Ordinal);
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        var sourceRows = 0;

        foreach (var sourceText in sourceTexts)
        {
            foreach (var line in SplitLines(sourceText))
            {
                if (!TryParseSourceRow(line.Text, out var key, out var value) ||
                    !IsSourceBackedReplacement(value))
                {
                    continue;
                }

                sourceRows++;
                foreach (var sourceKey in EnumerateSourceBackedKeys(key))
                {
                    AddSourceCandidate(candidates, conflicts, sourceKey, value);
                }
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

    private static IEnumerable<string> EnumerateSourceBackedKeys(string key)
    {
        yield return key;

        if (StringComparer.Ordinal.Equals(key, "STR_BATTLEPASS_LEV"))
        {
            yield return "STR_BATTLEPASS_Lev";
        }
    }

    private static bool TryParseSourceRow(string line, out string key, out string value)
    {
        if (TryParseLuaStringAssignment(line, out key, out value))
        {
            return true;
        }

        if (TryParseCSharpClientTextReplacement(line, out key, out value) ||
            TryParseCSharpStringConstant(line, out key, out value))
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

        if (keyStart >= line.Length)
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

    private static bool TryParseLuaStringAssignment(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var index = 0;
        SkipWhitespace(line, ref index);
        if (!TryReadCharacter(line, ref index, '[') ||
            !TryReadLuaQuotedString(line, ref index, out key) ||
            !TryReadCharacter(line, ref index, ']'))
        {
            return false;
        }

        SkipWhitespace(line, ref index);
        if (!TryReadCharacter(line, ref index, '='))
        {
            return false;
        }

        SkipWhitespace(line, ref index);
        if (!TryReadLuaQuotedString(line, ref index, out value))
        {
            return false;
        }

        SkipWhitespace(line, ref index);
        return index >= line.Length || line[index] is ',' or ';' || StartsWithLuaComment(line, index);
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
        if (!TryReadCSharpQuotedString(line, ref index, out value))
        {
            return false;
        }

        return IsLikelyClientResourceKey(key);
    }

    private static bool TryParseCSharpStringConstant(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var equalsIndex = line.IndexOf('=');
        if (equalsIndex <= 0)
        {
            return false;
        }

        if (!TryReadLastIdentifier(line[..equalsIndex], out key) ||
            !key.StartsWith("STR_", StringComparison.Ordinal))
        {
            return false;
        }

        var index = equalsIndex + 1;
        SkipWhitespace(line, ref index);
        return TryReadCSharpQuotedString(line, ref index, out value);
    }

    private static bool TryReadLuaQuotedString(string text, ref int index, out string value)
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

    private static bool TryReadLastIdentifier(string text, out string identifier)
    {
        identifier = string.Empty;

        var end = text.Length;
        while (end > 0 && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        var start = end;
        while (start > 0 && (char.IsAsciiLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
        {
            start--;
        }

        if (start == end)
        {
            return false;
        }

        identifier = text[start..end];
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

    private static bool StartsWithLuaComment(string text, int index)
    {
        return index + 1 < text.Length && text[index] == '-' && text[index + 1] == '-';
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
        return SameTokenSequence(ExtractPrintfPlaceholders(targetValue), ExtractPrintfPlaceholders(replacement)) &&
            SameTokenSequence(ExtractEscapedFormattingTokens(targetValue), ExtractEscapedFormattingTokens(replacement)) &&
            ContainsTokenSubsequence(
                ExtractColorAndControlTags(replacement),
                ExtractColorAndControlTags(targetValue));
    }

    private static IReadOnlyList<string> ExtractPrintfPlaceholders(string value)
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

    private static IReadOnlyList<string> ExtractColorAndControlTags(string value)
    {
        var tokens = new List<string>();
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '&' && TryReadAmpersandTag(value, index, out var ampersandTag, out var ampersandEnd))
            {
                tokens.Add(ampersandTag);
                index = ampersandEnd;
                continue;
            }

            if (value[index] == '#' && TryReadHashTag(value, index, out var hashTag, out var hashEnd))
            {
                tokens.Add(hashTag);
                index = hashEnd;
            }
        }

        return tokens;
    }

    private static bool TryReadAmpersandTag(string value, int start, out string token, out int end)
    {
        token = string.Empty;
        end = start;

        var index = start + 1;
        while (index < value.Length && char.IsAsciiLetterOrDigit(value[index]))
        {
            index++;
        }

        if (index == start + 1 || index >= value.Length || value[index] != '&')
        {
            return false;
        }

        token = value[start..(index + 1)];
        end = index;
        return true;
    }

    private static bool TryReadHashTag(string value, int start, out string token, out int end)
    {
        token = string.Empty;
        end = start;

        var index = start + 1;
        while (index < value.Length && char.IsAsciiDigit(value[index]))
        {
            index++;
        }

        if (index == start + 1)
        {
            return false;
        }

        token = value[start..index];
        end = index - 1;
        return true;
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

    private static bool ContainsTokenSubsequence(IReadOnlyList<string> tokens, IReadOnlyList<string> required)
    {
        if (required.Count == 0)
        {
            return true;
        }

        var requiredIndex = 0;
        foreach (var token in tokens)
        {
            if (!StringComparer.Ordinal.Equals(token, required[requiredIndex]))
            {
                continue;
            }

            requiredIndex++;
            if (requiredIndex == required.Count)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLikelyClientResourceKey(string key)
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

    private static bool NeedsTranslation(string value)
    {
        foreach (var character in value)
        {
            if (character is >= '\u3400' and <= '\u9fff' or '\ufffd')
            {
                return true;
            }
        }

        return false;
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

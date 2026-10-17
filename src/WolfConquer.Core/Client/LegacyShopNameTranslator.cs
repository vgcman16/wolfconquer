using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Client;

public sealed record LegacyShopNameTranslationResult(
    int ReferenceShopsScanned,
    int TargetShopsScanned,
    int MatchedTargetShops,
    int TranslatedNames,
    int ChangedNames,
    int UnmatchedNames,
    bool Changed);

public static class LegacyShopNameTranslator
{
    public static LegacyShopNameTranslationResult Translate(
        string targetText,
        string referenceText,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(targetText);
        ArgumentNullException.ThrowIfNull(referenceText);

        var referenceNames = ReadReferenceShopNames(referenceText);
        var targetLines = SplitLines(targetText).ToArray();
        var targetShopCount = 0;
        var matchedTargetShopCount = 0;
        var translatedNames = 0;
        var changedNames = 0;
        var unmatchedNames = 0;
        var changed = false;

        int? currentId = null;
        int? currentNameLineIndex = null;
        string? currentName = null;
        for (var index = 0; index <= targetLines.Length; index++)
        {
            if (index == targetLines.Length || TryParseSection(targetLines[index].Text, out _))
            {
                FlushTargetShop(
                    targetLines,
                    referenceNames,
                    currentId,
                    currentNameLineIndex,
                    currentName,
                    ref targetShopCount,
                    ref matchedTargetShopCount,
                    ref translatedNames,
                    ref changedNames,
                    ref unmatchedNames,
                    ref changed);

                currentId = null;
                currentNameLineIndex = null;
                currentName = null;
                if (index == targetLines.Length)
                {
                    break;
                }

                continue;
            }

            if (TryParseKeyValue(targetLines[index].Text, out var key, out var value, out _, out _) &&
                key.Equals("ID", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var shopId))
            {
                currentId = shopId;
                continue;
            }

            if (TryParseKeyValue(targetLines[index].Text, out key, out value, out _, out _) &&
                key.Equals("Name", StringComparison.OrdinalIgnoreCase))
            {
                currentNameLineIndex = index;
                currentName = value;
            }
        }

        var builder = new StringBuilder();
        foreach (var line in targetLines)
        {
            builder.Append(line.Text);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        return new LegacyShopNameTranslationResult(
            referenceNames.Count,
            targetShopCount,
            matchedTargetShopCount,
            translatedNames,
            changedNames,
            unmatchedNames,
            changed);
    }

    public static LegacyShopNameTranslationResult PatchFile(string targetPath, string referencePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(referencePath);

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var originalBytes = File.ReadAllBytes(targetPath);
        var originalText = encoding.GetString(originalBytes);
        var referenceText = encoding.GetString(File.ReadAllBytes(referencePath));
        var result = Translate(originalText, referenceText, out var updatedText);
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

    private static void FlushTargetShop(
        ClientTextLine[] targetLines,
        IReadOnlyDictionary<int, string> referenceNames,
        int? currentId,
        int? currentNameLineIndex,
        string? currentName,
        ref int targetShopCount,
        ref int matchedTargetShopCount,
        ref int translatedNames,
        ref int changedNames,
        ref int unmatchedNames,
        ref bool changed)
    {
        if (currentId is not { } shopId || currentNameLineIndex is not { } nameLineIndex || currentName is null)
        {
            return;
        }

        targetShopCount++;
        if (!NeedsTranslation(currentName))
        {
            return;
        }

        if (!referenceNames.TryGetValue(shopId, out var referenceName) &&
            !TryTranslateSourceBackedTransformShopName(currentName, out referenceName))
        {
            unmatchedNames++;
            return;
        }

        matchedTargetShopCount++;
        translatedNames++;
        if (StringComparer.Ordinal.Equals(currentName, referenceName))
        {
            return;
        }

        if (!TryParseKeyValue(targetLines[nameLineIndex].Text, out var key, out _, out var prefix, out var separator))
        {
            return;
        }

        targetLines[nameLineIndex] = targetLines[nameLineIndex] with
        {
            Text = prefix + key + separator + referenceName,
        };
        changedNames++;
        changed = true;
    }

    private static IReadOnlyDictionary<int, string> ReadReferenceShopNames(string text)
    {
        var names = new Dictionary<int, string>();
        int? currentId = null;
        string? currentName = null;

        foreach (var line in SplitLines(text).Select(line => line.Text).Append("[End]"))
        {
            if (TryParseSection(line, out _))
            {
                FlushReferenceShop(names, currentId, currentName);
                currentId = null;
                currentName = null;
                continue;
            }

            if (!TryParseKeyValue(line, out var key, out var value, out _, out _))
            {
                continue;
            }

            if (key.Equals("ID", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var shopId))
            {
                currentId = shopId;
            }
            else if (key.Equals("Name", StringComparison.OrdinalIgnoreCase))
            {
                currentName = value;
            }
        }

        return names;
    }

    private static void FlushReferenceShop(Dictionary<int, string> names, int? currentId, string? currentName)
    {
        if (currentId is { } shopId &&
            currentName is { Length: > 0 } name &&
            !NeedsTranslation(name))
        {
            names.TryAdd(shopId, name);
        }
    }

    private static bool TryTranslateSourceBackedTransformShopName(string currentName, out string referenceName)
    {
        const string transformMarker = "\u5916\u5f62";

        var markerIndex = currentName.IndexOf(transformMarker, StringComparison.Ordinal);
        if (markerIndex <= 0 || markerIndex + transformMarker.Length >= currentName.Length)
        {
            referenceName = string.Empty;
            return false;
        }

        var monsterName = currentName[..markerIndex];
        var baseShopName = currentName[(markerIndex + transformMarker.Length)..];
        if (!SourceBackedTransformMonsterNames.TryGetValue(monsterName, out var translatedMonsterName) ||
            !SourceBackedTransformBaseShopNames.TryGetValue(baseShopName, out var translatedBaseShopName))
        {
            referenceName = string.Empty;
            return false;
        }

        referenceName = translatedMonsterName + " Transform " + translatedBaseShopName;
        return true;
    }

    private static bool NeedsTranslation(string value)
    {
        foreach (var character in value)
        {
            if (character is >= '\u3400' and <= '\u9FFF' or '\uFFFD')
            {
                return true;
            }
        }

        return false;
    }

    private static readonly IReadOnlyDictionary<string, string> SourceBackedTransformMonsterNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["\u53eb\u5929\u9e21"] = "Pheasant",
            ["\u9b3c\u65a7\u738b"] = "Blade Ghost",
            ["\u706b\u9f20"] = "Fire Rat",
            ["\u7ffc\u86c7"] = "Winged Snake",
        };

    private static readonly IReadOnlyDictionary<string, string> SourceBackedTransformBaseShopNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["\u53cc\u9f99\u57ce\u6742\u8d27\u5e97"] = "Twin City Grocery",
            ["\u9e23\u51e4\u5821\u836f\u5e97"] = "Phoenix Castle Pharmacy",
            ["\u4e91\u95e8\u5173\u6b66\u5668\u5e97"] = "Cloud Gate Weapon Shop",
            ["\u9ed1\u864e\u57ce\u88c5\u5907\u5e97"] = "Black Tiger City Equipment Shop",
        };

    private static bool TryParseKeyValue(
        string line,
        out string key,
        out string value,
        out string prefix,
        out string separator)
    {
        var keyStart = 0;
        while (keyStart < line.Length && char.IsWhiteSpace(line[keyStart]))
        {
            keyStart++;
        }

        var equalsIndex = line.IndexOf('=', keyStart);
        if (equalsIndex <= keyStart)
        {
            key = string.Empty;
            value = string.Empty;
            prefix = string.Empty;
            separator = string.Empty;
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

    private static bool TryParseSection(string line, out string section)
    {
        var trimmed = line.Trim();
        if (trimmed.Length > 2 && trimmed[0] == '[' && trimmed[^1] == ']')
        {
            section = trimmed[1..^1];
            return true;
        }

        section = string.Empty;
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
}

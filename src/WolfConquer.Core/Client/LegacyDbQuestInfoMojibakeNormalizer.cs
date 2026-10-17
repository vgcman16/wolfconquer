using System.Text;

namespace WolfConquer.Core.Client;

public sealed record LegacyDbQuestInfoMojibakeNormalizeResult(
    int LinesScanned,
    int VisibleTextFieldsScanned,
    int NormalizedFields,
    bool Changed);

public static class LegacyDbQuestInfoMojibakeNormalizer
{
    private static readonly HashSet<string> VisibleExactKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "AcceptText",
        "BeginNpcName",
        "Brief",
        "Content",
        "Desc",
        "Description",
        "Detail",
        "FinishNpcName",
        "Guide",
        "Hint",
        "Intro",
        "Intention",
        "IntentionDesp",
        "IntentDesp",
        "Message",
        "MissionName",
        "Msg",
        "Name",
        "Notice",
        "Prize",
        "Prompt",
        "Remark",
        "Story",
        "Summary",
        "TabName",
        "TaskName",
        "Text",
        "Tip",
        "Tips",
        "Title",
        "Txt",
        "TypeName",
    };

    private static readonly HashSet<string> StructuralExactKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ActivityBeginTime",
        "ActivityEndTime",
        "Auto",
        "BeginNpcId",
        "Color",
        "CompleteFlag",
        "Count",
        "FinishNpcId",
        "First",
        "IntentAmount",
        "Map",
        "MissionId",
        "Num",
        "Prequest",
        "Profession",
        "RecommendInterval",
        "Sex",
        "TabId",
        "TaskNameColor",
        "TotalMission",
        "TypeId",
    };

    private static readonly string[] StructuralSuffixes =
    [
        "Amount",
        "Ani",
        "Auto",
        "Bottom",
        "Color",
        "Cost",
        "Count",
        "Date",
        "Effect",
        "File",
        "First",
        "Flag",
        "Flags",
        "Height",
        "Icon",
        "Id",
        "Ids",
        "Image",
        "Index",
        "Interval",
        "Item",
        "Items",
        "ItemType",
        "Left",
        "Level",
        "Lv",
        "Map",
        "Max",
        "Min",
        "Money",
        "Num",
        "Odds",
        "Order",
        "Path",
        "Percent",
        "Pic",
        "Picture",
        "Price",
        "Probability",
        "Profession",
        "Prequest",
        "Rate",
        "Reward",
        "Right",
        "Sex",
        "Sort",
        "TaskId",
        "Time",
        "Top",
        "Width",
        "X",
        "Y",
    ];

    private static readonly string[] StructuralNameMarkers =
    [
        "Ani",
        "Effect",
        "File",
        "Icon",
        "Image",
        "Path",
        "Pic",
        "Picture",
        "Sound",
    ];

    public static LegacyDbQuestInfoMojibakeNormalizeResult Normalize(
        string questInfoText,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(questInfoText);

        var lines = SplitLines(questInfoText).ToArray();
        var visibleTextFieldsScanned = 0;
        var normalizedFields = 0;

        for (var index = 0; index < lines.Length; index++)
        {
            if (!TryParseKeyValue(lines[index].Text, out var prefix, out var key, out var separator, out var value) ||
                !IsVisibleQuestTextKey(key))
            {
                continue;
            }

            visibleTextFieldsScanned++;
            if (!LegacyQuestVisibleTextNormalizer.TryNormalizeAlreadyEnglishValue(value, out var normalizedValue))
            {
                continue;
            }

            lines[index] = lines[index] with
            {
                Text = prefix + key + separator + normalizedValue,
            };
            normalizedFields++;
        }

        var builder = new StringBuilder(questInfoText.Length);
        foreach (var line in lines)
        {
            builder.Append(line.Text);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        return new LegacyDbQuestInfoMojibakeNormalizeResult(
            lines.Length,
            visibleTextFieldsScanned,
            normalizedFields,
            normalizedFields > 0);
    }

    public static LegacyDbQuestInfoMojibakeNormalizeResult PatchFile(string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var originalBytes = File.ReadAllBytes(targetPath);
        var (encoding, preambleLength) = DetectUtf8Encoding(originalBytes);
        var originalText = encoding.GetString(originalBytes.AsSpan(preambleLength));
        var result = Normalize(originalText, out var updatedText);
        if (result.Changed)
        {
            File.WriteAllText(targetPath, updatedText, encoding);
        }

        return result;
    }

    private static bool IsVisibleQuestTextKey(string key)
    {
        var baseKey = StripRepeatedKeySuffix(key.Trim());
        if (baseKey.Length == 0 ||
            StructuralExactKeys.Contains(baseKey) ||
            HasStructuralNameMarker(baseKey) ||
            EndsWithAny(baseKey, StructuralSuffixes))
        {
            return false;
        }

        return VisibleExactKeys.Contains(baseKey);
    }

    private static string StripRepeatedKeySuffix(string key)
    {
        var end = StripOneRepeatedKeySuffix(key, key.Length);
        while (end < key.Length)
        {
            var nextEnd = StripOneRepeatedKeySuffix(key, end);
            if (nextEnd == end)
            {
                break;
            }

            end = nextEnd;
        }

        return key[..end];
    }

    private static int StripOneRepeatedKeySuffix(string key, int end)
    {
        var suffixEnd = end;
        while (end > 0 && char.IsAsciiDigit(key[end - 1]))
        {
            end--;
        }

        if (end < suffixEnd && end > 0 && key[end - 1] == '_')
        {
            end--;
        }

        return end;
    }

    private static bool HasStructuralNameMarker(string key)
    {
        foreach (var marker in StructuralNameMarkers)
        {
            if (key.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EndsWithAny(string value, IReadOnlyList<string> suffixes)
    {
        foreach (var suffix in suffixes)
        {
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseKeyValue(
        string line,
        out string prefix,
        out string key,
        out string separator,
        out string value)
    {
        prefix = string.Empty;
        key = string.Empty;
        separator = string.Empty;
        value = string.Empty;

        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 ||
            trimmed.StartsWith(';') ||
            trimmed.StartsWith('#') ||
            trimmed.StartsWith("//", StringComparison.Ordinal) ||
            trimmed.StartsWith('['))
        {
            return false;
        }

        var keyStart = 0;
        while (keyStart < line.Length && char.IsWhiteSpace(line[keyStart]))
        {
            keyStart++;
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

        if (keyEnd <= keyStart)
        {
            return false;
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
        return true;
    }

    private static (Encoding Encoding, int PreambleLength) DetectUtf8Encoding(byte[] bytes)
    {
        var emitBom = bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF;
        var encoding = new UTF8Encoding(emitBom, throwOnInvalidBytes: true);
        var preambleLength = emitBom ? 3 : 0;
        encoding.GetString(bytes.AsSpan(preambleLength));
        return (encoding, preambleLength);
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

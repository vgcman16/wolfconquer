using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.Client;

public sealed record LegacyQuestInfoTranslationResult(
    int SectionsScanned,
    int MatchedSections,
    int TranslatedKeys,
    int ChangedKeys,
    int UnmatchedKeys,
    int UnmatchedSections,
    bool Changed);

public static class LegacyQuestInfoTranslator
{
    private static readonly HashSet<string> VisibleExactKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "AcceptText",
        "Brief",
        "Content",
        "Desc",
        "Description",
        "Detail",
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

    private static readonly string[] VisibleSuffixes =
    [
        "Content",
        "Desc",
        "Description",
        "Desp",
        "Detail",
        "Guide",
        "Hint",
        "Intro",
        "Message",
        "Msg",
        "Name",
        "Notice",
        "Prompt",
        "Remark",
        "Story",
        "Summary",
        "Text",
        "Tip",
        "Tips",
        "Title",
        "Txt",
    ];

    private static readonly HashSet<string> StructuralExactKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ActivityBeginTime",
        "ActivityEndTime",
        "Auto",
        "BeginNpcId",
        "Color",
        "CompleteFlag",
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

    private static readonly string[] PathPrefixes =
    [
        "ani/",
        "ani\\",
        "c3/",
        "c3\\",
        "data/",
        "data\\",
        "ini/",
        "ini\\",
        "map/",
        "map\\",
        "scene/",
        "scene\\",
        "sound/",
        "sound\\",
    ];

    private static readonly string[] PathExtensions =
    [
        ".ani",
        ".bmp",
        ".c3",
        ".dat",
        ".dds",
        ".gif",
        ".ini",
        ".jpg",
        ".jpeg",
        ".lua",
        ".mp3",
        ".png",
        ".swf",
        ".tga",
        ".txt",
        ".wav",
        ".wdf",
    ];

    private static readonly Dictionary<string, string> KnownTaskTabTranslations = new(StringComparer.Ordinal)
    {
        ["全部"] = "All",
        ["主线"] = "Main",
        ["支线"] = "Side",
        ["日常"] = "Daily",
        ["跨服"] = "Cross-server",
        ["活动"] = "Events",
        ["周常"] = "Weekly",
    };

    public static LegacyQuestInfoTranslationResult Translate(
        string clientQuestInfoText,
        string referenceQuestInfoText,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(referenceQuestInfoText);

        return Translate(clientQuestInfoText, new[] { referenceQuestInfoText }, LegacyQuestInfoNameFallbackCatalog.Empty, out updatedText);
    }

    public static LegacyQuestInfoTranslationResult Translate(
        string clientQuestInfoText,
        string referenceQuestInfoText,
        LegacyQuestInfoNameFallbackCatalog fallbackCatalog,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(referenceQuestInfoText);

        return Translate(clientQuestInfoText, new[] { referenceQuestInfoText }, fallbackCatalog, out updatedText);
    }

    public static LegacyQuestInfoTranslationResult Translate(
        string clientQuestInfoText,
        IEnumerable<string> referenceQuestInfoTexts,
        out string updatedText)
    {
        return Translate(clientQuestInfoText, referenceQuestInfoTexts, LegacyQuestInfoNameFallbackCatalog.Empty, out updatedText);
    }

    public static LegacyQuestInfoTranslationResult Translate(
        string clientQuestInfoText,
        IEnumerable<string> referenceQuestInfoTexts,
        LegacyQuestInfoNameFallbackCatalog fallbackCatalog,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(clientQuestInfoText);
        ArgumentNullException.ThrowIfNull(referenceQuestInfoTexts);
        ArgumentNullException.ThrowIfNull(fallbackCatalog);

        var referenceCatalog = ParseReferenceCatalog(referenceQuestInfoTexts);
        var builder = new StringBuilder(clientQuestInfoText.Length);
        ReferenceQuestSection? referenceSection = null;
        Dictionary<string, int>? keyOccurrences = null;
        var currentSectionName = string.Empty;
        string? currentMissionId = null;
        var sectionsScanned = 0;
        var matchedSections = 0;
        var unmatchedSections = 0;
        var translatedKeys = 0;
        var changedKeys = 0;
        var unmatchedKeys = 0;

        foreach (var line in SplitLines(clientQuestInfoText))
        {
            var outputText = line.Text;
            if (TryParseSection(line.Text, out var sectionName))
            {
                sectionsScanned++;
                currentSectionName = sectionName;
                currentMissionId = null;
                keyOccurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                if (referenceCatalog.Sections.TryGetValue(sectionName, out referenceSection))
                {
                    matchedSections++;
                }
                else
                {
                    unmatchedSections++;
                    referenceSection = null;
                }
            }
            else if (keyOccurrences is not null &&
                TryParseKeyValue(line.Text, out var prefix, out var key, out var separator, out var value))
            {
                if (key.Equals("MissionId", StringComparison.OrdinalIgnoreCase))
                {
                    var missionId = value.Trim();
                    if (missionId.Length != 0)
                    {
                        currentMissionId = missionId;
                    }
                }

                if (!IsTranslatableClientValue(value))
                {
                    builder.Append(outputText);
                    builder.Append(line.LineEnding);
                    continue;
                }

                var isCompoundNpcField = IsCompoundNpcField(key);
                var isLikelyVisibleTextKey = IsLikelyVisibleTextKey(key);
                if (!isCompoundNpcField && !isLikelyVisibleTextKey)
                {
                    builder.Append(outputText);
                    builder.Append(line.LineEnding);
                    continue;
                }

                var occurrence = GetNextOccurrence(keyOccurrences, key);
                var hasReplacement = false;
                var replacementValue = string.Empty;

                if (isCompoundNpcField)
                {
                    if (referenceSection is not null &&
                        referenceSection.TryGetCompoundNpcReplacement(key, occurrence, value, out replacementValue))
                    {
                        hasReplacement = true;
                    }

                    if (!hasReplacement &&
                        currentMissionId is not null &&
                        ContainsCjkOrReplacement(value) &&
                        referenceCatalog.TryGetCompoundNpcReplacement(currentMissionId, key, value, out replacementValue, out _))
                    {
                        hasReplacement = true;
                    }

                    if (!hasReplacement &&
                        ContainsCjkOrReplacement(value) &&
                        referenceCatalog.TryReplaceCompoundNpcTuple(value, out replacementValue))
                    {
                        hasReplacement = true;
                    }

                    if (!hasReplacement &&
                        ContainsCjkOrReplacement(value) &&
                        fallbackCatalog.TryReplaceCompoundNpcValue(value, out replacementValue))
                    {
                        hasReplacement = true;
                    }

                    if (!hasReplacement &&
                        HasReferenceEvidence(referenceSection, referenceCatalog, currentMissionId))
                    {
                        unmatchedKeys++;
                    }
                }
                else if (isLikelyVisibleTextKey)
                {
                    if (referenceSection is not null &&
                        referenceSection.TryGetValue(key, occurrence, out var referenceValue) &&
                        IsTranslatableReferenceValue(referenceValue))
                    {
                        replacementValue = NormalizeReferenceValue(referenceValue);
                        hasReplacement = true;
                    }

                    if (!hasReplacement &&
                        currentMissionId is not null &&
                        ContainsCjkOrReplacement(value) &&
                        referenceCatalog.TryGetUniqueReferenceValue(currentMissionId, key, out replacementValue, out _))
                    {
                        hasReplacement = true;
                    }

                    if (!hasReplacement &&
                        currentMissionId is not null &&
                        ContainsCjkOrReplacement(value) &&
                        referenceCatalog.TryReplaceAnchoredTokens(currentMissionId, value, out replacementValue))
                    {
                        hasReplacement = true;
                    }

                    if (!hasReplacement &&
                        ContainsCjkOrReplacement(value) &&
                        fallbackCatalog.TryReplaceVisibleValue(key, value, out replacementValue))
                    {
                        hasReplacement = true;
                    }

                    if (!hasReplacement &&
                        TryGetKnownQuestFallback(currentSectionName, key, value, out var fallbackValue))
                    {
                        replacementValue = fallbackValue;
                        hasReplacement = true;
                    }

                    if (!hasReplacement &&
                        LegacyQuestVisibleTextNormalizer.TryNormalizeAlreadyEnglishValue(value, out var normalizedValue))
                    {
                        replacementValue = normalizedValue;
                        hasReplacement = true;
                    }

                    if (!hasReplacement &&
                        HasReferenceEvidence(referenceSection, referenceCatalog, currentMissionId))
                    {
                        unmatchedKeys++;
                    }
                }

                if (hasReplacement)
                {
                    var translatedLine = prefix + key + separator + replacementValue;
                    translatedKeys++;
                    if (!StringComparer.Ordinal.Equals(line.Text, translatedLine))
                    {
                        changedKeys++;
                    }

                    outputText = translatedLine;
                }
            }

            builder.Append(outputText);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        return new LegacyQuestInfoTranslationResult(
            sectionsScanned,
            matchedSections,
            translatedKeys,
            changedKeys,
            unmatchedKeys,
            unmatchedSections,
            !StringComparer.Ordinal.Equals(clientQuestInfoText, updatedText));
    }

    private static ReferenceQuestCatalog ParseReferenceCatalog(IEnumerable<string> texts)
    {
        var catalog = new ReferenceQuestCatalog();

        foreach (var text in texts)
        {
            ArgumentNullException.ThrowIfNull(text);

            ReferenceQuestSection? current = null;
            ReferenceQuestSection? currentSectionMatch = null;
            foreach (var line in SplitLines(text))
            {
                if (TryParseSection(line.Text, out var sectionName))
                {
                    catalog.AddMissionSection(current);
                    current = new ReferenceQuestSection();
                    if (!catalog.Sections.TryGetValue(sectionName, out currentSectionMatch))
                    {
                        currentSectionMatch = current;
                        catalog.Sections.Add(sectionName, currentSectionMatch);
                    }

                    continue;
                }

                if (current is not null &&
                    TryParseKeyValue(line.Text, out _, out var key, out _, out var value))
                {
                    current.Add(key, value);
                    if (!ReferenceEquals(current, currentSectionMatch))
                    {
                        currentSectionMatch?.Add(key, value);
                    }

                    if (key.Equals("MissionId", StringComparison.OrdinalIgnoreCase))
                    {
                        current.MissionId = value.Trim();
                    }
                }
            }

            catalog.AddMissionSection(current);
        }

        return catalog;
    }

    private static int GetNextOccurrence(Dictionary<string, int> occurrences, string key)
    {
        var next = occurrences.GetValueOrDefault(key) + 1;
        occurrences[key] = next;
        return next;
    }

    private static bool IsLikelyVisibleTextKey(string key)
    {
        var baseKey = StripRepeatedKeySuffix(key.Trim());
        if (baseKey.Length == 0 ||
            StructuralExactKeys.Contains(baseKey) ||
            HasStructuralNameMarker(baseKey) ||
            EndsWithAny(baseKey, StructuralSuffixes))
        {
            return false;
        }

        if (VisibleExactKeys.Contains(baseKey))
        {
            return true;
        }

        return EndsWithAny(baseKey, VisibleSuffixes);
    }

    private static bool IsCompoundNpcField(string key)
    {
        var normalizedKey = StripRepeatedKeySuffix(key.Trim());
        return normalizedKey.Equals("BeginNpcId", StringComparison.OrdinalIgnoreCase) ||
            normalizedKey.Equals("FinishNpcId", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetCompoundNpcReferenceKeys(string key)
    {
        yield return key;

        var baseKey = StripRepeatedKeySuffix(key.Trim());
        if (!baseKey.Equals(key, StringComparison.OrdinalIgnoreCase))
        {
            yield return baseKey;
        }
    }

    private static bool TryBuildCompoundNpcReplacement(string targetValue, string referenceValue, out string replacementValue)
    {
        replacementValue = string.Empty;
        var targetParts = targetValue.Split(',');
        var referenceParts = referenceValue.Split(',');
        const int numericPartCount = 4;
        if (targetParts.Length < numericPartCount + 2 ||
            referenceParts.Length < numericPartCount + 2 ||
            targetParts.Length != referenceParts.Length)
        {
            return false;
        }

        for (var index = 0; index < numericPartCount; index++)
        {
            if (!NumericPartsEqual(targetParts[index], referenceParts[index]))
            {
                return false;
            }
        }

        var replacementParts = new string[targetParts.Length];
        for (var index = 0; index < numericPartCount; index++)
        {
            replacementParts[index] = targetParts[index];
        }

        for (var index = numericPartCount; index < referenceParts.Length; index++)
        {
            replacementParts[index] = referenceParts[index].Trim();
        }

        replacementValue = string.Join(',', replacementParts);
        return true;
    }

    private static bool TryReadCompoundNpcTupleKey(string value, out CompoundNpcTupleKey key)
    {
        key = default;
        var parts = value.Split(',');
        if (parts.Length < 4 ||
            !long.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var npcId) ||
            !long.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mapId) ||
            !long.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
            !long.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
        {
            return false;
        }

        key = new CompoundNpcTupleKey(npcId, mapId, x, y);
        return true;
    }

    private static bool NumericPartsEqual(string left, string right)
    {
        var leftTrimmed = left.Trim();
        var rightTrimmed = right.Trim();
        if (long.TryParse(leftTrimmed, out var leftValue) &&
            long.TryParse(rightTrimmed, out var rightValue))
        {
            return leftValue == rightValue;
        }

        return StringComparer.Ordinal.Equals(leftTrimmed, rightTrimmed);
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

    private static bool IsTranslatableClientValue(string value)
    {
        return IsTranslatableValue(value, requireAsciiLetter: false);
    }

    private static bool IsTranslatableReferenceValue(string value)
    {
        return IsTranslatableValue(value, requireAsciiLetter: true);
    }

    private static bool IsTranslatableValue(string value, bool requireAsciiLetter)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0 ||
            IsNumericLike(trimmed) ||
            IsHexLike(trimmed) ||
            IsPathLike(trimmed) ||
            IsStructuredListLike(trimmed))
        {
            return false;
        }

        return requireAsciiLetter ? ContainsAsciiLetter(trimmed) : ContainsTextSignal(trimmed);
    }

    private static string NormalizeReferenceValue(string value)
    {
        return LegacyQuestVisibleTextNormalizer.NormalizeReferenceValue(value);
    }

    private static bool ContainsCjkOrReplacement(string value)
    {
        return LegacyQuestVisibleTextNormalizer.ContainsCjkOrReplacement(value);
    }

    private static bool HasReferenceEvidence(
        ReferenceQuestSection? referenceSection,
        ReferenceQuestCatalog referenceCatalog,
        string? missionId)
    {
        return referenceSection is not null ||
            (missionId is not null && referenceCatalog.ContainsMission(missionId));
    }

    private static bool TryGetKnownQuestFallback(
        string section,
        string key,
        string value,
        out string replacementValue)
    {
        if (StringComparer.OrdinalIgnoreCase.Equals(section, "TaskTab") &&
            key.StartsWith("TabName", StringComparison.OrdinalIgnoreCase) &&
            KnownTaskTabTranslations.TryGetValue(value.Trim(), out var knownValue))
        {
            replacementValue = knownValue;
            return true;
        }

        replacementValue = string.Empty;
        return false;
    }

    private static bool IsNumericLike(string value)
    {
        var candidate = value.EndsWith('%') ? value[..^1] : value;
        return decimal.TryParse(
            candidate,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out _);
    }

    private static bool IsHexLike(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return value.Length > 2 && IsAllHex(value.AsSpan(2));
        }

        return value.Length is 7 or 9 &&
            value[0] == '#' &&
            IsAllHex(value.AsSpan(1));
    }

    private static bool IsPathLike(string value)
    {
        var normalized = value.Replace('\\', '/');
        foreach (var prefix in PathPrefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (normalized.Contains("://", StringComparison.Ordinal) ||
            normalized.Contains(":/", StringComparison.Ordinal))
        {
            return true;
        }

        if (ContainsWhitespace(value))
        {
            return false;
        }

        foreach (var extension in PathExtensions)
        {
            if (value.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsStructuredListLike(string value)
    {
        return IsStructuralDelimitedList(value, ',') ||
            IsStructuralDelimitedList(value, '|') ||
            IsStructuralDelimitedList(value, ';') ||
            IsStructuralWhitespaceList(value);
    }

    private static bool IsStructuralDelimitedList(string value, char delimiter)
    {
        if (!value.Contains(delimiter, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = value.Split(delimiter, StringSplitOptions.TrimEntries);
        var nonEmptyParts = 0;
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            nonEmptyParts++;
            if (!IsStructuralToken(part))
            {
                return false;
            }
        }

        return nonEmptyParts > 1;
    }

    private static bool IsStructuralWhitespaceList(string value)
    {
        if (!ContainsWhitespace(value))
        {
            return false;
        }

        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length <= 1)
        {
            return false;
        }

        foreach (var part in parts)
        {
            if (!IsStructuralToken(part))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsStructuralToken(string value)
    {
        var token = value.Trim().Trim('[', ']', '(', ')');
        return token.Length != 0 && (IsNumericLike(token) || IsHexLike(token));
    }

    private static bool ContainsTextSignal(string value)
    {
        foreach (var character in value)
        {
            if (char.IsLetter(character) || character > '\u007f')
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAsciiLetter(string value)
    {
        foreach (var character in value)
        {
            if (char.IsAsciiLetter(character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReplaceAnchoredTokens(
        string value,
        ReferenceTokenCatalog tokenCatalog,
        out string replacementValue)
    {
        replacementValue = value;
        var builder = default(StringBuilder);
        var copiedUntil = 0;
        var searchIndex = 0;
        while (TryFindNextAnchoredToken(value, searchIndex, out var token))
        {
            searchIndex = token.EndIndex + 1;
            if (!ContainsCjkOrReplacement(token.Name) ||
                !tokenCatalog.TryGetValue(token.Kind, token.Payload, out var replacementName))
            {
                continue;
            }

            builder ??= new StringBuilder(value.Length);
            builder.Append(value, copiedUntil, token.NameStart - copiedUntil);
            builder.Append(replacementName);
            copiedUntil = token.NameStart + token.NameLength;
        }

        if (builder is null)
        {
            return false;
        }

        builder.Append(value, copiedUntil, value.Length - copiedUntil);
        replacementValue = builder.ToString();
        return !StringComparer.Ordinal.Equals(value, replacementValue);
    }

    private static bool TryFindNextAnchoredToken(string value, int startIndex, out AnchoredToken token)
    {
        var openIndex = value.IndexOf('[', startIndex);
        while (openIndex >= 0)
        {
            var closeIndex = value.IndexOf(']', openIndex + 1);
            if (closeIndex < 0)
            {
                break;
            }

            if (TryParseAnchoredToken(value, openIndex, closeIndex, out token))
            {
                return true;
            }

            openIndex = value.IndexOf('[', openIndex + 1);
        }

        token = default;
        return false;
    }

    private static bool TryParseAnchoredToken(string value, int openIndex, int closeIndex, out AnchoredToken token)
    {
        var index = openIndex + 1;
        while (index < closeIndex && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        var kindStart = index;
        while (index < closeIndex && !char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        var kind = value[kindStart..index];
        if (!IsSupportedAnchoredTokenKind(kind))
        {
            token = default;
            return false;
        }

        if (index >= closeIndex || !char.IsWhiteSpace(value[index]))
        {
            token = default;
            return false;
        }

        while (index < closeIndex && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        var nameStart = index;
        var commaIndex = value.IndexOf(',', nameStart, closeIndex - nameStart);
        if (commaIndex < 0)
        {
            token = default;
            return false;
        }

        var nameEnd = commaIndex;
        while (nameEnd > nameStart && char.IsWhiteSpace(value[nameEnd - 1]))
        {
            nameEnd--;
        }

        if (nameEnd <= nameStart)
        {
            token = default;
            return false;
        }

        var payload = NormalizeAnchoredTokenPayload(value[(commaIndex + 1)..closeIndex]);
        if (payload.Length == 0)
        {
            token = default;
            return false;
        }

        token = new AnchoredToken(
            kind.ToLowerInvariant(),
            payload,
            value[nameStart..nameEnd],
            nameStart,
            nameEnd - nameStart,
            closeIndex);
        return true;
    }

    private static bool IsSupportedAnchoredTokenKind(string kind)
    {
        return kind.Equals("npc", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("item_t", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAnchoredTokenPayload(string payload)
    {
        var parts = payload.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        for (var index = 0; index < parts.Length; index++)
        {
            if (parts[index].Length == 0)
            {
                return string.Empty;
            }
        }

        return string.Join(',', parts);
    }

    private static bool ContainsWhitespace(string value)
    {
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAllHex(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseSection(string line, out string section)
    {
        var trimmed = line.Trim();
        if (trimmed.Length > 2 && trimmed[0] == '[' && trimmed[^1] == ']')
        {
            section = trimmed[1..^1].Trim();
            return section.Length != 0;
        }

        section = string.Empty;
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

    private sealed class ReferenceQuestSection
    {
        private readonly Dictionary<string, List<string>> _values = new(StringComparer.OrdinalIgnoreCase);

        public string? MissionId { get; set; }

        public void Add(string key, string value)
        {
            if (!_values.TryGetValue(key, out var values))
            {
                values = [];
                _values.Add(key, values);
            }

            values.Add(value);
        }

        public bool TryGetValue(string key, int occurrence, out string value)
        {
            if (_values.TryGetValue(key, out var values) &&
                occurrence > 0 &&
                occurrence <= values.Count)
            {
                value = values[occurrence - 1];
                return true;
            }

            value = string.Empty;
            return false;
        }

        public bool TryGetCompoundNpcReplacement(
            string key,
            int occurrence,
            string targetValue,
            out string replacementValue)
        {
            foreach (var candidateKey in GetCompoundNpcReferenceKeys(key))
            {
                if (TryGetValue(candidateKey, occurrence, out var referenceValue) &&
                    IsTranslatableReferenceValue(referenceValue) &&
                    TryBuildCompoundNpcReplacement(targetValue, NormalizeReferenceValue(referenceValue), out replacementValue))
                {
                    return true;
                }
            }

            replacementValue = string.Empty;
            return false;
        }

        public IEnumerable<KeyValuePair<string, string>> Values
        {
            get
            {
                foreach (var values in _values)
                {
                    foreach (var value in values.Value)
                    {
                        yield return new KeyValuePair<string, string>(values.Key, value);
                    }
                }
            }
        }
    }

    private sealed class ReferenceQuestCatalog
    {
        private readonly Dictionary<string, ReferenceMission> _missions = new(StringComparer.Ordinal);
        private readonly CompoundNpcTupleCatalog _compoundNpcTuples = new();

        public Dictionary<string, ReferenceQuestSection> Sections { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void AddMissionSection(ReferenceQuestSection? section)
        {
            if (section?.MissionId is not { Length: > 0 } missionId)
            {
                return;
            }

            if (missionId.Length == 0)
            {
                return;
            }

            if (!_missions.TryGetValue(missionId, out var mission))
            {
                mission = new ReferenceMission();
                _missions.Add(missionId, mission);
            }

            mission.AddSection(section);
            foreach (var field in section.Values)
            {
                if (IsCompoundNpcField(field.Key))
                {
                    _compoundNpcTuples.Add(field.Value);
                }
            }
        }

        public bool ContainsMission(string missionId)
        {
            return _missions.ContainsKey(missionId);
        }

        public bool TryGetUniqueReferenceValue(string missionId, string key, out string value, out bool ambiguous)
        {
            if (_missions.TryGetValue(missionId, out var mission))
            {
                return mission.TryGetUniqueReferenceValue(key, out value, out ambiguous);
            }

            value = string.Empty;
            ambiguous = false;
            return false;
        }

        public bool TryGetCompoundNpcReplacement(
            string missionId,
            string key,
            string targetValue,
            out string replacementValue,
            out bool ambiguous)
        {
            if (_missions.TryGetValue(missionId, out var mission))
            {
                return mission.TryGetCompoundNpcReplacement(key, targetValue, out replacementValue, out ambiguous);
            }

            replacementValue = string.Empty;
            ambiguous = false;
            return false;
        }

        public bool TryReplaceAnchoredTokens(string missionId, string targetValue, out string replacementValue)
        {
            if (_missions.TryGetValue(missionId, out var mission))
            {
                return mission.TryReplaceAnchoredTokens(targetValue, out replacementValue);
            }

            replacementValue = string.Empty;
            return false;
        }

        public bool TryReplaceCompoundNpcTuple(string targetValue, out string replacementValue)
        {
            return _compoundNpcTuples.TryReplaceValue(targetValue, out replacementValue);
        }
    }

    private sealed class ReferenceMission
    {
        private readonly Dictionary<string, List<string>> _values = new(StringComparer.OrdinalIgnoreCase);
        private readonly ReferenceTokenCatalog _tokens = new();

        public void AddSection(ReferenceQuestSection section)
        {
            foreach (var field in section.Values)
            {
                if (!IsLikelyVisibleTextKey(field.Key) && !IsCompoundNpcField(field.Key))
                {
                    continue;
                }

                var normalizedValue = NormalizeReferenceValue(field.Value);
                if (!IsTranslatableReferenceValue(normalizedValue) ||
                    ContainsCjkOrReplacement(normalizedValue))
                {
                    continue;
                }

                if (!_values.TryGetValue(field.Key, out var values))
                {
                    values = [];
                    _values.Add(field.Key, values);
                }

                values.Add(normalizedValue);

                if (IsLikelyVisibleTextKey(field.Key))
                {
                    _tokens.AddTokens(normalizedValue);
                }
            }
        }

        public bool TryGetUniqueReferenceValue(string key, out string value, out bool ambiguous)
        {
            value = string.Empty;
            ambiguous = false;
            if (!_values.TryGetValue(key, out var values))
            {
                return false;
            }

            return TryGetOnlyDistinctValue(values, out value, out ambiguous);
        }

        public bool TryGetCompoundNpcReplacement(
            string key,
            string targetValue,
            out string replacementValue,
            out bool ambiguous)
        {
            replacementValue = string.Empty;
            ambiguous = false;
            var matches = new List<string>();
            foreach (var candidateKey in GetCompoundNpcReferenceKeys(key))
            {
                if (!_values.TryGetValue(candidateKey, out var values))
                {
                    continue;
                }

                foreach (var value in values)
                {
                    if (TryBuildCompoundNpcReplacement(targetValue, value, out var candidate))
                    {
                        matches.Add(candidate);
                    }
                }
            }

            return TryGetOnlyDistinctValue(matches, out replacementValue, out ambiguous);
        }

        public bool TryReplaceAnchoredTokens(string targetValue, out string replacementValue)
        {
            return LegacyQuestInfoTranslator.TryReplaceAnchoredTokens(targetValue, _tokens, out replacementValue);
        }

        private static bool TryGetOnlyDistinctValue(
            IEnumerable<string> values,
            out string value,
            out bool ambiguous)
        {
            value = string.Empty;
            ambiguous = false;
            foreach (var candidate in values)
            {
                if (value.Length == 0)
                {
                    value = candidate;
                    continue;
                }

                if (!StringComparer.Ordinal.Equals(value, candidate))
                {
                    ambiguous = true;
                    return false;
                }
            }

            return value.Length != 0;
        }
    }

    private sealed class ReferenceTokenCatalog
    {
        private readonly Dictionary<ReferenceTokenKey, ReferenceTokenValue> _values = new();

        public void AddTokens(string value)
        {
            var searchIndex = 0;
            while (TryFindNextAnchoredToken(value, searchIndex, out var token))
            {
                searchIndex = token.EndIndex + 1;
                if (ContainsCjkOrReplacement(token.Name) || !ContainsAsciiLetter(token.Name))
                {
                    continue;
                }

                var key = new ReferenceTokenKey(token.Kind, token.Payload);
                if (!_values.TryGetValue(key, out var field))
                {
                    _values.Add(key, new ReferenceTokenValue(token.Name));
                    continue;
                }

                field.Add(token.Name);
            }
        }

        public bool TryGetValue(string kind, string payload, out string value)
        {
            if (_values.TryGetValue(new ReferenceTokenKey(kind, payload), out var field) &&
                !field.Ambiguous)
            {
                value = field.Value;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }

    private sealed class CompoundNpcTupleCatalog
    {
        private readonly Dictionary<CompoundNpcTupleKey, ReferenceTokenValue> _values = new();

        public void Add(string value)
        {
            if (!TryReadCompoundNpcTupleKey(value, out var key) ||
                !IsTranslatableReferenceValue(value) ||
                ContainsCjkOrReplacement(value))
            {
                return;
            }

            var normalizedValue = NormalizeReferenceValue(value);
            if (!_values.TryGetValue(key, out var field))
            {
                _values.Add(key, new ReferenceTokenValue(normalizedValue));
                return;
            }

            field.Add(normalizedValue);
        }

        public bool TryReplaceValue(string targetValue, out string replacementValue)
        {
            replacementValue = string.Empty;
            if (!TryReadCompoundNpcTupleKey(targetValue, out var key) ||
                !_values.TryGetValue(key, out var field) ||
                field.Ambiguous)
            {
                return false;
            }

            return TryBuildCompoundNpcReplacement(targetValue, field.Value, out replacementValue);
        }
    }

    private readonly record struct AnchoredToken(
        string Kind,
        string Payload,
        string Name,
        int NameStart,
        int NameLength,
        int EndIndex);

    private readonly record struct ReferenceTokenKey(string Kind, string Payload);

    private readonly record struct CompoundNpcTupleKey(long NpcId, long MapId, long X, long Y);

    private sealed class ReferenceTokenValue(string value)
    {
        public string Value { get; } = value;

        public bool Ambiguous { get; private set; }

        public void Add(string value)
        {
            if (!StringComparer.Ordinal.Equals(Value, value))
            {
                Ambiguous = true;
            }
        }
    }
}

public sealed class LegacyQuestInfoNameFallbackCatalog
{
    public static LegacyQuestInfoNameFallbackCatalog Empty { get; } = new(
        new Dictionary<int, string>(),
        new Dictionary<int, string>(),
        new Dictionary<int, string>(),
        new Dictionary<int, string>());

    private LegacyQuestInfoNameFallbackCatalog(
        IReadOnlyDictionary<int, string> itemNames,
        IReadOnlyDictionary<int, string> npcNames,
        IReadOnlyDictionary<int, string> monsterNames,
        IReadOnlyDictionary<int, string> mapNames)
    {
        ItemNames = itemNames;
        NpcNames = npcNames;
        MonsterNames = monsterNames;
        MapNames = mapNames;
    }

    public IReadOnlyDictionary<int, string> ItemNames { get; }

    public IReadOnlyDictionary<int, string> NpcNames { get; }

    public IReadOnlyDictionary<int, string> MonsterNames { get; }

    public IReadOnlyDictionary<int, string> MapNames { get; }

    public static LegacyQuestInfoNameFallbackCatalog FromSourcePaths(IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var builder = new CatalogBuilder();
        foreach (var sourcePath in ExpandSourcePaths(sourcePaths))
        {
            AddSourcePath(builder, sourcePath);
        }

        return builder.Build();
    }

    internal bool TryReplaceCompoundNpcValue(string value, out string replacementValue)
    {
        replacementValue = value;
        if (NpcNames.Count == 0 && MapNames.Count == 0)
        {
            return false;
        }

        var parts = value.Split(',');
        const int numericPartCount = 4;
        if (parts.Length < numericPartCount + 1 ||
            !TryParseInt(parts[0], out var npcId) ||
            !TryParseInt(parts[1], out var mapId) ||
            !TryParseInt(parts[2], out _) ||
            !TryParseInt(parts[3], out _))
        {
            return false;
        }

        var changed = false;
        if (parts.Length > numericPartCount &&
            ContainsCjkOrReplacement(parts[numericPartCount]) &&
            NpcNames.TryGetValue(npcId, out var npcName))
        {
            parts[numericPartCount] = npcName;
            changed = true;
        }

        if (parts.Length > numericPartCount + 1 &&
            ContainsCjkOrReplacement(parts[numericPartCount + 1]) &&
            MapNames.TryGetValue(mapId, out var mapName))
        {
            parts[numericPartCount + 1] = mapName;
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        replacementValue = string.Join(',', parts);
        return !StringComparer.Ordinal.Equals(value, replacementValue);
    }

    internal bool TryReplaceVisibleValue(string key, string value, out string replacementValue)
    {
        replacementValue = value;
        var changed = false;
        if (TryReplaceEmbeddedTokenNames(replacementValue, out var tokenReplacementValue))
        {
            replacementValue = tokenReplacementValue;
            changed = true;
        }

        if (TryReplaceItemTokenNamesFromCheckItems(replacementValue, out var checkedItemReplacementValue))
        {
            replacementValue = checkedItemReplacementValue;
            changed = true;
        }

        if (TryReplaceRewardLabels(key, replacementValue, out var rewardReplacementValue))
        {
            replacementValue = rewardReplacementValue;
            changed = true;
        }

        if (TryReplaceRewardItemNamesFromAnchors(key, replacementValue, out var rewardItemReplacementValue))
        {
            replacementValue = rewardItemReplacementValue;
            changed = true;
        }

        return changed && !StringComparer.Ordinal.Equals(value, replacementValue);
    }

    private bool TryReplaceEmbeddedTokenNames(string value, out string replacementValue)
    {
        replacementValue = value;
        var builder = default(StringBuilder);
        var copiedUntil = 0;
        var searchIndex = 0;
        while (TryFindNextAnchoredToken(value, searchIndex, out var token))
        {
            searchIndex = token.EndIndex + 1;
            if (!ContainsCjkOrReplacement(token.Name) ||
                !TryGetAnchoredTokenReplacement(token, out var replacementName))
            {
                continue;
            }

            builder ??= new StringBuilder(value.Length);
            builder.Append(value, copiedUntil, token.NameStart - copiedUntil);
            builder.Append(replacementName);
            copiedUntil = token.NameStart + token.NameLength;
        }

        if (builder is null)
        {
            return false;
        }

        builder.Append(value, copiedUntil, value.Length - copiedUntil);
        replacementValue = builder.ToString();
        return !StringComparer.Ordinal.Equals(value, replacementValue);
    }

    private bool TryReplaceItemTokenNamesFromCheckItems(string value, out string replacementValue)
    {
        replacementValue = value;
        if (ItemNames.Count == 0)
        {
            return false;
        }

        var builder = default(StringBuilder);
        var copiedUntil = 0;
        var searchIndex = 0;
        while (TryFindNextAnchoredToken(value, searchIndex, out var token))
        {
            searchIndex = token.EndIndex + 1;
            if (!token.Kind.Equals("item_t", StringComparison.Ordinal) ||
                !ContainsCjkOrReplacement(token.Name) ||
                !TryParseFollowingCheckItemId(value, token.EndIndex + 1, out var itemId) ||
                !ItemNames.TryGetValue(itemId, out var replacementName))
            {
                continue;
            }

            builder ??= new StringBuilder(value.Length);
            builder.Append(value, copiedUntil, token.NameStart - copiedUntil);
            builder.Append(replacementName);
            copiedUntil = token.NameStart + token.NameLength;
        }

        if (builder is null)
        {
            return false;
        }

        builder.Append(value, copiedUntil, value.Length - copiedUntil);
        replacementValue = builder.ToString();
        return !StringComparer.Ordinal.Equals(value, replacementValue);
    }

    private bool TryGetAnchoredTokenReplacement(AnchoredToken token, out string replacementName)
    {
        replacementName = string.Empty;
        var payloadParts = token.Payload.Split(',', StringSplitOptions.TrimEntries);
        if (payloadParts.Length == 0 || !TryParseInt(payloadParts[0], out var id))
        {
            return false;
        }

        var source = token.Kind switch
        {
            "item_t" => ItemNames,
            "npc" => NpcNames,
            "monster" => MonsterNames,
            "mob" => MonsterNames,
            _ => null,
        };

        if (source is not null && source.TryGetValue(id, out var candidate))
        {
            replacementName = candidate;
            return true;
        }

        if (token.Kind.Equals("mob", StringComparison.Ordinal) &&
            MapNames.TryGetValue(id, out var mapName))
        {
            replacementName = mapName;
            return true;
        }

        return false;
    }

    private bool TryReplaceRewardItemNamesFromAnchors(string key, string value, out string replacementValue)
    {
        replacementValue = value;
        if (ItemNames.Count == 0 || !IsRewardTextKey(key))
        {
            return false;
        }

        var builder = default(StringBuilder);
        var copiedUntil = 0;
        var searchIndex = 0;
        while (TryFindNextBracketedItemAnchor(value, searchIndex, out var anchor))
        {
            searchIndex = anchor.CloseIndex + 1;
            if (!ItemNames.TryGetValue(anchor.ItemId, out var replacementName) ||
                !TryFindSimplePrecedingItemName(value, anchor.OpenIndex, out var nameStart, out var nameLength) ||
                nameStart < copiedUntil)
            {
                continue;
            }

            builder ??= new StringBuilder(value.Length);
            builder.Append(value, copiedUntil, nameStart - copiedUntil);
            builder.Append(replacementName);
            copiedUntil = nameStart + nameLength;
        }

        if (builder is null)
        {
            return false;
        }

        builder.Append(value, copiedUntil, value.Length - copiedUntil);
        replacementValue = builder.ToString();
        return !StringComparer.Ordinal.Equals(value, replacementValue);
    }

    private static bool TryReplaceRewardLabels(string key, string value, out string replacementValue)
    {
        replacementValue = value;
        if (!IsRewardTextKey(key))
        {
            return false;
        }

        var builder = default(StringBuilder);
        var copiedUntil = 0;
        for (var index = 0; index < value.Length; index++)
        {
            if (!TryGetRewardLabelAt(value, index, out var sourceLabel, out var targetLabel))
            {
                continue;
            }

            builder ??= new StringBuilder(value.Length);
            builder.Append(value, copiedUntil, index - copiedUntil);
            builder.Append(targetLabel);
            index += sourceLabel.Length - 1;
            copiedUntil = index + 1;
        }

        if (builder is null)
        {
            return false;
        }

        builder.Append(value, copiedUntil, value.Length - copiedUntil);
        replacementValue = builder.ToString();
        return !StringComparer.Ordinal.Equals(value, replacementValue);
    }

    private static bool IsRewardTextKey(string key)
    {
        var normalized = key.Trim();
        return normalized.Equals("Prize", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Reward", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("Prize", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("Reward", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetRewardLabelAt(
        string value,
        int index,
        out string sourceLabel,
        out string targetLabel)
    {
        foreach (var label in RewardLabelTranslations)
        {
            if (index + label.Source.Length <= value.Length &&
                value.AsSpan(index, label.Source.Length).SequenceEqual(label.Source) &&
                IsRewardLabelBoundaryBefore(value, index) &&
                IsRewardLabelBoundaryAfter(value, index + label.Source.Length))
            {
                sourceLabel = label.Source;
                targetLabel = label.Target;
                return true;
            }
        }

        sourceLabel = string.Empty;
        targetLabel = string.Empty;
        return false;
    }

    private static bool IsRewardLabelBoundaryBefore(string value, int index)
    {
        if (index == 0)
        {
            return true;
        }

        var previous = value[index - 1];
        return char.IsWhiteSpace(previous) || IsRewardLabelDelimiter(previous);
    }

    private static bool IsRewardLabelBoundaryAfter(string value, int index)
    {
        if (index >= value.Length)
        {
            return true;
        }

        var next = value[index];
        return char.IsWhiteSpace(next) ||
            IsRewardLabelDelimiter(next) ||
            next is ':' or '\uFF1A' or '=';
    }

    private static bool IsRewardLabelDelimiter(char character)
    {
        return character is ',' or ';' or '|' or '/' or '(' or ')' or '[' or ']' or '{' or '}' or '\uFF0C' or '\u3001';
    }

    private static bool TryParseFollowingCheckItemId(string value, int startIndex, out int itemId)
    {
        itemId = 0;
        var index = startIndex;
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        if (index >= value.Length || value[index] != '[')
        {
            return false;
        }

        var closeIndex = value.IndexOf(']', index + 1);
        return closeIndex > index &&
            TryParseBracketedIdToken(value, index, closeIndex, "chk_item", out itemId);
    }

    private static bool TryFindNextBracketedItemAnchor(
        string value,
        int startIndex,
        out BracketedItemAnchor anchor)
    {
        var openIndex = value.IndexOf('[', startIndex);
        while (openIndex >= 0)
        {
            var closeIndex = value.IndexOf(']', openIndex + 1);
            if (closeIndex < 0)
            {
                break;
            }

            if (TryParseBracketedIdToken(value, openIndex, closeIndex, "item", out var itemId))
            {
                anchor = new BracketedItemAnchor(itemId, openIndex, closeIndex);
                return true;
            }

            openIndex = value.IndexOf('[', openIndex + 1);
        }

        anchor = default;
        return false;
    }

    private static bool TryParseBracketedIdToken(
        string value,
        int openIndex,
        int closeIndex,
        string expectedKind,
        out int id)
    {
        id = 0;
        var index = openIndex + 1;
        while (index < closeIndex && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        var kindStart = index;
        while (index < closeIndex && !char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        if (index <= kindStart ||
            !value.AsSpan(kindStart, index - kindStart).Equals(expectedKind, StringComparison.OrdinalIgnoreCase) ||
            index >= closeIndex ||
            !char.IsWhiteSpace(value[index]))
        {
            return false;
        }

        while (index < closeIndex && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        var idStart = index;
        while (index < closeIndex && char.IsAsciiDigit(value[index]))
        {
            index++;
        }

        if (index <= idStart ||
            !TryParseInt(value[idStart..index], out id))
        {
            return false;
        }

        while (index < closeIndex && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        return index < closeIndex && value[index] == ',';
    }

    private static bool TryFindSimplePrecedingItemName(
        string value,
        int anchorOpenIndex,
        out int nameStart,
        out int nameLength)
    {
        nameStart = 0;
        nameLength = 0;
        var nameEnd = anchorOpenIndex;
        while (nameEnd > 0 && char.IsWhiteSpace(value[nameEnd - 1]))
        {
            nameEnd--;
        }

        if (nameEnd == 0 || !IsSimpleCjkItemNameCharacter(value[nameEnd - 1]))
        {
            return false;
        }

        nameStart = nameEnd - 1;
        while (nameStart > 0 && IsSimpleCjkItemNameCharacter(value[nameStart - 1]))
        {
            nameStart--;
        }

        nameLength = nameEnd - nameStart;
        return nameLength > 0 && ContainsCjkOrReplacement(value.Substring(nameStart, nameLength));
    }

    private static bool IsSimpleCjkItemNameCharacter(char character)
    {
        return character == '\u00B7' ||
            character == '\u30FB' ||
            character is >= '\u3400' and <= '\u9FFF';
    }

    private static bool TryFindNextAnchoredToken(string value, int startIndex, out AnchoredToken token)
    {
        var openIndex = value.IndexOf('[', startIndex);
        while (openIndex >= 0)
        {
            var closeIndex = value.IndexOf(']', openIndex + 1);
            if (closeIndex < 0)
            {
                break;
            }

            if (TryParseAnchoredToken(value, openIndex, closeIndex, out token))
            {
                return true;
            }

            openIndex = value.IndexOf('[', openIndex + 1);
        }

        token = default;
        return false;
    }

    private static bool TryParseAnchoredToken(string value, int openIndex, int closeIndex, out AnchoredToken token)
    {
        var index = openIndex + 1;
        while (index < closeIndex && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        var kindStart = index;
        while (index < closeIndex && !char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        var kind = value[kindStart..index];
        if (!IsSupportedAnchoredTokenKind(kind))
        {
            token = default;
            return false;
        }

        if (index >= closeIndex || !char.IsWhiteSpace(value[index]))
        {
            token = default;
            return false;
        }

        while (index < closeIndex && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        var nameStart = index;
        var commaIndex = value.IndexOf(',', nameStart, closeIndex - nameStart);
        if (commaIndex < 0)
        {
            token = default;
            return false;
        }

        var nameEnd = commaIndex;
        while (nameEnd > nameStart && char.IsWhiteSpace(value[nameEnd - 1]))
        {
            nameEnd--;
        }

        if (nameEnd <= nameStart)
        {
            token = default;
            return false;
        }

        var payload = NormalizeAnchoredTokenPayload(value[(commaIndex + 1)..closeIndex]);
        if (payload.Length == 0)
        {
            token = default;
            return false;
        }

        token = new AnchoredToken(
            kind.ToLowerInvariant(),
            payload,
            value[nameStart..nameEnd],
            nameStart,
            nameEnd - nameStart,
            closeIndex);
        return true;
    }

    private static bool IsSupportedAnchoredTokenKind(string kind)
    {
        return kind.Equals("npc", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("item_t", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("mob", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("monster", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAnchoredTokenPayload(string payload)
    {
        var parts = payload.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        for (var index = 0; index < parts.Length; index++)
        {
            if (parts[index].Length == 0)
            {
                return string.Empty;
            }
        }

        return string.Join(',', parts);
    }

    private static IEnumerable<string> ExpandSourcePaths(IEnumerable<string> sourcePaths)
    {
        foreach (var sourcePath in sourcePaths)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(sourcePath);
            if (File.Exists(fullPath))
            {
                yield return fullPath;
                continue;
            }

            if (!Directory.Exists(fullPath))
            {
                continue;
            }

            foreach (var candidate in ExpandSourceDirectory(fullPath))
            {
                if (File.Exists(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static IEnumerable<string> ExpandSourceDirectory(string directory)
    {
        yield return Path.Combine(directory, "itemtype.txt");
        yield return Path.Combine(directory, "Server_Key.lua");
        yield return Path.Combine(directory, "Script", "Server_Key.lua");
        yield return Path.Combine(directory, "ini", "Server_Key.lua");
        yield return Path.Combine(directory, "GameMapEx.ini");
        yield return Path.Combine(directory, "ini", "GameMapEx.ini");
        yield return Path.Combine(directory, "GameMap.txt");
        yield return Path.Combine(directory, "ini", "GameMap.txt");
        yield return Path.Combine(directory, "GameMap.dat");
        yield return Path.Combine(directory, "ini", "GameMap.dat");
        yield return Path.Combine(directory, "NpcID.cs");
        yield return Path.Combine(directory, "DecompiledSource", "GameServer", "MrRayzo.Game.MsgNpc", "NpcID.cs");
    }

    private static void AddSourcePath(CatalogBuilder builder, string sourcePath)
    {
        var fileName = Path.GetFileName(sourcePath);
        try
        {
            if (fileName.Equals("itemtype.txt", StringComparison.OrdinalIgnoreCase))
            {
                AddItemTypeFile(builder, sourcePath);
                return;
            }

            if (fileName.Equals("Server_Key.lua", StringComparison.OrdinalIgnoreCase))
            {
                AddServerKeyFile(builder, sourcePath);
                return;
            }

            if (fileName.Equals("GameMapEx.ini", StringComparison.OrdinalIgnoreCase))
            {
                AddGameMapExFile(builder, sourcePath);
                return;
            }

            if (fileName.Equals("GameMap.txt", StringComparison.OrdinalIgnoreCase))
            {
                AddGameMapTextFile(builder, sourcePath);
                return;
            }

            if (fileName.Equals("GameMap.dat", StringComparison.OrdinalIgnoreCase))
            {
                AddGameMapDatFile(builder, sourcePath);
                return;
            }

            if (Path.GetExtension(sourcePath).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                builder.AddNpcNames(LegacyServerKeyResourceTranslator.ReadNpcIdEnumNames([sourcePath]));
            }
        }
        catch (DecoderFallbackException)
        {
        }
        catch (FormatException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void AddItemTypeFile(CatalogBuilder builder, string sourcePath)
    {
        foreach (var line in SplitLines(DecodeLenientCodePage936(File.ReadAllBytes(sourcePath))))
        {
            if (string.IsNullOrWhiteSpace(line.Text))
            {
                continue;
            }

            try
            {
                var itemType = ItemTypeParser.ParseLine(line.Text);
                builder.AddItemName(itemType.Id, itemType.Name);
            }
            catch (FormatException)
            {
            }
        }
    }

    private static void AddServerKeyFile(CatalogBuilder builder, string sourcePath)
    {
        foreach (var line in SplitLines(DecodeLenientCodePage936(File.ReadAllBytes(sourcePath))))
        {
            if (!TryParseServerKeyLine(line.Text, out var key, out var value) ||
                !TryParseTrailingId(key, out var id))
            {
                continue;
            }

            if (key.StartsWith("STR_ITEM_TYPE_", StringComparison.Ordinal))
            {
                builder.AddItemName(id, value);
            }
            else if (key.StartsWith("STR_NPC_ID_", StringComparison.Ordinal))
            {
                builder.AddNpcName(id, value);
            }
            else if (key.StartsWith("STR_MONSTER_ID_", StringComparison.Ordinal) ||
                key.StartsWith("STR_SHOOT_MONSTER_", StringComparison.Ordinal))
            {
                builder.AddMonsterName(id, value);
            }
        }
    }

    private static void AddGameMapExFile(CatalogBuilder builder, string sourcePath)
    {
        int? currentMapId = null;
        foreach (var line in SplitLines(DecodeLenientCodePage936(File.ReadAllBytes(sourcePath))))
        {
            var trimmed = line.Text.Trim();
            if (trimmed.Length == 0 ||
                trimmed.StartsWith(';') ||
                trimmed.StartsWith('#') ||
                trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (TryParseBracketedInt(trimmed, out var mapId))
            {
                currentMapId = mapId;
                continue;
            }

            if (currentMapId is { } id &&
                TryParseSimpleKeyValue(trimmed, out var key, out var value) &&
                key.Equals("Name", StringComparison.OrdinalIgnoreCase))
            {
                builder.AddMapName(id, value);
            }
        }
    }

    private static void AddGameMapTextFile(CatalogBuilder builder, string sourcePath)
    {
        foreach (var line in SplitLines(DecodeLenientCodePage936(File.ReadAllBytes(sourcePath))))
        {
            var fields = line.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (fields.Length < 2 || !TryParseInt(fields[0], out var id))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(fields[1].Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
            builder.AddMapAssetName(id, name);
        }
    }

    private static void AddGameMapDatFile(CatalogBuilder builder, string sourcePath)
    {
        foreach (var entry in LegacyGameMapParser.ParseGameMapDatFile(sourcePath))
        {
            var name = Path.GetFileNameWithoutExtension(entry.MapFile.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
            builder.AddMapAssetName(entry.MapId, name);
        }
    }

    private static bool TryParseServerKeyLine(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var keyPrefixIndex = line.IndexOf("[\"", StringComparison.Ordinal);
        if (keyPrefixIndex < 0)
        {
            return false;
        }

        var keyStart = keyPrefixIndex + 2;
        var keyEnd = line.IndexOf('"', keyStart);
        if (keyEnd <= keyStart)
        {
            return false;
        }

        var equalsIndex = line.IndexOf('=', keyEnd + 1);
        if (equalsIndex < 0)
        {
            return false;
        }

        var valueStart = line.IndexOf('"', equalsIndex + 1);
        if (valueStart < 0)
        {
            return false;
        }

        var valueEnd = FindLuaStringEnd(line, valueStart + 1);
        if (valueEnd <= valueStart)
        {
            return false;
        }

        key = line[keyStart..keyEnd];
        value = UnescapeLuaString(line[(valueStart + 1)..valueEnd]);
        return key.StartsWith("STR_", StringComparison.Ordinal);
    }

    private static int FindLuaStringEnd(string value, int startIndex)
    {
        var escaped = false;
        for (var index = startIndex; index < value.Length; index++)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (value[index] == '\\')
            {
                escaped = true;
                continue;
            }

            if (value[index] == '"')
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryParseSimpleKeyValue(string line, out string key, out string value)
    {
        var equalsIndex = line.IndexOf('=');
        if (equalsIndex <= 0)
        {
            key = string.Empty;
            value = string.Empty;
            return false;
        }

        key = line[..equalsIndex].Trim();
        value = line[(equalsIndex + 1)..].Trim();
        return key.Length != 0;
    }

    private static bool TryParseBracketedInt(string value, out int id)
    {
        var trimmed = value.Trim();
        if (trimmed.Length > 2 &&
            trimmed[0] == '[' &&
            trimmed[^1] == ']' &&
            TryParseInt(trimmed[1..^1], out id))
        {
            return true;
        }

        id = 0;
        return false;
    }

    private static bool TryParseTrailingId(string key, out int id)
    {
        var separatorIndex = key.LastIndexOf('_');
        if (separatorIndex >= 0 && separatorIndex < key.Length - 1)
        {
            return TryParseInt(key[(separatorIndex + 1)..], out id);
        }

        id = 0;
        return false;
    }

    private static bool TryParseInt(string value, out int id)
    {
        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
    }

    private static string NormalizeName(string value)
    {
        return value
            .Replace('~', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private static bool IsUsableName(string value)
    {
        var normalized = NormalizeName(value);
        return normalized.Length != 0 &&
            ContainsAsciiLetter(normalized) &&
            !ContainsCjkOrReplacement(normalized);
    }

    private static bool ContainsAsciiLetter(string value)
    {
        foreach (var character in value)
        {
            if (char.IsAsciiLetter(character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsCjkOrReplacement(string value)
    {
        return LegacyQuestVisibleTextNormalizer.ContainsCjkOrReplacement(value);
    }

    private static string UnescapeLuaString(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character != '\\' || index + 1 >= value.Length)
            {
                builder.Append(character);
                continue;
            }

            var escaped = value[++index];
            builder.Append(escaped switch
            {
                'n' => ' ',
                'r' => ' ',
                't' => ' ',
                _ => escaped,
            });
        }

        return builder.ToString();
    }

    private static string DecodeLenientCodePage936(byte[] bytes)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
                936,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ReplacementFallback)
            .GetString(bytes);
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

    private static readonly (string Source, string Target)[] RewardLabelTranslations =
    [
        ("\u7ecf\u9a8c", "EXP"),
        ("\u7269\u54c1", "Item"),
        ("\u94f6\u4e24", "Silver"),
    ];

    private readonly record struct AnchoredToken(
        string Kind,
        string Payload,
        string Name,
        int NameStart,
        int NameLength,
        int EndIndex);

    private readonly record struct BracketedItemAnchor(int ItemId, int OpenIndex, int CloseIndex);

    private sealed class CatalogBuilder
    {
        private readonly ConsistentNameBuilder _itemNames = new();
        private readonly ConsistentNameBuilder _npcNames = new();
        private readonly ConsistentNameBuilder _monsterNames = new();
        private readonly ConsistentNameBuilder _mapNames = new();
        private readonly ConsistentNameBuilder _mapAssetNames = new();

        public void AddItemName(int id, string name)
        {
            _itemNames.Add(id, name);
        }

        public void AddNpcName(int id, string name)
        {
            _npcNames.Add(id, name);
        }

        public void AddNpcNames(IReadOnlyDictionary<int, string> names)
        {
            foreach (var name in names)
            {
                AddNpcName(name.Key, name.Value);
            }
        }

        public void AddMonsterName(int id, string name)
        {
            _monsterNames.Add(id, name);
        }

        public void AddMapName(int id, string name)
        {
            _mapNames.Add(id, name);
        }

        public void AddMapAssetName(int id, string name)
        {
            _mapAssetNames.Add(id, name);
        }

        public LegacyQuestInfoNameFallbackCatalog Build()
        {
            var mapNames = new Dictionary<int, string>(_mapNames.Build());
            foreach (var mapAssetName in _mapAssetNames.Build())
            {
                mapNames.TryAdd(mapAssetName.Key, mapAssetName.Value);
            }

            return new LegacyQuestInfoNameFallbackCatalog(
                _itemNames.Build(),
                _npcNames.Build(),
                _monsterNames.Build(),
                mapNames);
        }
    }

    private sealed class ConsistentNameBuilder
    {
        private readonly Dictionary<int, string> _names = new();
        private readonly HashSet<int> _conflicts = [];

        public void Add(int id, string value)
        {
            if (id <= 0 || _conflicts.Contains(id))
            {
                return;
            }

            var normalized = NormalizeName(value);
            if (!IsUsableName(normalized))
            {
                return;
            }

            if (_names.TryGetValue(id, out var existing))
            {
                if (!StringComparer.Ordinal.Equals(existing, normalized))
                {
                    _names.Remove(id);
                    _conflicts.Add(id);
                }

                return;
            }

            _names.Add(id, normalized);
        }

        public IReadOnlyDictionary<int, string> Build()
        {
            return new Dictionary<int, string>(_names);
        }
    }
}

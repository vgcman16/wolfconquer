using System.Text;

namespace WolfConquer.Core.Client;

public sealed record LegacyQuestInfoPassionTranslationResult(
    int ReferenceSectionsScanned,
    int ReferenceMissionIds,
    int ReferenceFields,
    int AmbiguousReferenceFields,
    int TargetSectionsScanned,
    int TargetSectionsWithMissionId,
    int TranslatableFields,
    int MatchedFields,
    int ChangedFields,
    int AmbiguousFields,
    int UnmatchedFields,
    bool Changed);

public static class LegacyQuestInfoPassionTranslator
{
    private const int MinimumAnchoredReferenceMatches = 2;

    private static readonly HashSet<string> ExactTextKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "BeginNpcName",
        "Content",
        "FinishNpcName",
        "IntentionDesp",
        "Name",
        "Prize",
    };

    public static LegacyQuestInfoPassionTranslationResult Translate(
        string targetText,
        string referenceText,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(referenceText);

        return Translate(targetText, new[] { referenceText }, LegacyQuestInfoNameFallbackCatalog.Empty, out updatedText);
    }

    public static LegacyQuestInfoPassionTranslationResult Translate(
        string targetText,
        string referenceText,
        LegacyQuestInfoNameFallbackCatalog fallbackCatalog,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(referenceText);

        return Translate(targetText, new[] { referenceText }, fallbackCatalog, out updatedText);
    }

    public static LegacyQuestInfoPassionTranslationResult Translate(
        string targetText,
        IEnumerable<string> referenceTexts,
        out string updatedText)
    {
        return Translate(targetText, referenceTexts, LegacyQuestInfoNameFallbackCatalog.Empty, out updatedText);
    }

    public static LegacyQuestInfoPassionTranslationResult Translate(
        string targetText,
        IEnumerable<string> referenceTexts,
        LegacyQuestInfoNameFallbackCatalog fallbackCatalog,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(targetText);
        ArgumentNullException.ThrowIfNull(referenceTexts);
        ArgumentNullException.ThrowIfNull(fallbackCatalog);

        var referenceIndex = ReadReferenceIndex(referenceTexts);
        var lines = SplitLines(targetText).ToArray();
        var targetSections = ParseSections(lines);
        var targetSectionsWithMissionId = 0;
        var translatableFields = 0;
        var matchedFields = 0;
        var changedFields = 0;
        var ambiguousFields = 0;
        var unmatchedFields = 0;
        var changed = false;

        foreach (var section in targetSections)
        {
            if (section.Name.Equals("TaskType", StringComparison.OrdinalIgnoreCase))
            {
                TranslateTaskTypeSection(lines, section, referenceIndex, ref translatableFields, ref matchedFields, ref changedFields, ref ambiguousFields, ref unmatchedFields, ref changed);
                continue;
            }

            var missionId = TryReadSectionMissionId(lines, section);
            if (missionId is null)
            {
                continue;
            }

            targetSectionsWithMissionId++;
            var sectionAnchors = ReadUsableTextAnchors(lines, section, missionId, referenceIndex);
            for (var index = section.StartIndex + 1; index < section.EndIndex; index++)
            {
                if (!TryParseKeyValue(lines[index].Text, out var prefix, out var key, out var separator, out var value) ||
                    !NeedsTranslation(value))
                {
                    continue;
                }

                var isQuestTextField = IsQuestTextField(key);
                var isCompoundNpcField = IsCompoundNpcField(key);
                if (!isQuestTextField && !isCompoundNpcField)
                {
                    continue;
                }

                translatableFields++;
                var matchedReference = referenceIndex.TryGetValue(missionId, key, out var referenceValue, out var ambiguous);
                if (!matchedReference && ambiguous)
                {
                    var aggregateAmbiguous = ambiguous;
                    matchedReference = referenceIndex.TryGetAnchoredValue(missionId, key, sectionAnchors, out referenceValue, out ambiguous);
                    ambiguous = aggregateAmbiguous || ambiguous;
                }

                if (!matchedReference)
                {
                    if (isCompoundNpcField &&
                        ((!ambiguous && referenceIndex.TryReplaceCompoundNpcTuple(value, out var compoundReplacementValue)) ||
                            fallbackCatalog.TryReplaceCompoundNpcValue(value, out compoundReplacementValue)))
                    {
                        matchedFields++;
                        var compoundTranslatedLine = prefix + key + separator + compoundReplacementValue;
                        if (!StringComparer.Ordinal.Equals(lines[index].Text, compoundTranslatedLine))
                        {
                            lines[index] = lines[index] with
                            {
                                Text = compoundTranslatedLine,
                            };
                            changedFields++;
                            changed = true;
                        }
                    }
                    else if (isQuestTextField &&
                        referenceIndex.TryReplaceAnchoredTokens(missionId, value, out var tokenReplacementValue))
                    {
                        matchedFields++;
                        var tokenTranslatedLine = prefix + key + separator + tokenReplacementValue;
                        if (!StringComparer.Ordinal.Equals(lines[index].Text, tokenTranslatedLine))
                        {
                            lines[index] = lines[index] with
                            {
                                Text = tokenTranslatedLine,
                            };
                            changedFields++;
                            changed = true;
                        }
                    }
                    else if (isQuestTextField &&
                        fallbackCatalog.TryReplaceVisibleValue(key, value, out var fallbackReplacementValue))
                    {
                        matchedFields++;
                        var fallbackTranslatedLine = prefix + key + separator + fallbackReplacementValue;
                        if (!StringComparer.Ordinal.Equals(lines[index].Text, fallbackTranslatedLine))
                        {
                            lines[index] = lines[index] with
                            {
                                Text = fallbackTranslatedLine,
                            };
                            changedFields++;
                            changed = true;
                        }
                    }
                    else if (!ambiguous &&
                        isQuestTextField &&
                        LegacyQuestVisibleTextNormalizer.TryNormalizeAlreadyEnglishValue(value, out var normalizedValue))
                    {
                        matchedFields++;
                        var normalizedLine = prefix + key + separator + normalizedValue;
                        if (!StringComparer.Ordinal.Equals(lines[index].Text, normalizedLine))
                        {
                            lines[index] = lines[index] with
                            {
                                Text = normalizedLine,
                            };
                            changedFields++;
                            changed = true;
                        }
                    }
                    else if (ambiguous)
                    {
                        ambiguousFields++;
                    }
                    else
                    {
                        unmatchedFields++;
                    }

                    continue;
                }

                if (isCompoundNpcField &&
                    !TryBuildCompoundNpcReplacement(value, referenceValue, out referenceValue))
                {
                    if ((ambiguous || !referenceIndex.TryReplaceCompoundNpcTuple(value, out referenceValue)) &&
                        !fallbackCatalog.TryReplaceCompoundNpcValue(value, out referenceValue))
                    {
                        unmatchedFields++;
                        continue;
                    }
                }

                matchedFields++;
                var translatedLine = prefix + key + separator + referenceValue;
                if (!StringComparer.Ordinal.Equals(lines[index].Text, translatedLine))
                {
                    lines[index] = lines[index] with
                    {
                        Text = translatedLine,
                    };
                    changedFields++;
                    changed = true;
                }
            }
        }

        var builder = new StringBuilder(targetText.Length);
        foreach (var line in lines)
        {
            builder.Append(line.Text);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        return new LegacyQuestInfoPassionTranslationResult(
            referenceIndex.SectionsScanned,
            referenceIndex.MissionIds,
            referenceIndex.Fields,
            referenceIndex.AmbiguousFields,
            targetSections.Count,
            targetSectionsWithMissionId,
            translatableFields,
            matchedFields,
            changedFields,
            ambiguousFields,
            unmatchedFields,
            changed);
    }

    private static ReferenceIndex ReadReferenceIndex(IEnumerable<string> referenceTexts)
    {
        var index = new ReferenceIndex();
        foreach (var referenceText in referenceTexts)
        {
            ArgumentNullException.ThrowIfNull(referenceText);

            var lines = SplitLines(referenceText).ToArray();
            foreach (var section in ParseSections(lines))
            {
                index.SectionsScanned++;
                var missionId = TryReadSectionMissionId(lines, section);
                if (missionId is null)
                {
                    if (section.Name.Equals("TaskType", StringComparison.OrdinalIgnoreCase))
                    {
                        index.AddTaskTypeSection(lines, section);
                    }

                    continue;
                }

                var referenceSection = new ReferenceSection();
                for (var lineIndex = section.StartIndex + 1; lineIndex < section.EndIndex; lineIndex++)
                {
                    if (TryParseKeyValue(lines[lineIndex].Text, out _, out var key, out _, out var value) &&
                        (IsQuestTextField(key) || IsCompoundNpcField(key)) &&
                        IsUsableReferenceValue(value, out var normalizedValue))
                    {
                        index.Add(missionId, key, normalizedValue);
                        referenceSection.Add(key, normalizedValue);
                    }
                }

                index.AddSection(missionId, referenceSection);
            }
        }

        index.RefreshCounts();
        return index;
    }

    private static List<QuestSection> ParseSections(IReadOnlyList<ClientTextLine> lines)
    {
        var sections = new List<QuestSection>();
        int? sectionStartIndex = null;
        var sectionName = string.Empty;
        for (var index = 0; index < lines.Count; index++)
        {
            if (!TryParseSection(lines[index].Text, out var nextSectionName))
            {
                continue;
            }

            if (sectionStartIndex is { } startIndex)
            {
                sections.Add(new QuestSection(sectionName, startIndex, index));
            }

            sectionStartIndex = index;
            sectionName = nextSectionName;
        }

        if (sectionStartIndex is { } finalStartIndex)
        {
            sections.Add(new QuestSection(sectionName, finalStartIndex, lines.Count));
        }

        return sections;
    }

    private static string? TryReadSectionMissionId(IReadOnlyList<ClientTextLine> lines, QuestSection section)
    {
        string? missionId = null;
        for (var index = section.StartIndex + 1; index < section.EndIndex; index++)
        {
            if (!TryParseKeyValue(lines[index].Text, out _, out var key, out _, out var value) ||
                !key.Equals("MissionId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidate = value.Trim();
            if (candidate.Length == 0)
            {
                continue;
            }

            if (missionId is not null && !StringComparer.Ordinal.Equals(missionId, candidate))
            {
                return null;
            }

            missionId = candidate;
        }

        return missionId;
    }

    private static bool IsQuestTextField(string key)
    {
        var normalizedKey = key.Trim();
        if (ExactTextKeys.Contains(normalizedKey))
        {
            return true;
        }

        return IsNumberedTextKey(normalizedKey, "Intention");
    }

    private static bool IsCompoundNpcField(string key)
    {
        var normalizedKey = key.Trim();
        return normalizedKey.Equals("BeginNpcId", StringComparison.OrdinalIgnoreCase) ||
            normalizedKey.Equals("FinishNpcId", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNumberedTextKey(string normalizedKey, string prefix)
    {
        if (!normalizedKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            normalizedKey.Length == prefix.Length)
        {
            return false;
        }

        for (var index = prefix.Length; index < normalizedKey.Length; index++)
        {
            if (!char.IsAsciiDigit(normalizedKey[index]))
            {
                return false;
            }
        }

        return true;
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

    private static bool TryBuildCompoundNpcTupleReplacement(
        string targetValue,
        string referenceTail,
        out string replacementValue)
    {
        replacementValue = string.Empty;
        if (!TryReadCompoundNpcTuple(targetValue, out _, out var targetTailParts))
        {
            return false;
        }

        var targetParts = targetValue.Split(',');
        var referenceTailParts = referenceTail.Split(',', StringSplitOptions.TrimEntries);
        const int numericPartCount = 4;
        if (referenceTailParts.Length != targetTailParts.Length ||
            targetParts.Length != numericPartCount + targetTailParts.Length)
        {
            return false;
        }

        var changed = false;
        var replacementParts = new string[targetParts.Length];
        for (var index = 0; index < numericPartCount; index++)
        {
            replacementParts[index] = targetParts[index];
        }

        for (var index = 0; index < targetTailParts.Length; index++)
        {
            if (ContainsCjkOrReplacement(targetTailParts[index]) &&
                referenceTailParts[index].Length != 0 &&
                ContainsAsciiLetter(referenceTailParts[index]) &&
                !ContainsCjkOrReplacement(referenceTailParts[index]))
            {
                replacementParts[numericPartCount + index] = referenceTailParts[index];
                changed = true;
                continue;
            }

            replacementParts[numericPartCount + index] = targetParts[numericPartCount + index];
        }

        if (!changed)
        {
            return false;
        }

        replacementValue = string.Join(',', replacementParts);
        return !StringComparer.Ordinal.Equals(targetValue, replacementValue);
    }

    private static bool TryReadCompoundNpcTuple(
        string value,
        out CompoundNpcTupleKey key,
        out string[] tailParts)
    {
        key = default;
        tailParts = [];
        var parts = value.Split(',');
        const int numericPartCount = 4;
        if (parts.Length < numericPartCount + 1 ||
            !long.TryParse(parts[0].Trim(), out var npcId) ||
            !long.TryParse(parts[1].Trim(), out var mapId) ||
            !long.TryParse(parts[2].Trim(), out var x) ||
            !long.TryParse(parts[3].Trim(), out var y))
        {
            return false;
        }

        tailParts = new string[parts.Length - numericPartCount];
        for (var index = 0; index < tailParts.Length; index++)
        {
            tailParts[index] = parts[numericPartCount + index].Trim();
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

    private static bool NeedsTranslation(string value)
    {
        return ContainsCjkOrReplacement(value);
    }

    private static bool IsUsableReferenceValue(string value, out string normalizedValue)
    {
        normalizedValue = NormalizeReferenceValue(value);
        return normalizedValue.Length != 0 &&
            ContainsAsciiLetter(normalizedValue) &&
            !ContainsCjkOrReplacement(normalizedValue);
    }

    private static string NormalizeReferenceValue(string value)
    {
        return LegacyQuestVisibleTextNormalizer.NormalizeReferenceValue(value);
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

    private static bool TryParseSection(string line, out string sectionName)
    {
        var trimmed = line.Trim();
        if (trimmed.Length > 2 && trimmed[0] == '[' && trimmed[^1] == ']')
        {
            sectionName = trimmed[1..^1];
            return true;
        }

        sectionName = string.Empty;
        return false;
    }

    private static void TranslateTaskTypeSection(
        ClientTextLine[] lines,
        QuestSection section,
        ReferenceIndex referenceIndex,
        ref int translatableFields,
        ref int matchedFields,
        ref int changedFields,
        ref int ambiguousFields,
        ref int unmatchedFields,
        ref bool changed)
    {
        var typeIds = ReadIndexedValues(lines, section, "TypeId");
        for (var index = section.StartIndex + 1; index < section.EndIndex; index++)
        {
            if (!TryParseKeyValue(lines[index].Text, out var prefix, out var key, out var separator, out var value) ||
                !TryParseNumberedKey(key, "TypeName", out var typeIndex) ||
                !NeedsTranslation(value))
            {
                continue;
            }

            translatableFields++;
            if (!typeIds.TryGetValue(typeIndex, out var typeId))
            {
                unmatchedFields++;
                continue;
            }

            if (!referenceIndex.TryGetTaskTypeName(typeId, out var referenceValue, out var ambiguous))
            {
                if (ambiguous)
                {
                    ambiguousFields++;
                }
                else
                {
                    unmatchedFields++;
                }

                continue;
            }

            matchedFields++;
            var translatedLine = prefix + key + separator + referenceValue;
            if (!StringComparer.Ordinal.Equals(lines[index].Text, translatedLine))
            {
                lines[index] = lines[index] with
                {
                    Text = translatedLine,
                };
                changedFields++;
                changed = true;
            }
        }
    }

    private static Dictionary<int, string> ReadIndexedValues(IReadOnlyList<ClientTextLine> lines, QuestSection section, string keyPrefix)
    {
        var values = new Dictionary<int, string>();
        for (var index = section.StartIndex + 1; index < section.EndIndex; index++)
        {
            if (TryParseKeyValue(lines[index].Text, out _, out var key, out _, out var value) &&
                TryParseNumberedKey(key, keyPrefix, out var itemIndex))
            {
                values[itemIndex] = value.Trim();
            }
        }

        return values;
    }

    private static IReadOnlyDictionary<string, string> ReadUsableTextAnchors(
        IReadOnlyList<ClientTextLine> lines,
        QuestSection section,
        string missionId,
        ReferenceIndex referenceIndex)
    {
        var anchors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = section.StartIndex + 1; index < section.EndIndex; index++)
        {
            if (!TryParseKeyValue(lines[index].Text, out _, out var key, out _, out var value) ||
                !IsQuestTextField(key))
            {
                continue;
            }

            if (IsUsableReferenceValue(value, out var normalizedValue))
            {
                anchors[key] = normalizedValue;
                continue;
            }

            if (NeedsTranslation(value) &&
                referenceIndex.TryGetValue(missionId, key, out var referenceValue, out _))
            {
                anchors[key] = referenceValue;
            }
        }

        return anchors;
    }

    private static bool TryParseNumberedKey(string key, string prefix, out int index)
    {
        index = 0;
        var normalizedKey = key.Trim();
        if (!IsNumberedTextKey(normalizedKey, prefix))
        {
            return false;
        }

        return int.TryParse(normalizedKey[prefix.Length..], out index);
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

    private readonly record struct QuestSection(string Name, int StartIndex, int EndIndex);

    private sealed class ReferenceIndex
    {
        private readonly Dictionary<string, ReferenceMission> _missions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ReferenceSection>> _missionSections = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ReferenceField> _taskTypeNames = new(StringComparer.Ordinal);
        private readonly CompoundNpcTupleCatalog _compoundNpcTuples = new();

        public int SectionsScanned { get; set; }

        public int MissionIds { get; private set; }

        public int Fields { get; private set; }

        public int AmbiguousFields { get; private set; }

        public void Add(string missionId, string key, string value)
        {
            if (!_missions.TryGetValue(missionId, out var mission))
            {
                mission = new ReferenceMission();
                _missions.Add(missionId, mission);
            }

            mission.Add(key, value);
            if (IsCompoundNpcField(key))
            {
                _compoundNpcTuples.Add(value);
            }
        }

        public void AddSection(string missionId, ReferenceSection section)
        {
            if (section.FieldCount == 0)
            {
                return;
            }

            if (!_missionSections.TryGetValue(missionId, out var sections))
            {
                sections = [];
                _missionSections.Add(missionId, sections);
            }

            sections.Add(section);
        }

        public void AddTaskTypeSection(IReadOnlyList<ClientTextLine> lines, QuestSection section)
        {
            var typeIds = ReadIndexedValues(lines, section, "TypeId");
            for (var index = section.StartIndex + 1; index < section.EndIndex; index++)
            {
                if (!TryParseKeyValue(lines[index].Text, out _, out var key, out _, out var value) ||
                    !TryParseNumberedKey(key, "TypeName", out var typeIndex) ||
                    !typeIds.TryGetValue(typeIndex, out var typeId) ||
                    !IsUsableReferenceValue(value, out var normalizedValue))
                {
                    continue;
                }

                if (!_taskTypeNames.TryGetValue(typeId, out var field))
                {
                    _taskTypeNames.Add(typeId, new ReferenceField(normalizedValue));
                    continue;
                }

                field.Add(normalizedValue);
            }
        }

        public bool TryGetValue(string missionId, string key, out string value, out bool ambiguous)
        {
            if (_missions.TryGetValue(missionId, out var mission))
            {
                return mission.TryGetValue(key, out value, out ambiguous);
            }

            value = string.Empty;
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

        public bool TryGetAnchoredValue(
            string missionId,
            string key,
            IReadOnlyDictionary<string, string> anchors,
            out string value,
            out bool ambiguous)
        {
            value = string.Empty;
            ambiguous = false;
            if (anchors.Count < MinimumAnchoredReferenceMatches ||
                !_missionSections.TryGetValue(missionId, out var sections))
            {
                return false;
            }

            ReferenceField? field = null;
            foreach (var section in sections)
            {
                if (!section.TryGetValue(key, out var candidateValue) ||
                    !section.HasMatchingAnchors(anchors, key, MinimumAnchoredReferenceMatches))
                {
                    continue;
                }

                if (field is null)
                {
                    field = new ReferenceField(candidateValue);
                    continue;
                }

                field.Add(candidateValue);
            }

            if (field is null)
            {
                return false;
            }

            value = field.Value;
            ambiguous = field.Ambiguous;
            return !ambiguous;
        }

        public bool TryReplaceCompoundNpcTuple(string targetValue, out string replacementValue)
        {
            return _compoundNpcTuples.TryReplaceValue(targetValue, out replacementValue);
        }

        public bool TryGetTaskTypeName(string typeId, out string value, out bool ambiguous)
        {
            if (_taskTypeNames.TryGetValue(typeId, out var field))
            {
                value = field.Value;
                ambiguous = field.Ambiguous;
                return !ambiguous;
            }

            value = string.Empty;
            ambiguous = false;
            return false;
        }

        public void RefreshCounts()
        {
            MissionIds = _missions.Count;
            Fields = 0;
            AmbiguousFields = 0;
            foreach (var mission in _missions.Values)
            {
                Fields += mission.Fields;
                AmbiguousFields += mission.AmbiguousFields;
            }

            Fields += _taskTypeNames.Count;
            foreach (var field in _taskTypeNames.Values)
            {
                if (field.Ambiguous)
                {
                    AmbiguousFields++;
                }
            }
        }
    }

    private sealed class ReferenceSection
    {
        private readonly Dictionary<string, string> _fields = new(StringComparer.OrdinalIgnoreCase);

        public int FieldCount => _fields.Count;

        public void Add(string key, string value)
        {
            _fields[key] = value;
        }

        public bool TryGetValue(string key, out string value)
        {
            return _fields.TryGetValue(key, out value!);
        }

        public bool HasMatchingAnchors(
            IReadOnlyDictionary<string, string> anchors,
            string translatedKey,
            int minimumMatches)
        {
            var matches = 0;
            foreach (var anchor in anchors)
            {
                if (anchor.Key.Equals(translatedKey, StringComparison.OrdinalIgnoreCase) ||
                    !_fields.TryGetValue(anchor.Key, out var referenceValue))
                {
                    continue;
                }

                if (!StringComparer.Ordinal.Equals(referenceValue, anchor.Value))
                {
                    return false;
                }

                matches++;
            }

            return matches >= minimumMatches;
        }
    }

    private sealed class ReferenceMission
    {
        private readonly Dictionary<string, ReferenceField> _fields = new(StringComparer.OrdinalIgnoreCase);
        private readonly ReferenceTokenCatalog _tokens = new();

        public int Fields => _fields.Count;

        public int AmbiguousFields
        {
            get
            {
                var count = 0;
                foreach (var field in _fields.Values)
                {
                    if (field.Ambiguous)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void Add(string key, string value)
        {
            if (!_fields.TryGetValue(key, out var field))
            {
                _fields.Add(key, new ReferenceField(value));
                if (IsQuestTextField(key))
                {
                    _tokens.AddTokens(value);
                }

                return;
            }

            field.Add(value);
            if (IsQuestTextField(key))
            {
                _tokens.AddTokens(value);
            }
        }

        public bool TryGetValue(string key, out string value, out bool ambiguous)
        {
            if (_fields.TryGetValue(key, out var field))
            {
                value = field.Value;
                ambiguous = field.Ambiguous;
                return !ambiguous;
            }

            value = string.Empty;
            ambiguous = false;
            return false;
        }

        public bool TryReplaceAnchoredTokens(string targetValue, out string replacementValue)
        {
            return LegacyQuestInfoPassionTranslator.TryReplaceAnchoredTokens(targetValue, _tokens, out replacementValue);
        }
    }

    private sealed class ReferenceField(string value)
    {
        public string Value { get; private set; } = value;

        public bool Ambiguous { get; private set; }

        public void Add(string value)
        {
            if (!StringComparer.Ordinal.Equals(Value, value))
            {
                Ambiguous = true;
            }
        }
    }

    private sealed class ReferenceTokenCatalog
    {
        private readonly Dictionary<ReferenceTokenKey, ReferenceField> _values = new();

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
                    _values.Add(key, new ReferenceField(token.Name));
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
        private readonly Dictionary<CompoundNpcTupleKey, ReferenceField> _values = new();

        public void Add(string value)
        {
            if (!TryReadCompoundNpcTuple(value, out var key, out var tailParts))
            {
                return;
            }

            var tail = string.Join(',', tailParts);
            if (tail.Length == 0 ||
                !ContainsAsciiLetter(tail) ||
                ContainsCjkOrReplacement(tail))
            {
                return;
            }

            if (!_values.TryGetValue(key, out var field))
            {
                _values.Add(key, new ReferenceField(tail));
                return;
            }

            field.Add(tail);
        }

        public bool TryReplaceValue(string targetValue, out string replacementValue)
        {
            replacementValue = string.Empty;
            if (!TryReadCompoundNpcTuple(targetValue, out var key, out _) ||
                !_values.TryGetValue(key, out var field) ||
                field.Ambiguous)
            {
                return false;
            }

            return TryBuildCompoundNpcTupleReplacement(targetValue, field.Value, out replacementValue);
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
}

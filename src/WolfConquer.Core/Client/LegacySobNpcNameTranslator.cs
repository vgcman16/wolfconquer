using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.Client;

public sealed record LegacySobNpcNameTranslationResult(
    int ReferenceRowsScanned,
    int ReferenceExactRows,
    int ReferenceUniqueIds,
    int TargetRowsScanned,
    int TranslatableNames,
    int ExactRowMatches,
    int UniqueIdMatches,
    int KnownNameMatches,
    int TranslatedNames,
    int ChangedNames,
    int UnmatchedNames,
    bool Changed);

public static class LegacySobNpcNameTranslator
{
    public static LegacySobNpcNameTranslationResult Translate(
        string targetText,
        string referenceText,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(targetText);
        ArgumentNullException.ThrowIfNull(referenceText);

        var referenceNames = ReadReferenceNames(referenceText);
        var targetLines = SplitLines(targetText).ToArray();
        var targetRowsScanned = 0;
        var translatableNames = 0;
        var exactRowMatches = 0;
        var uniqueIdMatches = 0;
        var knownNameMatches = 0;
        var changedNames = 0;
        var unmatchedNames = 0;
        var changed = false;

        for (var index = 0; index < targetLines.Length; index++)
        {
            if (!TryParseSobNpcRow(targetLines[index].Text, out var targetRow))
            {
                continue;
            }

            targetRowsScanned++;
            if (!NeedsTranslation(targetRow.Name))
            {
                continue;
            }

            translatableNames++;
            if (referenceNames.ExactByIdentity.TryGetValue(targetRow.Identity, out var translatedName))
            {
                exactRowMatches++;
            }
            else if (referenceNames.UniqueById.TryGetValue(targetRow.Id, out translatedName))
            {
                uniqueIdMatches++;
            }
            else if (LegacySobNpcNameCatalog.TryTranslateKnownName(targetRow.Name, out translatedName))
            {
                knownNameMatches++;
            }
            else
            {
                unmatchedNames++;
                continue;
            }

            if (StringComparer.Ordinal.Equals(targetRow.Name, translatedName))
            {
                continue;
            }

            targetRow.Fields[1] = translatedName;
            targetLines[index] = targetLines[index] with
            {
                Text = string.Join(',', targetRow.Fields),
            };
            changedNames++;
            changed = true;
        }

        var builder = new StringBuilder();
        foreach (var line in targetLines)
        {
            builder.Append(line.Text);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        return new LegacySobNpcNameTranslationResult(
            referenceNames.RowsScanned,
            referenceNames.ExactByIdentity.Count,
            referenceNames.UniqueById.Count,
            targetRowsScanned,
            translatableNames,
            exactRowMatches,
            uniqueIdMatches,
            knownNameMatches,
            exactRowMatches + uniqueIdMatches + knownNameMatches,
            changedNames,
            unmatchedNames,
            changed);
    }

    public static LegacySobNpcNameTranslationResult PatchFile(string targetPath, string referencePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(referencePath);

        var targetBytes = File.ReadAllBytes(targetPath);
        var targetEncoding = DetectUtf8Encoding(targetBytes);
        var targetText = targetEncoding.GetString(targetBytes);
        var referenceText = ReadUtf8Text(referencePath);
        var result = Translate(targetText, referenceText, out var updatedText);
        if (result.Changed)
        {
            File.WriteAllText(targetPath, updatedText, targetEncoding);
        }

        return result;
    }

    private static ReferenceSobNpcNameCatalog ReadReferenceNames(string text)
    {
        var exactByIdentity = new Dictionary<SobNpcIdentity, string>();
        var uniqueById = new Dictionary<int, string>();
        var conflictsById = new HashSet<int>();
        var rowsScanned = 0;

        foreach (var line in SplitLines(text))
        {
            if (!TryParseSobNpcRow(line.Text, out var row))
            {
                continue;
            }

            rowsScanned++;
            if (!IsLikelyEnglishName(row.Name))
            {
                continue;
            }

            exactByIdentity.TryAdd(row.Identity, row.Name);
            AddConsistentCandidate(uniqueById, conflictsById, row.Id, row.Name);
        }

        foreach (var conflict in conflictsById)
        {
            uniqueById.Remove(conflict);
        }

        return new ReferenceSobNpcNameCatalog(exactByIdentity, uniqueById, rowsScanned);
    }

    private static void AddConsistentCandidate(
        Dictionary<int, string> candidates,
        HashSet<int> conflicts,
        int id,
        string name)
    {
        if (conflicts.Contains(id))
        {
            return;
        }

        if (candidates.TryGetValue(id, out var existing))
        {
            if (!StringComparer.Ordinal.Equals(existing, name))
            {
                candidates.Remove(id);
                conflicts.Add(id);
            }

            return;
        }

        candidates[id] = name;
    }

    private static bool TryParseSobNpcRow(string line, out SobNpcNameRow row)
    {
        var fields = line.Split(',');
        if (fields.Length < 7 ||
            !TryParseLegacyInt(fields[0], out var id) ||
            !TryParseLegacyInt(fields[2], out var kind) ||
            !TryParseLegacyInt(fields[3], out var lookface) ||
            !TryParseLegacyInt(fields[4], out var mapId) ||
            !TryParseLegacyInt(fields[5], out var x) ||
            !TryParseLegacyInt(fields[6], out var y))
        {
            row = default;
            return false;
        }

        row = new SobNpcNameRow(
            fields,
            id,
            fields[1],
            new SobNpcIdentity(id, kind, lookface, mapId, x, y));
        return true;
    }

    private static bool TryParseLegacyInt(string value, out int result)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            result = 0;
            return false;
        }

        normalized = normalized.TrimStart('0');
        if (normalized.Length == 0)
        {
            normalized = "0";
        }

        return int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out result);
    }

    private static bool NeedsTranslation(string value)
    {
        if (value.IndexOf('\uFFFD') >= 0)
        {
            return true;
        }

        return LegacySobNpcNameCatalog.ContainsCjk(value);
    }

    private static bool IsLikelyEnglishName(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is '\uFFFD' or >= '\u3400' and <= '\u9fff' or >= '\u0600' and <= '\u06ff')
            {
                return false;
            }
        }

        return true;
    }

    private static string ReadUtf8Text(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return DetectUtf8Encoding(bytes).GetString(bytes);
    }

    private static Encoding DetectUtf8Encoding(byte[] bytes)
    {
        var emitBom = bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF;
        var encoding = new UTF8Encoding(emitBom, throwOnInvalidBytes: true);
        encoding.GetString(bytes);
        return encoding;
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

    private readonly record struct SobNpcIdentity(int Id, int Kind, int Lookface, int MapId, int X, int Y);

    private readonly record struct SobNpcNameRow(
        string[] Fields,
        int Id,
        string Name,
        SobNpcIdentity Identity);

    private sealed record ReferenceSobNpcNameCatalog(
        IReadOnlyDictionary<SobNpcIdentity, string> ExactByIdentity,
        IReadOnlyDictionary<int, string> UniqueById,
        int RowsScanned);
}

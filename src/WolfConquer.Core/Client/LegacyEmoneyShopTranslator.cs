using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WolfConquer.Core.Client;

public sealed record LegacyEmoneyShopTranslationResult(
    int RowCount,
    int TranslatedRows,
    int UnmatchedRows,
    int UniqueItemIds,
    int MatchedUniqueItemIds,
    IReadOnlyList<int> UnmatchedItemIds,
    int ChangedRows,
    bool Changed);

public static class LegacyEmoneyShopTranslator
{
    private static readonly Regex ShopItemRowPattern = new(
        @"^(?<prefix>\s*(?:(?:[Ii]tem\d+)\s*=\s*)?(?<id>\d+)\s+\S+\s+)(?<description>.*)$",
        RegexOptions.CultureInvariant);

    public static LegacyEmoneyShopTranslationResult PatchFile(string shopPath, string itemTypePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemTypePath);

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var original = File.ReadAllBytes(shopPath);
        var shopText = encoding.GetString(original);
        var result = Translate(shopText, File.ReadLines(itemTypePath), out var updatedText);
        if (result.Changed)
        {
            File.WriteAllBytes(shopPath, encoding.GetBytes(updatedText));
        }

        return result;
    }

    public static LegacyEmoneyShopTranslationResult Translate(
        string shopText,
        IEnumerable<string> itemTypeLines,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(shopText);
        ArgumentNullException.ThrowIfNull(itemTypeLines);

        var itemDescriptions = BuildItemDescriptions(itemTypeLines);
        var uniqueIds = new HashSet<int>();
        var matchedUniqueIds = new HashSet<int>();
        var unmatchedIds = new HashSet<int>();
        var builder = new StringBuilder(shopText.Length);
        var rowCount = 0;
        var translatedRows = 0;
        var unmatchedRows = 0;
        var changedRows = 0;

        foreach (var line in SplitLines(shopText))
        {
            if (!TryParseShopItemRow(line.Text, out var itemId, out var prefix))
            {
                builder.Append(line.Text);
                builder.Append(line.LineEnding);
                continue;
            }

            rowCount++;
            uniqueIds.Add(itemId);
            if (itemDescriptions.TryGetValue(itemId, out var description))
            {
                translatedRows++;
                matchedUniqueIds.Add(itemId);
                var translatedLine = prefix + description;
                if (!StringComparer.Ordinal.Equals(line.Text, translatedLine))
                {
                    changedRows++;
                }

                builder.Append(translatedLine);
                builder.Append(line.LineEnding);
                continue;
            }

            unmatchedRows++;
            unmatchedIds.Add(itemId);
            builder.Append(line.Text);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        var missingItemIds = unmatchedIds.Order().ToArray();
        return new LegacyEmoneyShopTranslationResult(
            rowCount,
            translatedRows,
            unmatchedRows,
            uniqueIds.Count,
            matchedUniqueIds.Count,
            missingItemIds,
            changedRows,
            !StringComparer.Ordinal.Equals(shopText, updatedText));
    }

    private static Dictionary<int, string> BuildItemDescriptions(IEnumerable<string> itemTypeLines)
    {
        var itemDescriptions = new Dictionary<int, string>();
        foreach (var line in itemTypeLines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = line.Split("@@");
            if (fields.Length <= 53 ||
                !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var itemId))
            {
                continue;
            }

            var description = NormalizeDescription(fields[53]);
            if (description.Length == 0)
            {
                description = NormalizeDescription(fields[1]);
            }

            if (description.Length == 0)
            {
                continue;
            }

            if (!itemDescriptions.TryGetValue(itemId, out var existing) || existing.Length == 0)
            {
                itemDescriptions[itemId] = description;
            }
        }

        return itemDescriptions;
    }

    private static string NormalizeDescription(string value)
    {
        return value
            .Replace('~', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private static bool TryParseShopItemRow(string line, out int itemId, out string prefix)
    {
        itemId = 0;
        prefix = string.Empty;

        var match = ShopItemRowPattern.Match(line);
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out itemId))
        {
            return false;
        }

        prefix = match.Groups["prefix"].Value;
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
}

using System.Globalization;

namespace WolfConquer.Core.Data;

public static class ShopParser
{
    private const string ShopDatDialect = "ShopDat";
    private const string EmoneyShopDialect = "EmoneyShop";
    private const string EmoneyShopV2Dialect = "EmoneyShopV2";
    private static readonly char[] CsvSeparator = [','];

    public static ShopData ParseShopDat(string content, string sourceFile = "Shop.dat")
    {
        var document = IniDocument.Parse(content);
        var shops = new List<ShopDefinition>();
        var items = new List<ShopItem>();

        foreach (var section in document.Sections
            .Where(pair => TryGetShopSectionIndex(pair.Key, out _))
            .OrderBy(pair => GetShopSectionIndex(pair.Key)))
        {
            var values = section.Value;
            var shopId = ParseRequiredInt(values, "ID", section.Key);
            shops.Add(new ShopDefinition(
                shopId,
                GetOptional(values, "Name"),
                TryParseOptionalInt(values, "Type"),
                TryParseOptionalInt(values, "MoneyType"),
                sourceFile,
                ShopDatDialect,
                CopyAttributes(values)));

            foreach (var item in EnumerateItemValues(values))
            {
                items.Add(new ShopItem(
                    shopId,
                    ParseInt(item.Value, $"{section.Key}.{item.Key}"),
                    item.Position,
                    Category: null,
                    Status: null,
                    IsBound: false,
                    Price: null,
                    Description: null,
                    Flags: [],
                    sourceFile,
                    ShopDatDialect,
                    $"{item.Key}={item.Value}",
                    new Dictionary<string, string> { ["key"] = item.Key }));
            }
        }

        return new ShopData(shops, items);
    }

    public static ShopData ParseLegacyEmoneyShop(string content, string sourceFile = "emoneyshop.ini")
    {
        var shops = new List<ShopDefinition>();
        var items = new List<ShopItem>();
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int? shopId = null;
        int? moneyType = null;
        string? category = null;

        void FinishShop()
        {
            if (shopId is null)
            {
                return;
            }

            shops.Add(new ShopDefinition(
                shopId.Value,
                Name: null,
                Type: null,
                moneyType,
                sourceFile,
                EmoneyShopDialect,
                new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase)));
        }

        foreach (var rawLine in ReadMeaningfulLines(content))
        {
            if (rawLine.StartsWith("ID=", StringComparison.OrdinalIgnoreCase))
            {
                FinishShop();
                shopId = ParseInt(rawLine["ID=".Length..], "shop ID");
                moneyType = null;
                category = null;
                attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ID"] = shopId.Value.ToString(CultureInfo.InvariantCulture),
                };
                continue;
            }

            if (shopId is null)
            {
                throw new FormatException($"Shop line appears before ID in {sourceFile}: '{rawLine}'.");
            }

            if (rawLine.StartsWith('[') && rawLine.EndsWith(']'))
            {
                category = rawLine[1..^1].Trim();
                continue;
            }

            var equalsIndex = rawLine.IndexOf('=');
            if (equalsIndex <= 0)
            {
                throw new FormatException($"Invalid emoney shop line in {sourceFile}: '{rawLine}'.");
            }

            var key = rawLine[..equalsIndex].Trim();
            var value = rawLine[(equalsIndex + 1)..].Trim();
            if (key.Equals("MoneyType", StringComparison.OrdinalIgnoreCase))
            {
                moneyType = ParseInt(value, "money type");
                attributes["MoneyType"] = value;
                continue;
            }

            if (key.Equals("Amount", StringComparison.OrdinalIgnoreCase))
            {
                attributes[$"{category ?? "root"}.Amount"] = value;
                continue;
            }

            if (!TryGetItemPosition(key, out var position))
            {
                attributes[key] = value;
                continue;
            }

            items.Add(ParseLegacyEmoneyItem(
                shopId.Value,
                position,
                category,
                key,
                value,
                sourceFile));
        }

        FinishShop();
        return new ShopData(shops, items);
    }

    public static ShopData ParseEmoneyShopV2(string content, string sourceFile = "emoneyShopV2.ini")
    {
        var shops = new List<ShopDefinition>();
        var items = new List<ShopItem>();
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int? shopId = null;
        int? moneyType = null;
        var position = 0;
        string? section = null;

        void FinishShop()
        {
            if (shopId is null)
            {
                return;
            }

            shops.Add(new ShopDefinition(
                shopId.Value,
                Name: null,
                Type: null,
                moneyType,
                sourceFile,
                EmoneyShopV2Dialect,
                new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase)));
        }

        foreach (var rawLine in ReadMeaningfulLines(content))
        {
            if (rawLine.StartsWith("ID=", StringComparison.OrdinalIgnoreCase))
            {
                FinishShop();
                shopId = ParseInt(rawLine["ID=".Length..], "shop ID");
                moneyType = null;
                position = 0;
                section = null;
                attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ID"] = shopId.Value.ToString(CultureInfo.InvariantCulture),
                };
                continue;
            }

            if (shopId is null)
            {
                throw new FormatException($"Shop item appears before ID in {sourceFile}: '{rawLine}'.");
            }

            if (rawLine.StartsWith("MoneyType=", StringComparison.OrdinalIgnoreCase))
            {
                var value = rawLine["MoneyType=".Length..].Trim();
                moneyType = ParseInt(value, "money type");
                attributes["MoneyType"] = value;
                continue;
            }

            if (rawLine.StartsWith('[') && rawLine.EndsWith(']'))
            {
                section = rawLine[1..^1].Trim();
                continue;
            }

            var equalsIndex = rawLine.IndexOf('=');
            if (equalsIndex > 0)
            {
                var key = rawLine[..equalsIndex].Trim();
                var value = rawLine[(equalsIndex + 1)..].Trim();
                if (key.Equals("Amount", StringComparison.OrdinalIgnoreCase))
                {
                    attributes[$"{section ?? "root"}.Amount"] = value;
                    continue;
                }

                if (!TryGetItemPosition(key, out var itemPosition))
                {
                    attributes[key] = value;
                    continue;
                }

                items.Add(ParseEmoneyShopV2KeyedItem(shopId.Value, itemPosition, section, key, value, sourceFile));
                continue;
            }

            items.Add(ParseEmoneyShopV2Item(shopId.Value, position++, rawLine, sourceFile));
        }

        FinishShop();
        return new ShopData(shops, items);
    }

    public static ExchangeShopRow ParseExchangeRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count < 2)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has too few fields.");
        }

        _ = ParseInt(fields[0], $"{sourceFile} line {lineNumber} row id");
        return new ExchangeShopRow(sourceFile, lineNumber, fields);
    }

    public static HonorShopCatalog ParseHonorShop(string content, string sourceFile = "HonorShop.ini")
    {
        var items = new List<HonorShopItem>();
        var recommendations = new List<HonorShopRecommendation>();
        int? expectedItems = null;
        int? expectedRecommendations = null;
        var readingRecommendations = false;

        foreach (var rawLine in ReadMeaningfulLines(content))
        {
            if (rawLine.StartsWith("ItemAmount=", StringComparison.OrdinalIgnoreCase))
            {
                expectedItems = ParseInt(rawLine["ItemAmount=".Length..], "HonorShop item count");
                readingRecommendations = false;
                continue;
            }

            if (rawLine.StartsWith("RecommendAmount=", StringComparison.OrdinalIgnoreCase))
            {
                expectedRecommendations = ParseInt(rawLine["RecommendAmount=".Length..], "HonorShop recommendation count");
                readingRecommendations = true;
                continue;
            }

            if (expectedItems is null)
            {
                throw new FormatException($"HonorShop item row appears before ItemAmount: '{rawLine}'.");
            }

            if (readingRecommendations)
            {
                recommendations.Add(ParseHonorShopRecommendation(
                    rawLine,
                    position: recommendations.Count,
                    sourceFile));
            }
            else
            {
                items.Add(ParseHonorShopItem(
                    rawLine,
                    position: items.Count,
                    sourceFile));
            }
        }

        ValidateDeclaredCount(sourceFile, "ItemAmount", expectedItems, items.Count);
        ValidateDeclaredCount(sourceFile, "RecommendAmount", expectedRecommendations, recommendations.Count);

        return new HonorShopCatalog(items, recommendations);
    }

    public static PointShopCatalog ParsePointShop(string content, string sourceFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var items = new List<PointShopItem>();
        var recommendations = new List<PointShopRecommendation>();
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? section = null;

        foreach (var rawLine in ReadMeaningfulLines(content))
        {
            if (rawLine.StartsWith('[') && rawLine.EndsWith(']'))
            {
                section = rawLine[1..^1].Trim();
                continue;
            }

            if (section is null)
            {
                throw new FormatException($"Point shop row appears before a section in {sourceFile}: '{rawLine}'.");
            }

            var equalsIndex = rawLine.IndexOf('=');
            if (equalsIndex > 0)
            {
                config[$"{section}.{rawLine[..equalsIndex].Trim()}"] = rawLine[(equalsIndex + 1)..].Trim();
                continue;
            }

            var parts = SplitMax(rawLine, 3);
            if (parts.Length < 2)
            {
                throw new FormatException($"Invalid point shop row in {sourceFile}: '{rawLine}'.");
            }

            if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                items.Add(ParsePointShopItem(rawLine, section, items.Count, sourceFile));
            }
            else
            {
                recommendations.Add(ParsePointShopRecommendation(rawLine, section, recommendations.Count, sourceFile));
            }
        }

        return new PointShopCatalog(items, recommendations, config);
    }

    public static BoothCatalog ParseBooths(string content, string sourceFile = "Booths.txt")
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var booths = new List<BoothDefinition>();
        var items = new List<BoothItem>();
        Dictionary<string, string>? attributes = null;
        string? sourceSection = null;
        int boothSourceLine = 0;
        var itemSequence = 0;

        void FinishBooth()
        {
            if (attributes is null)
            {
                return;
            }

            if (!attributes.ContainsKey("ID"))
            {
                return;
            }

            var sectionLabel = sourceSection ?? string.Empty;
            var boothId = ParseRequiredInt(attributes, "ID", sectionLabel);
            booths.Add(new BoothDefinition(
                boothId,
                ParseRequiredString(attributes, "Name", sectionLabel),
                ParseRequiredInt(attributes, "Mesh", sectionLabel),
                ParseRequiredInt(attributes, "Map", sectionLabel),
                TryParseOptionalInt(attributes, "Type"),
                ParseRequiredInt(attributes, "X", sectionLabel),
                ParseRequiredInt(attributes, "Y", sectionLabel),
                TryParseOptionalInt(attributes, "ItemAmount"),
                sectionLabel,
                boothSourceLine,
                booths.Count,
                sourceFile,
                CopyAttributes(attributes)));
        }

        foreach (var (rawLine, lineNumber) in ReadMeaningfulLinesWithNumbers(content))
        {
            if (TryParseBoothSection(rawLine, out var boothId))
            {
                if (attributes is not null && attributes.ContainsKey("ID"))
                {
                    FinishBooth();
                    attributes = null;
                }

                sourceSection = "booth" + boothId.ToString(CultureInfo.InvariantCulture);
                itemSequence = 0;
                continue;
            }

            var equalsIndex = rawLine.IndexOf('=');
            if (equalsIndex <= 0)
            {
                throw new FormatException($"Invalid booth row in {sourceFile}: '{rawLine}'.");
            }

            var key = rawLine[..equalsIndex].Trim();
            var value = rawLine[(equalsIndex + 1)..].Trim();
            if (key.Equals("ID", StringComparison.OrdinalIgnoreCase))
            {
                if (attributes is not null && attributes.ContainsKey("ID"))
                {
                    FinishBooth();
                }

                attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ID"] = value,
                };
                boothSourceLine = lineNumber;
                sourceSection = "booth" + value;
                itemSequence = 0;
                continue;
            }

            if (attributes is null)
            {
                throw new FormatException($"Booth row appears before an ID in {sourceFile}: '{rawLine}'.");
            }

            if (TryGetItemPosition(key, out var itemKey))
            {
                items.Add(ParseBoothItem(
                    ParseRequiredInt(attributes, "ID", sourceSection ?? string.Empty),
                    itemKey,
                    itemSequence++,
                    key,
                    value,
                    sourceFile,
                    lineNumber));
                continue;
            }

            attributes[key] = value;
        }

        FinishBooth();
        return new BoothCatalog(booths, items);
    }

    private static HonorShopItem ParseHonorShopItem(string rawLine, int position, string sourceFile)
    {
        var fields = rawLine
            .Split(CsvSeparator, StringSplitOptions.None)
            .Select(field => field.Trim())
            .ToArray();

        if (fields.Length is < 4 or > 6)
        {
            throw new FormatException($"HonorShop item row has {fields.Length} fields; expected 4 to 6: '{rawLine}'.");
        }

        var isRecommended = fields.Length > 4 && fields[4].Equals("recommend", StringComparison.OrdinalIgnoreCase);
        string? status = null;
        foreach (var field in fields.Skip(4).Where(field => field.Length > 0))
        {
            if (IsStatus(field))
            {
                status = NullIfLiteral(field);
            }
        }

        return new HonorShopItem(
            ParseInt(fields[0], "HonorShop item type id"),
            ParseInt(fields[1], "HonorShop cost"),
            ParseInt(fields[2], "HonorShop unknown0"),
            fields[3],
            isRecommended,
            status,
            position,
            sourceFile,
            rawLine,
            fields);
    }

    private static HonorShopRecommendation ParseHonorShopRecommendation(string rawLine, int position, string sourceFile)
    {
        var equalsIndex = rawLine.IndexOf('=');
        if (equalsIndex <= 0)
        {
            throw new FormatException($"Invalid HonorShop recommendation row: '{rawLine}'.");
        }

        var itemTypeId = ParseInt(rawLine[..equalsIndex].Trim(), "HonorShop recommendation item type id");
        var text = rawLine[(equalsIndex + 1)..].Trim();
        return new HonorShopRecommendation(itemTypeId, text, position, sourceFile, rawLine);
    }

    private static PointShopItem ParsePointShopItem(string rawLine, string section, int position, string sourceFile)
    {
        var fields = rawLine
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length is < 4 or > 5)
        {
            throw new FormatException($"Point shop item row has {fields.Length} fields; expected 4 or 5: '{rawLine}'.");
        }

        return new PointShopItem(
            ParseInt(fields[0], "point shop item type id"),
            ParseInt(fields[1], "point shop cost"),
            fields[2],
            fields[3],
            fields.Length > 4 ? NullIfLiteral(fields[4]) : null,
            section,
            position,
            sourceFile,
            rawLine,
            fields);
    }

    private static PointShopRecommendation ParsePointShopRecommendation(
        string rawLine,
        string section,
        int position,
        string sourceFile)
    {
        var parts = SplitMax(rawLine, 2);
        if (parts.Length < 2)
        {
            throw new FormatException($"Invalid point shop recommendation row: '{rawLine}'.");
        }

        return new PointShopRecommendation(
            ParseInt(parts[0], "point shop recommendation item type id"),
            parts[1],
            section,
            position,
            sourceFile,
            rawLine);
    }

    private static BoothItem ParseBoothItem(
        int boothId,
        int itemKey,
        int sequence,
        string key,
        string value,
        string sourceFile,
        int sourceLine)
    {
        var fields = AtAtTableParser.SplitRow(value)
            .Select(field => field.Trim())
            .ToArray();
        if (fields.Length < 2)
        {
            throw new FormatException($"Booth item row has {fields.Length} fields; expected at least 2: '{key}={value}'.");
        }

        return new BoothItem(
            boothId,
            ParseInt(fields[0], "booth item type id"),
            ParseLong(fields[1], "booth item price"),
            fields.Length > 2 && fields[2].Length > 0 ? ParseInt(fields[2], "booth item unknown0") : null,
            fields.Length > 3 ? fields[3] : null,
            fields.Length > 4 ? fields[4] : null,
            itemKey,
            sequence,
            sourceFile,
            sourceLine,
            $"{key}={value}",
            fields);
    }

    private static ShopItem ParseLegacyEmoneyItem(
        int shopId,
        int position,
        string? category,
        string key,
        string value,
        string sourceFile)
    {
        var parts = SplitMax(value, 3);
        if (parts.Length == 0)
        {
            throw new FormatException($"Empty shop item value for {key}.");
        }

        var itemIdText = parts[0];
        string? status = null;
        if (parts.Length > 1)
        {
            status = NullIfLiteral(parts[1]);
        }
        else if (TrySplitUnderscoreStatus(parts[0], out var splitItemId, out var splitStatus))
        {
            itemIdText = splitItemId;
            status = NullIfLiteral(splitStatus);
        }

        return new ShopItem(
            shopId,
            ParseInt(itemIdText, "item type id"),
            position,
            category,
            status,
            IsBound: false,
            Price: null,
            Description: parts.Length > 2 ? parts[2] : null,
            Flags: status is null ? [] : [status],
            sourceFile,
            EmoneyShopDialect,
            $"{key}={value}",
            new Dictionary<string, string>
            {
                ["key"] = key,
                ["section"] = category ?? string.Empty,
            });
    }

    private static ShopItem ParseEmoneyShopV2Item(int shopId, int position, string rawLine, string sourceFile)
    {
        var parts = SplitMax(rawLine, 3);
        if (parts.Length < 2)
        {
            throw new FormatException($"Invalid emoneyShopV2 row: '{rawLine}'.");
        }

        var flags = parts[1]
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var status = flags.FirstOrDefault(IsStatus);
        var category = flags.FirstOrDefault(flag => !IsStatus(flag));
        var isBound = HasLegacyBoundPresentFlag(flags);

        return new ShopItem(
            shopId,
            ParseInt(parts[0], "item type id"),
            position,
            category,
            NullIfLiteral(status),
            isBound,
            Price: null,
            Description: parts.Length > 2 ? parts[2] : null,
            Flags: flags,
            sourceFile,
            EmoneyShopV2Dialect,
            rawLine,
            new Dictionary<string, string>());
    }

    private static ShopItem ParseEmoneyShopV2KeyedItem(
        int shopId,
        int position,
        string? section,
        string key,
        string value,
        string sourceFile)
    {
        var parts = SplitMax(value, 3);
        if (parts.Length < 2)
        {
            throw new FormatException($"Invalid emoneyShopV2 keyed row: '{key}={value}'.");
        }

        var flags = parts[1]
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var status = flags.FirstOrDefault(IsStatus);
        if (status is null && IsStatus(parts[1]))
        {
            status = parts[1];
        }
        var isBound = HasLegacyBoundPresentFlag(flags);

        return new ShopItem(
            shopId,
            ParseInt(parts[0], "item type id"),
            position,
            section,
            NullIfLiteral(status),
            isBound,
            Price: null,
            Description: parts.Length > 2 ? parts[2] : null,
            Flags: flags,
            sourceFile,
            EmoneyShopV2Dialect,
            $"{key}={value}",
            new Dictionary<string, string>
            {
                ["key"] = key,
                ["section"] = section ?? string.Empty,
            });
    }

    private static IEnumerable<string> ReadMeaningfulLines(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var reader = new StringReader(content);
        while (reader.ReadLine() is { } rawLine)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            yield return line;
        }
    }

    private static IEnumerable<(string Line, int LineNumber)> ReadMeaningfulLinesWithNumbers(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var reader = new StringReader(content);
        var lineNumber = 0;
        while (reader.ReadLine() is { } rawLine)
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            yield return (line, lineNumber);
        }
    }

    private static IReadOnlyDictionary<string, string> CopyAttributes(IReadOnlyDictionary<string, string> values)
    {
        return new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<(string Key, int Position, string Value)> EnumerateItemValues(IReadOnlyDictionary<string, string> values)
    {
        return values
            .Where(pair => TryGetItemPosition(pair.Key, out _))
            .Select(pair => (pair.Key, Position: GetItemPosition(pair.Key), pair.Value))
            .OrderBy(item => item.Position)
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryGetShopSectionIndex(string sectionName, out int index)
    {
        index = 0;
        return sectionName.StartsWith("Shop", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(sectionName["Shop".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
    }

    private static int GetShopSectionIndex(string sectionName)
    {
        return TryGetShopSectionIndex(sectionName, out var index)
            ? index
            : throw new FormatException($"Invalid shop section name: '{sectionName}'.");
    }

    private static bool TryGetItemPosition(string key, out int position)
    {
        position = 0;
        return key.StartsWith("Item", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(key["Item".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out position);
    }

    private static int GetItemPosition(string key)
    {
        return TryGetItemPosition(key, out var position)
            ? position
            : throw new FormatException($"Invalid item key: '{key}'.");
    }

    private static bool TryParseBoothSection(string line, out int boothId)
    {
        boothId = 0;
        var section = line.Trim();
        if (section.StartsWith('['))
        {
            section = section[1..];
        }

        if (!section.EndsWith(']'))
        {
            return false;
        }

        section = section[..^1].Trim();
        return section.StartsWith("booth", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(section["booth".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out boothId);
    }

    private static string? GetOptional(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) && value.Length > 0 ? value : null;
    }

    private static int ParseRequiredInt(IReadOnlyDictionary<string, string> values, string key, string sectionName)
    {
        if (!values.TryGetValue(key, out var value))
        {
            throw new FormatException($"Shop section '{sectionName}' is missing required key '{key}'.");
        }

        return ParseInt(value, key);
    }

    private static string ParseRequiredString(IReadOnlyDictionary<string, string> values, string key, string sectionName)
    {
        if (!values.TryGetValue(key, out var value))
        {
            throw new FormatException($"Shop section '{sectionName}' is missing required key '{key}'.");
        }

        return value;
    }

    private static int? TryParseOptionalInt(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) && value.Length > 0
            ? ParseInt(value, key)
            : null;
    }

    private static int ParseInt(string value, string fieldName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid {fieldName}: '{value}'.");
        }

        return parsed;
    }

    private static long ParseLong(string value, string fieldName)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid {fieldName}: '{value}'.");
        }

        return parsed;
    }

    private static string[] SplitMax(string value, int count)
    {
        return value.Split(' ', count, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool TrySplitUnderscoreStatus(string value, out string itemId, out string status)
    {
        var separatorIndex = value.LastIndexOf('_');
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
        {
            itemId = value;
            status = string.Empty;
            return false;
        }

        var candidateStatus = value[(separatorIndex + 1)..];
        if (!IsStatus(candidateStatus))
        {
            itemId = value;
            status = string.Empty;
            return false;
        }

        itemId = value[..separatorIndex];
        status = candidateStatus;
        return true;
    }

    private static bool IsStatus(string value)
    {
        return value.Equals("new", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("hot", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("null", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasLegacyBoundPresentFlag(IEnumerable<string> flags)
    {
        return flags.Any(flag => flag.Equals("present", StringComparison.OrdinalIgnoreCase));
    }

    private static string? NullIfLiteral(string? value)
    {
        return value is null || value.Equals("null", StringComparison.OrdinalIgnoreCase) ? null : value;
    }

    private static void ValidateDeclaredCount(string sourceFile, string countName, int? expected, int actual)
    {
        if (expected is null)
        {
            throw new FormatException($"{sourceFile} is missing {countName}.");
        }

        if (expected.Value != actual)
        {
            throw new FormatException($"{sourceFile} {countName} is {expected.Value}, but parsed {actual} rows.");
        }
    }
}

using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

internal static class PrestigeRankingQueryTestData
{
    // The 66-column persisted shape comes from RankingSnapshotParser and the
    // plain C# PrestigeRanking.Load/Save source. It is not a live-client capture.
    internal static PrestigeRankEntry Row(int uid = 41, uint total = 30_004,
        uint category = 4, string name = "Hero", string guild = "Guild")
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = uid.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[2] = category.ToString(CultureInfo.InvariantCulture);
        fields[3] = "5051";
        fields[4] = "140";
        fields[5] = "7";
        fields[6] = total.ToString(CultureInfo.InvariantCulture);
        for (var index = 7; index <= 19; index++)
            fields[index] = (index + 1).ToString(CultureInfo.InvariantCulture);
        fields[20] = guild;
        fields[21] = "43";
        for (var index = 22; index <= 64; index++)
            fields[index] = (index + 979).ToString(CultureInfo.InvariantCulture);
        fields[65] = "";
        return RankingSnapshotParser.ParsePrestigeRankRow("fixture://prestige-ranking", 2,
            string.Join('/', fields));
    }

    internal static PrestigeRankEntry ChangeField(PrestigeRankEntry row, int index, string value)
    {
        var fields = row.Fields.ToArray();
        fields[index] = value;
        return row with
        {
            CharacterId = int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var uid) ? uid : row.CharacterId,
            CharacterName = fields[1],
            Fields = fields,
            RankingFields = fields[2..65],
            RawLine = string.Join('/', fields),
        };
    }

    internal static PrestigeRankEntry? Corruption(string kind)
    {
        var row = Row(99, total: 1); // Even an ineligible tail must be validated.
        return kind switch
        {
            "null-row" => null,
            "null-fields" => row with { Fields = null! },
            "null-ranking-fields" => row with { RankingFields = null! },
            "null-name" => row with { CharacterName = null! },
            "missing-source" => row with { SourceFile = "" },
            "invalid-line-number" => row with { LineNumber = 0 },
            "null-raw-line" => row with { RawLine = null! },
            "raw-line-mirror" => row with { RawLine = "99/Other/" },
            "missing-fields" => row with { Fields = row.Fields.Take(65).ToArray() },
            "extra-fields" => row with { Fields = row.Fields.Concat([""]).ToArray() },
            "missing-ranking-field" => row with { RankingFields = row.RankingFields.Take(62).ToArray() },
            "extra-ranking-field" => row with { RankingFields = row.RankingFields.Concat(["0"]).ToArray() },
            "id-mirror" => row with { CharacterId = 100 },
            "name-mirror" => row with { CharacterName = "Different" },
            "ranking-mirror" => row with { RankingFields = row.RankingFields.Select((v, i) => i == 0 ? "3" : v).ToArray() },
            "null-numeric-field" => ChangeField(row, 64, null!),
            "null-guild" => ChangeField(row, 20, null!),
            "nonempty-terminator" => ChangeField(row, 65, "junk"),
            "zero-uid" => ChangeField(row, 0, "0"),
            "negative-uid" => ChangeField(row, 0, "-1"),
            "uid-overflow" => ChangeField(row, 0, "2147483648"),
            "blank-name" => ChangeField(row, 1, " "),
            "name-control" => ChangeField(row, 1, "Bad\nName"),
            "guild-control" => ChangeField(row, 20, "Bad\nGuild"),
            "name-high-surrogate" => ChangeField(row, 1, "Bad\uD800Name"),
            "name-low-surrogate" => ChangeField(row, 1, "Bad\uDC00Name"),
            "guild-high-surrogate" => ChangeField(row, 20, "Bad\uD800Guild"),
            "guild-low-surrogate" => ChangeField(row, 20, "Bad\uDC00Guild"),
            "world-category" => ChangeField(row, 2, "0"),
            "category-outside-range" => ChangeField(row, 2, "12"),
            "level-overflow" => ChangeField(row, 4, "256"),
            "negative-points-count" => ChangeField(row, 21, "-1"),
            "short-points-count" => ChangeField(row, 21, "42"),
            "long-points-count" => ChangeField(row, 21, "44"),
            "huge-points-count" => ChangeField(row, 21, "2147483647"),
            "tail-not-numeric" => ChangeField(row, 64, "not-a-number"),
            "tail-overflow" => ChangeField(row, 64, "4294967296"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    internal static readonly string[] Corruptions =
    [
        "null-row", "null-fields", "null-ranking-fields", "null-name", "missing-source",
        "invalid-line-number", "null-raw-line", "raw-line-mirror", "missing-fields",
        "extra-fields", "missing-ranking-field", "extra-ranking-field", "id-mirror", "name-mirror",
        "ranking-mirror", "null-numeric-field", "null-guild", "nonempty-terminator", "zero-uid",
        "negative-uid", "uid-overflow", "blank-name", "name-control", "guild-control",
        "name-high-surrogate", "name-low-surrogate", "guild-high-surrogate", "guild-low-surrogate",
        "world-category", "category-outside-range", "level-overflow",
        "negative-points-count", "short-points-count", "long-points-count", "huge-points-count",
        "tail-not-numeric", "tail-overflow",
    ];
}

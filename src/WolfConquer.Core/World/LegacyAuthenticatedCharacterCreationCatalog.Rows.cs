using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyAuthenticatedCharacterCreationCatalog
{
    private sealed record ItemRow(ItemType Row, PlainList<ItemType> Group, PlainList<string> Fields)
    {
        internal bool IsCurrent(IReadOnlyList<ItemType> group) => Group.IsCurrent(group) && Fields.IsCurrent(Row.Fields);
    }
    private sealed record ProfessionRow(ProfessionLevelBenefit Row, PlainList<string> Fields, PlainList<int> Bonuses)
    {
        internal bool IsCurrent() => Fields.IsCurrent(Row.Fields) && Bonuses.IsCurrent(Row.BonusValues);
    }
    private sealed record RuneRow(RuneStorageAttribute Row, PlainList<string> Fields, PlainList<RuneStorageAttributeBonus> Bonuses)
    {
        internal bool IsCurrent() => Fields.IsCurrent(Row.Fields) && Bonuses.IsCurrent(Row.Bonuses);
    }
    private sealed record InnerRow(InnerPowerRankEntry Row, PlainList<string> Fields, PlainList<string> Ranking)
    {
        internal bool IsCurrent() => Fields.IsCurrent(Row.Fields) && Ranking.IsCurrent(Row.RankingFields);
    }

    private ProfessionRow CaptureProfession(ProfessionLevelBenefit row)
    {
        var fields = new PlainList<string>(row.Fields); var bonuses = new PlainList<int>(row.BonusValues);
        if (fields.Values.Length != 9 || !int.TryParse(fields.Values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int key) || key != row.ProfessionCode)
            throw Invalid();
        return new(row, fields, bonuses);
    }
    private RuneRow CaptureRune(RuneStorageAttribute row)
    {
        var fields = new PlainList<string>(row.Fields); var bonuses = new PlainList<RuneStorageAttributeBonus>(row.Bonuses);
        if (fields.Values.Length != 9 || !int.TryParse(fields.Values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int key) || key != row.RequiredStorageScore)
            throw Invalid();
        if (key == 0)
        {
            var parsed = LegacyAuthenticatedCharacterCreationMetadata.ParseRune(fields.Values);
            if (parsed.Id != row.Id || !parsed.Bonuses.SequenceEqual(bonuses.Values)) throw Invalid();
        }
        return new(row, fields, bonuses);
    }
    private InnerRow CaptureInner(InnerPowerRankEntry row, uint account)
    {
        var fields = new PlainList<string>(row.Fields); var ranking = new PlainList<string>(row.RankingFields);
        if (fields.Values.Length < 2 || !int.TryParse(fields.Values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int key) ||
            key != row.CharacterId || key == account) throw Invalid();
        return new(row, fields, ranking);
    }
}

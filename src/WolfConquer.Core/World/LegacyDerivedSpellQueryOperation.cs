using System.Collections.ObjectModel;
using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal enum LegacyDerivedSpellQuerySource { Rank = 1, MythSoul = 2 }
internal sealed record LegacyDerivedSpellQueryRankInput(int CharacterId, IReadOnlyList<string> Fields);

// Historical DATA only; the real private query/writer separately owns a grant.
internal sealed partial class LegacyDerivedSpellQueryOperation
{
    private readonly byte[] itemsBefore;
    public uint TargetUid { get; }
    public LegacyDerivedSpellQuerySource Source { get; }
    public bool Alternate { get; }
    public byte[] ItemsBefore => itemsBefore.ToArray();
    public IReadOnlyList<LegacyDerivedSpellQueryRankInput> RankingRows { get; }
    public MagicType? GrantedRow { get; }

    internal LegacyDerivedSpellQueryOperation(uint targetUid, LegacyDerivedSpellQuerySource source, bool alternate,
        byte[] itemsBefore, IReadOnlyList<LegacyDerivedSpellQueryRankInput> rankingRows, MagicType? grantedRow)
    {
        if (itemsBefore is null || itemsBefore.Length is 0 or > LegacyTradeJournal.DerivedSpellQueryMaximumReferenceBytes ||
            rankingRows is null || rankingRows.Count > ushort.MaxValue) throw Invalid();
        TargetUid = targetUid; Source = source; Alternate = alternate; this.itemsBefore = itemsBefore.ToArray();
        RankingRows = Array.AsReadOnly(rankingRows.Select(row => row is null ? throw Invalid() :
            new LegacyDerivedSpellQueryRankInput(row.CharacterId, CopyFields(row.Fields))).ToArray());
        GrantedRow = grantedRow is null ? null : new(grantedRow.Id, grantedRow.TypeId, grantedRow.Name, CopyFields(grantedRow.Fields));
        Validate();
    }

    internal void Validate()
    {
        if (TargetUid == 0 || Source is not (LegacyDerivedSpellQuerySource.Rank or LegacyDerivedSpellQuerySource.MythSoul) ||
            Source == LegacyDerivedSpellQuerySource.MythSoul && RankingRows.Count != 0) throw Invalid();
        long characters = 0;
        foreach (var row in RankingRows)
        {
            if (row.Fields.Count == 0 || !int.TryParse(row.Fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var id) || id != row.CharacterId) throw Invalid();
            characters = checked(characters + row.Fields.Sum(field => (long)field.Length));
        }
        if (GrantedRow is { } granted)
        {
            if (granted.Name is null || granted.Name.Length > 4096 || granted.TypeId != SourceId) throw Invalid();
            characters = checked(characters + granted.Name.Length + granted.Fields.Sum(field => (long)field.Length));
        }
        if (characters > 4 * 1024 * 1024) throw Invalid();
        _ = PlayerItemSnapshotLoader.Parse(itemsBefore);
    }

    internal ushort SourceId => Source == LegacyDerivedSpellQuerySource.Rank ? (ushort)21_930 : (ushort)20_000;

    private static ReadOnlyCollection<string> CopyFields(IReadOnlyList<string> fields)
    {
        if (fields is null || fields.Count > 512) throw Invalid();
        var rows = fields.ToArray();
        if (rows.Any(field => field is null || field.Length > 4096 || field.Contains('\0'))) throw Invalid();
        return Array.AsReadOnly(rows);
    }

    private static InvalidDataException Invalid() => new("derivedSpellQuery23OperationInvalid");
}

using System.Collections.ObjectModel;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyPhysicalDeathRankInput(int CharacterId, IReadOnlyList<string> Fields);

// Historical DATA only. It cannot issue a hit, adopt a catalog or commit files.
internal sealed partial class LegacyPhysicalDeathOperation
{
    private readonly byte[] itemsBefore;
    public uint TargetUid { get; }
    public bool QueryAdmitted { get; }
    public bool Alternate { get; }
    public byte[] ItemsBefore => (byte[])itemsBefore.Clone();
    public IReadOnlyList<LegacyPhysicalDeathRankInput> RankingRows { get; }
    public IReadOnlyList<WorldPacketHandlers.OwnedPhysicalDeathDerivedSpellChange> DerivedSpells { get; }
    public IReadOnlyList<MagicType> GrantedRows { get; }

    internal LegacyPhysicalDeathOperation(uint targetUid, bool queryAdmitted, bool alternate, byte[] itemsBefore,
        IReadOnlyList<LegacyPhysicalDeathRankInput> rankingRows,
        IReadOnlyList<WorldPacketHandlers.OwnedPhysicalDeathDerivedSpellChange> derivedSpells,
        IReadOnlyList<MagicType> grantedRows)
    {
        if (itemsBefore is null || itemsBefore.Length is 0 or > LegacyTradeJournal.PhysicalDeathMaximumReferenceBytes ||
            rankingRows is null || rankingRows.Count > ushort.MaxValue || derivedSpells is null || derivedSpells.Count > 2 ||
            grantedRows is null || grantedRows.Count > 2) throw Invalid();
        TargetUid = targetUid; QueryAdmitted = queryAdmitted; Alternate = alternate;
        this.itemsBefore = (byte[])itemsBefore.Clone();
        RankingRows = Array.AsReadOnly(rankingRows.Select(row => row is null ? throw Invalid() :
            new LegacyPhysicalDeathRankInput(row.CharacterId, CopyFields(row.Fields))).ToArray());
        DerivedSpells = Array.AsReadOnly(derivedSpells.Select(row => row is null ? throw Invalid() :
            new WorldPacketHandlers.OwnedPhysicalDeathDerivedSpellChange(row.Id, row.After is null ? null : row.After with { })).ToArray());
        GrantedRows = Array.AsReadOnly(grantedRows.Select(row => row is null ? throw Invalid() :
            new MagicType(row.Id, row.TypeId, row.Name, CopyFields(row.Fields))).ToArray());
        Validate();
    }

    internal void Validate()
    {
        if (TargetUid == 0 || !QueryAdmitted && (RankingRows.Count != 0 || DerivedSpells.Count != 0 || GrantedRows.Count != 0))
            throw Invalid();
        long metadataCharacters = 0;
        foreach (var row in RankingRows)
        {
            if (row.Fields.Count == 0 || !int.TryParse(row.Fields[0], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var rawId) || rawId != row.CharacterId) throw Invalid();
            metadataCharacters = checked(metadataCharacters + row.Fields.Sum(field => (long)field.Length));
        }
        foreach (var row in GrantedRows)
        {
            if (row.Name is null || row.Name.Length > 4096) throw Invalid();
            metadataCharacters = checked(metadataCharacters + row.Name.Length + row.Fields.Sum(field => (long)field.Length));
        }
        // Finite journal allocation bounds, not gameplay/ranking limits.
        if (metadataCharacters > 4 * 1024 * 1024) throw Invalid();
        var ids = new HashSet<ushort>(); int last = -1;
        foreach (var change in DerivedSpells)
        {
            int order = change.Id switch { 21_930 => 0, 20_000 => 1, _ => throw Invalid() };
            if (order <= last || !ids.Add(change.Id)) throw Invalid();
            last = order;
            if (change.After is { } after && (after.Id != change.Id || after.Experience != 0 ||
                after.PreviousLevel != 0 || after.SoulLevel != 0 || after.UseSpellSoul != 0)) throw Invalid();
        }
        var sets = DerivedSpells.Where(change => change.After is not null).ToArray();
        if (sets.Length != GrantedRows.Count) throw Invalid();
        for (int index = 0; index < sets.Length; index++)
            if (!LegacyMagicRuntime.IsRecordedDerivedSpellRow(GrantedRows[index], sets[index].Id, sets[index].After!.Level))
                throw Invalid();
        _ = PlayerItemSnapshotLoader.Parse(itemsBefore);
    }

    private static ReadOnlyCollection<string> CopyFields(IReadOnlyList<string> fields)
    {
        if (fields is null || fields.Count > 512) throw Invalid();
        var copy = fields.ToArray();
        if (copy.Any(field => field is null || field.Length > 4096 || field.Contains('\0'))) throw Invalid();
        return Array.AsReadOnly(copy);
    }

    private static InvalidDataException Invalid() => new("physicalDeath22IntentInvalid");
}

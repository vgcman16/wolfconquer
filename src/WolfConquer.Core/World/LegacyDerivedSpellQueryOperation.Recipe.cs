using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyDerivedSpellQueryOperation
{
    internal LegacyDerivedSpellReconciliation.Physical Project(byte[] characterBefore, byte[]? spellsBefore)
    {
        try
        {
            Validate();
            if (characterBefore is null || characterBefore.Length is 0 or > LegacyTradeJournal.DerivedSpellQueryMaximumReferenceBytes ||
                spellsBefore is { Length: > LegacyTradeJournal.DerivedSpellQueryMaximumReferenceBytes }) throw Invalid();
            var character = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(characterBefore, out _));
            if (character.Uid != TargetUid) throw Invalid();
            var spells = spellsBefore is null ? PlayerSpellSnapshot.Empty : PlayerSpellSnapshotLoader.Parse(spellsBefore);
            var desired = Derive(character, spells);
            var projected = LegacyDerivedSpellReconciliation.Project(characterBefore, spellsBefore, new[] { desired });
            if (projected.CopyCharacterAfter().Length > LegacyTradeJournal.DerivedSpellQueryMaximumReferenceBytes ||
                projected.CopySpellsAfter().Length > LegacyTradeJournal.DerivedSpellQueryMaximumReferenceBytes) throw Invalid();
            if (projected.Reconciled.Changes.Count != 1 || projected.Reconciled.Changes[0] != desired) throw Invalid();
            if (desired.After is { } grant)
            {
                if (GrantedRow is null || !LegacyMagicRuntime.IsRecordedDerivedSpellRow(GrantedRow, SourceId, grant.Level)) throw Invalid();
            }
            else if (GrantedRow is not null) throw Invalid();
            return projected;
        }
        catch (Exception error) when (error is InvalidOperationException or FormatException or OverflowException)
        { throw new InvalidDataException("derivedSpellQuery23ProjectionInvalid", error); }
    }

    internal LegacyDerivedSpellDesiredRow Derive(CharacterSnapshot character, PlayerSpellSnapshot spells)
    {
        if (character.Uid != TargetUid) throw Invalid();
        ushort? level;
        if (Source == LegacyDerivedSpellQuerySource.Rank)
        {
            if (character.Class / 1000 is not (2 or 4 or 7 or 13 or 14))
            {
                // Explicit owned-provider retirement after a real class
                // change, not adoption/deletion of an unmarked old row.
                if (RankingRows.Count != 0) throw Invalid();
                level = null;
            }
            else
            {
                uint weekly = Rank(40), overall = Rank(42), rank = weekly is >= 1 and <= 10 ? weekly : overall;
                level = character.Class is >= 7_000 and <= 7_051 && rank is >= 1 and <= 10 ? checked((ushort)(11 - rank)) : null;
            }
        }
        else if (Source == LegacyDerivedSpellQuerySource.MythSoul)
        {
            byte venom = 0;
            var items = PlayerItemSnapshotLoader.Parse(itemsBefore);
            foreach (var item in LegacyEquipmentQuerySelector.Select(character, items, Alternate, spells))
            {
                byte tier = checked((byte)(item.MythSoul % 10));
                if (item.MythSoul != 0 && item.MythSoul % 1000 - tier == 10) venom = checked((byte)(venom + tier));
            }
            level = venom == 0 ? null : venom;
        }
        else throw Invalid();
        return new(SourceId, level is { } value ? LegacyDerivedSpellOrigins.Granted(SourceId, value) : null);
    }

    private uint Rank(byte type)
    {
        var rows = new Dictionary<uint, uint>();
        foreach (var row in RankingRows)
        {
            if (row.CharacterId < 0 || row.Fields.Count < 4 ||
                !byte.TryParse(row.Fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kind) || kind != type ||
                !uint.TryParse(row.Fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var points)) continue;
            rows[checked((uint)row.CharacterId)] = points;
            rows = rows.OrderByDescending(pair => pair.Value).Take(10).ToDictionary(pair => pair.Key, pair => pair.Value);
        }
        int index = Array.IndexOf(rows.Keys.ToArray(), TargetUid);
        return index < 0 ? 0 : checked((uint)(index + 1));
    }
}

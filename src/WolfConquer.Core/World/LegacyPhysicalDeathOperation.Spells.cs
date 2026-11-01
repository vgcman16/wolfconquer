using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPhysicalDeathOperation
{
    internal byte[] ProjectSpells(byte[]? physicalBefore, CharacterSnapshot character)
    {
        Validate();
        if (character.Uid != TargetUid || physicalBefore is { Length: > LegacyTradeJournal.PhysicalDeathMaximumReferenceBytes }) throw Invalid();
        var original = physicalBefore is null ? PlayerSpellSnapshot.Empty : PlayerSpellSnapshotLoader.Parse(physicalBefore);
        var current = ReconcileRecordedSpells(physicalBefore, character).Spells;
        var bytes = PlayerSpellSnapshotLoader.ToLegacyBytes(current);
        var offsets = original.Spells.Select((row, index) => (row.Id,
                Offset: sizeof(int) + index * PlayerSpellSnapshotLoader.LegacySpellSize))
            .ToDictionary(row => row.Id, row => row.Offset);
        if (physicalBefore is not null)
            for (int index = 0; index < current.Spells.Count; index++)
                if (offsets.TryGetValue(current.Spells[index].Id, out var offset))
                    bytes[sizeof(int) + index * PlayerSpellSnapshotLoader.LegacySpellSize + 11] = physicalBefore[offset + 11];
        return bytes;
    }

    private LegacyDerivedSpellReconciliation.Model ReconcileRecordedSpells(byte[]? physicalBefore, CharacterSnapshot character)
    {
        if (physicalBefore is { Length: > LegacyTradeJournal.PhysicalDeathMaximumReferenceBytes }) throw Invalid();
        LegacyDerivedSpellOrigins.RequireSavedRows(character, physicalBefore);
        var original = physicalBefore is null ? PlayerSpellSnapshot.Empty : PlayerSpellSnapshotLoader.Parse(physicalBefore);
        var current = LegacyDerivedSpellOrigins.Freeze(original);
        var desired = CalculateChanges(character, current).Select(change => new LegacyDerivedSpellDesiredRow(change.Id, change.After)).ToArray();
        var result = LegacyDerivedSpellReconciliation.Project(character, current, desired);
        if (result.Changes.Count != DerivedSpells.Count) throw Invalid();
        for (int index = 0; index < result.Changes.Count; index++)
            if (result.Changes[index].Id != DerivedSpells[index].Id || result.Changes[index].After != DerivedSpells[index].After) throw Invalid();
        return result;
    }

    private IReadOnlyList<WorldPacketHandlers.OwnedPhysicalDeathDerivedSpellChange> CalculateChanges(
        CharacterSnapshot character, PlayerSpellSnapshot spells)
    {
        var changes = new List<WorldPacketHandlers.OwnedPhysicalDeathDerivedSpellChange>();
        if (!QueryAdmitted) return changes;
        bool usesRanking = character.Class / 1000 is 2 or 4 or 7 or 13 or 14;
        if (!usesRanking && RankingRows.Count != 0) throw Invalid();
        if (usesRanking)
        {
            uint overall = PirateRank(42), weekly = PirateRank(40);
            uint rank = weekly is >= 1 and <= 10 ? weekly : overall;
            if (character.Class is >= 7_000 and <= 7_051 && rank is >= 1 and <= 10)
                changes.Add(new(21_930, new PlayerSpellSnapshotEntry(21_930, checked((ushort)(11 - rank)), 0, 0, 0, 0)));
            else if (spells.ContainsSpell(21_930)) changes.Add(new(21_930, null));
        }
        else if (LegacyDerivedSpellOrigins.Read(character).ContainsKey(21_930)) changes.Add(new(21_930, null));
        var items = PlayerItemSnapshotLoader.Parse(itemsBefore);
        byte venom = 0;
        foreach (var item in LegacyEquipmentQuerySelector.Select(character, items, Alternate, spells))
        {
            byte level = checked((byte)(item.MythSoul % 10));
            if (item.MythSoul != 0 && item.MythSoul % 1000 - level == 10) venom = checked((byte)(venom + level));
        }
        if (venom != 0) changes.Add(new(20_000, new PlayerSpellSnapshotEntry(20_000, venom, 0, 0, 0, 0)));
        else if (spells.ContainsSpell(20_000)) changes.Add(new(20_000, null));
        return changes;
    }

    private uint PirateRank(byte type)
    {
        var ranking = new Dictionary<uint, uint>();
        foreach (var row in RankingRows)
        {
            if (row.CharacterId < 0 || row.Fields.Count < 4 ||
                !byte.TryParse(row.Fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kind) || kind != type ||
                !uint.TryParse(row.Fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var points)) continue;
            ranking[unchecked((uint)row.CharacterId)] = points;
            ranking = ranking.OrderByDescending(pair => pair.Value).Take(10).ToDictionary(pair => pair.Key, pair => pair.Value);
        }
        int index = Array.IndexOf(ranking.Keys.ToArray(), TargetUid);
        return index < 0 ? 0 : checked((uint)index + 1);
    }
}

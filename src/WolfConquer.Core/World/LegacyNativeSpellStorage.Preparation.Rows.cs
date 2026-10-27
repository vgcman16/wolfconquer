using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSpellStorage
{
    private const int MaximumPhysicalSpells = ushort.MaxValue + 1;
    private readonly record struct SpellValues(DateTime ColdTime, bool WithColdTime,
        ushort Size, ushort PacketType, int PacketStamp, int Experience, ushort Id, ushort Unknown,
        ushort Level, ushort Type, LegacyNativeMsgSpell.UseSpellSoulTyp UseSoul, byte Soul,
        int HashLevelSoul, DateTime Stamp, byte PreviousLevel, byte SoulLevel);
    private readonly record struct SpellRow(ushort Key, LegacyNativeMsgSpell Row, SpellValues Values);

    private static SpellValues ReadSpellValues(LegacyNativeMsgSpell row) => new(row.ColdTime,
        row.IsSpellWithColdTime, row.Size, row.PacketType, row.PacketStamp, row.Experience, row.ID,
        row.UnKnow, row.Level, row.Type, row.UseSoul, row.Soul, row.HashLevelSoul, row.StampSpell,
        row.PreviousLevel, row.SoulLevel);

    private static SpellRow[] CaptureNinjaSpellRows(ConcurrentDictionary<ushort, LegacyNativeMsgSpell> map)
    {
        if (map is null || map.Count > MaximumPhysicalSpells) throw UnavailableNinjaSpells();
        var rows = new List<SpellRow>();
        var ids = new HashSet<ushort>();
        var physical = new HashSet<LegacyNativeMsgSpell>(ReferenceEqualityComparer.Instance);
        foreach (var pair in map)
        {
            if (rows.Count >= MaximumPhysicalSpells || pair.Value is not { } row || pair.Key != row.ID ||
                !ids.Add(pair.Key) || !physical.Add(row)) throw UnavailableNinjaSpells();
            rows.Add(new(pair.Key, row, ReadSpellValues(row)));
        }
        return rows.ToArray();
    }

    private static void RequireNinjaSpellRows(ConcurrentDictionary<ushort, LegacyNativeMsgSpell> map, SpellRow[] rows)
    {
        if (map is null || map.Count != rows.Length) throw UnavailableNinjaSpells();
        var ordinal = 0;
        foreach (var pair in map)
        {
            if (ordinal >= rows.Length) throw UnavailableNinjaSpells();
            var saved = rows[ordinal++];
            if (pair.Key != saved.Key || !ReferenceEquals(pair.Value, saved.Row) ||
                ReadSpellValues(pair.Value) != saved.Values) throw UnavailableNinjaSpells();
        }
        if (ordinal != rows.Length) throw UnavailableNinjaSpells();
    }
}

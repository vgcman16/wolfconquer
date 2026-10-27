using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSpellStorage
{
    private static bool CheckSpellLookup(ConcurrentDictionary<ushort, LegacyNativeMsgSpell> map,
        Dictionary<ushort, LegacyNativeMsgSpell> expected, ushort id)
    {
        var raw = expected.TryGetValue(id, out var row);
        var present = map.TryGetValue(id, out var found);
        if (present != raw || present && !ReferenceEquals(found, row)) throw UnavailableNinjaSpells();
        return raw;
    }

    private static void RequireExpectedSpellMap(ConcurrentDictionary<ushort, LegacyNativeMsgSpell> map,
        Dictionary<ushort, LegacyNativeMsgSpell> expected)
    {
        if (map.Count != expected.Count) throw UnavailableNinjaSpells();
        foreach (var pair in map)
            if (!expected.TryGetValue(pair.Key, out var row) || !ReferenceEquals(pair.Value, row))
                throw UnavailableNinjaSpells();
    }

    private static void RemoveExpectedSpell(ConcurrentDictionary<ushort, LegacyNativeMsgSpell> map,
        Dictionary<ushort, LegacyNativeMsgSpell> expected, ushort id)
    {
        if (!expected.TryGetValue(id, out var row) || !map.TryRemove(id, out var removed) ||
            !ReferenceEquals(removed, row)) throw UnavailableNinjaSpells();
        expected.Remove(id);
    }
}

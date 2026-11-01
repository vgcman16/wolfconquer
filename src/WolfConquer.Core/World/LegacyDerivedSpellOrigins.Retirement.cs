using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyDerivedSpellOrigins
{
    // Historical v8 DATA rule, not permission for a live Clear/class reset.
    // Only an actual separate producer may retire an old claim and its row.
    internal static void RequirePreservedOrRemoved(CharacterSnapshot beforeCharacter, byte[]? beforeSpells,
        CharacterSnapshot afterCharacter, byte[] afterSpells)
    {
        var before = Read(beforeCharacter); var after = Read(afterCharacter);
        foreach (var pair in after)
            if (!before.TryGetValue(pair.Key, out var level) || level != pair.Value) throw Invalid();
        RequireSavedRows(beforeCharacter, beforeSpells);
        RequireSavedRows(afterCharacter, afterSpells);
        if (before.Count == 0) return; // No new unrelated row restrictions on old v8 DATA.
        var rows = PlayerSpellSnapshotLoader.Parse(afterSpells).Spells;
        foreach (var id in before.Keys)
            if (!after.ContainsKey(id) && rows.Any(row => row.Id == id)) throw Invalid();
    }
}

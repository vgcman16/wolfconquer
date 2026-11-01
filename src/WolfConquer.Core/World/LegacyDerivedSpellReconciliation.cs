using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyDerivedSpellDesiredRow(ushort Id, PlayerSpellSnapshotEntry? After);

// Pure DATA. The actual query/death producer must derive desired rows and own
// the real two-file transaction; a result cannot commit or adopt an old row.
internal static class LegacyDerivedSpellReconciliation
{
    internal sealed class Model
    {
        internal CharacterSnapshot Character { get; }
        internal PlayerSpellSnapshot Spells { get; }
        internal IReadOnlyList<LegacyDerivedSpellDesiredRow> Changes { get; }
        private Model(CharacterSnapshot character, PlayerSpellSnapshot spells, LegacyDerivedSpellDesiredRow[] changes)
        { Character = LegacyDerivedSpellOrigins.CopyCharacter(character); Spells = LegacyDerivedSpellOrigins.Freeze(spells);
            Changes = Array.AsReadOnly(changes.ToArray()); }
        internal static Model Create(CharacterSnapshot character, PlayerSpellSnapshot spells, LegacyDerivedSpellDesiredRow[] changes) =>
            new(character, spells, changes);
    }

    internal sealed class Physical
    {
        private readonly byte[] character, spells;
        internal Model Reconciled { get; }
        private Physical(Model model, byte[] character, byte[] spells)
        { Reconciled = model; this.character = character.ToArray(); this.spells = spells.ToArray(); }
        internal byte[] CopyCharacterAfter() => character.ToArray();
        internal byte[] CopySpellsAfter() => spells.ToArray();
        internal static Physical Create(Model model, byte[] character, byte[] spells) => new(model, character, spells);
    }

    internal static Model Project(CharacterSnapshot characterBefore, PlayerSpellSnapshot spellsBefore,
        IReadOnlyList<LegacyDerivedSpellDesiredRow> desired)
    {
        var character = LegacyDerivedSpellOrigins.CopyCharacter(characterBefore);
        var rows = LegacyDerivedSpellOrigins.Freeze(spellsBefore).Spells.ToList();
        var origins = LegacyDerivedSpellOrigins.Read(character).ToDictionary(pair => pair.Key, pair => pair.Value);
        LegacyDerivedSpellOrigins.RequireRows(character, new(rows));
        var changes = new List<LegacyDerivedSpellDesiredRow>();
        foreach (var action in CopyDesired(desired))
        {
            int index = rows.FindIndex(row => row.Id == action.Id);
            if (index >= 0 && !origins.ContainsKey(action.Id)) continue; // Never adopt an existing unmarked row.
            if (action.After is { } grant)
            {
                if (index < 0) rows.Add(grant); else rows[index] = grant;
                origins[action.Id] = grant.Level; changes.Add(action);
            }
            else if (index >= 0)
            { rows.RemoveAt(index); origins.Remove(action.Id); changes.Add(action); }
        }
        character = LegacyDerivedSpellOrigins.WithOrigins(character, origins);
        LegacyDerivedSpellOrigins.RequireRows(character, new(rows));
        return Model.Create(character, new(rows), changes.ToArray());
    }

    internal static Physical Project(byte[] characterBefore, byte[]? spellsBefore,
        IReadOnlyList<LegacyDerivedSpellDesiredRow> desired)
    {
        if (characterBefore is null || characterBefore.Length is 0 or > CharacterSnapshotFileBaseline.MaximumBytes ||
            spellsBefore is { Length: > CharacterSnapshotFileBaseline.MaximumBytes }) throw LegacyDerivedSpellOrigins.Invalid();
        var beforeCharacter = characterBefore.ToArray(); var beforeSpells = spellsBefore?.ToArray();
        var character = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(beforeCharacter, out _));
        LegacyDerivedSpellOrigins.RequireSavedRows(character, beforeSpells);
        var original = beforeSpells is null ? PlayerSpellSnapshot.Empty : PlayerSpellSnapshotLoader.Parse(beforeSpells);
        var model = Project(character, original, desired);
        var afterCharacter = LegacyDerivedSpellOrigins.ProjectCharacterImage(beforeCharacter, model.Character);
        var afterSpells = PlayerSpellSnapshotLoader.ToLegacyBytes(model.Spells);
        var offsets = original.Spells.Select((row, index) => (row.Id, Offset: sizeof(int) + index * PlayerSpellSnapshotLoader.LegacySpellSize))
            .ToDictionary(pair => pair.Id, pair => pair.Offset);
        if (beforeSpells is not null)
            for (int index = 0; index < model.Spells.Spells.Count; index++)
                if (offsets.TryGetValue(model.Spells.Spells[index].Id, out var offset))
                    afterSpells[sizeof(int) + index * PlayerSpellSnapshotLoader.LegacySpellSize + 11] = beforeSpells[offset + 11];
        LegacyDerivedSpellOrigins.RequireSavedRows(model.Character, afterSpells);
        return Physical.Create(model, afterCharacter, afterSpells);
    }

    private static LegacyDerivedSpellDesiredRow[] CopyDesired(IReadOnlyList<LegacyDerivedSpellDesiredRow> desired)
    {
        if (desired is null || desired.GetType() != typeof(LegacyDerivedSpellDesiredRow[]) &&
            desired.GetType() != typeof(List<LegacyDerivedSpellDesiredRow>) || desired.Count > 2) throw LegacyDerivedSpellOrigins.Invalid();
        var rows = desired.ToArray(); int previous = -1;
        foreach (var action in rows)
        {
            if (action is null || !LegacyDerivedSpellOrigins.IsDomain(action.Id)) throw LegacyDerivedSpellOrigins.Invalid();
            int order = action.Id == 21_930 ? 0 : 1;
            if (order <= previous || action.After is { } grant &&
                (grant.Level is 0 or > byte.MaxValue || grant != LegacyDerivedSpellOrigins.Granted(action.Id, grant.Level)))
                throw LegacyDerivedSpellOrigins.Invalid();
            previous = order;
        }
        return rows;
    }
}

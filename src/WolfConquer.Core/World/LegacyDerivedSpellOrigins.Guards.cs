using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyDerivedSpellOrigins
{
    // Ordinary writers may preserve a claim. Only the actual derived writer's
    // separate capability may change it; this DATA guard grants no exception.
    internal static void RequireUnchanged(CharacterSnapshot expectedCharacter, PlayerSpellSnapshot expectedSpells,
        CharacterSnapshot proposedCharacter, PlayerSpellSnapshot proposedSpells)
    {
        var before = Read(expectedCharacter); var after = Read(proposedCharacter);
        var beforeRows = Freeze(expectedSpells).Spells; var afterRows = Freeze(proposedSpells).Spells;
        RequireRows(before, beforeRows); RequireRows(after, afterRows);
        if (Encoded(expectedCharacter) != Encoded(proposedCharacter) || before.Count != after.Count ||
            before.Any(pair => !after.TryGetValue(pair.Key, out var level) || level != pair.Value)) throw Invalid();
    }

    internal static void RequireRows(CharacterSnapshot character, PlayerSpellSnapshot spells) =>
        RequireRows(Read(character), Freeze(spells).Spells);
}

using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static class LegacyPlayerSpellClassGateSanitizer
{
    public static bool TrySanitize(
        CharacterSnapshot character,
        PlayerSpellSnapshot playerSpells,
        IReadOnlyDictionary<int, IReadOnlyList<MagicTypeOperationEntry>>? operationsBySpellId,
        out PlayerSpellSnapshot sanitized,
        out IReadOnlyList<ushort> removedSpellIds)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(playerSpells);

        sanitized = playerSpells;
        removedSpellIds = [];
        if (operationsBySpellId is null || playerSpells.Spells.Count == 0)
        {
            return false;
        }

        var kept = new List<PlayerSpellSnapshotEntry>(playerSpells.Spells.Count);
        List<ushort>? removed = null;
        foreach (var spell in playerSpells.Spells)
        {
            if (LegacyMagicClassGate.CanUsePlayerSpell(
                    character,
                    spell.Id,
                    operationsBySpellId,
                    out _))
            {
                kept.Add(spell);
                continue;
            }

            removed ??= [];
            removed.Add(spell.Id);
        }

        if (removed is null)
        {
            return false;
        }

        sanitized = new PlayerSpellSnapshot(kept);
        removedSpellIds = removed;
        return true;
    }
}

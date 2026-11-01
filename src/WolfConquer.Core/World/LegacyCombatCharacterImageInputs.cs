using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// A disk/runtime comparison only, never an award or write capability. Combat
// writers retain their real owner, locks, captured bytes and commit receipts.
internal static class LegacyCombatCharacterImageInputs
{
    internal static bool Matches(CharacterSnapshot disk, CharacterSnapshot expected)
    {
        ArgumentNullException.ThrowIfNull(disk);
        ArgumentNullException.ThrowIfNull(expected);
        if (disk.Uid != expected.Uid ||
            CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(disk, expected) ||
            !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(disk, expected)) return false;
        foreach (var pair in disk.Attributes)
        {
            // These three fields are live status/resource inputs, not fields
            // owned by CharacterSnapshotScalarPersistence. Actual skill/ticker
            // publications can change them without changing the saved seed.
            // The writer preserves their original raw lines, not live values.
            if (pair.Key.Equals("StatusFlags", StringComparison.OrdinalIgnoreCase) ||
                pair.Key.Equals("XPCount", StringComparison.OrdinalIgnoreCase) ||
                pair.Key.Equals("Stamina", StringComparison.OrdinalIgnoreCase)) continue;
            if (!expected.Attributes.TryGetValue(pair.Key, out var value) || value != pair.Value) return false;
        }
        return true;
    }
}

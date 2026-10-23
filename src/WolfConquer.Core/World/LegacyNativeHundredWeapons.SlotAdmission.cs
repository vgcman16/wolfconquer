using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHundredWeapons
{
    // Serializes these slot/query operations only. Public Objects/row/bitmap writers,
    // loading and Add/RemoveWeapon have not joined this monitor: no global transaction claim.
    private readonly object slotSyncRoot = new();

    private static bool IsSlotPosition(int position) => position is >= 0 and < 32;

    private bool TrySnapshotSlotStorage(bool requireBitmap, out LegacyNativeHundredWeaponInfo[] rows)
    {
        rows = [];
        var source = Objects;
        if (source is null) return false;
        KeyValuePair<LegacyNativeMagicType.WeaponsType, LegacyNativeHundredWeaponInfo>[] entries;
        try { entries = source.ToArray(); }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        var result = new LegacyNativeHundredWeaponInfo[entries.Length];
        var aliases = new HashSet<LegacyNativeHundredWeaponInfo>(ReferenceEqualityComparer.Instance);
        var vectors = new HashSet<LegacyNativeBitVector32>(ReferenceEqualityComparer.Instance);
        var words = new HashSet<uint[]>(ReferenceEqualityComparer.Instance);
        var appearances = new HashSet<byte>();
        uint occupied = 0;
        for (int index = 0; index < entries.Length; index++)
        {
            var pair = entries[index];
            var item = pair.Value;
            if (item is null || pair.Key != item.WeaponSubtype ||
                !ValidWeapon(item.WeaponSubtype, item.Level) || !aliases.Add(item) ||
                (item.AppearancePosition != 0 && !appearances.Add(item.AppearancePosition))) return false;
            if (requireBitmap)
            {
                // Both genuine Load and AddWeapon create exactly one 32-bit word.
                // Do not call the native off-by-one Contain reader on unknown storage.
                var vector = item.BitVector;
                var bits = vector?.bits;
                if (vector is null || bits is null || bits.Length != 1 ||
                    !vectors.Add(vector) || !words.Add(bits) || (occupied & bits[0]) != 0) return false;
                occupied |= bits[0];
            }
            result[index] = item;
        }
        if (!ReferenceEquals(source, Objects)) return false;
        rows = result;
        return true;
    }

    private bool TryGetSlotMutationPlayer(out LegacyNativePlayerRuntime player)
    {
        player = (Owner?.Player)!;
        return player is not null && ReferenceEquals(player.Owner, Owner) &&
            ReferenceEquals(Owner.HundredWeapons, this);
    }

    private bool IsCurrentSlotMutationPlayer(LegacyNativePlayerRuntime player, uint uid) =>
        ReferenceEquals(Owner.Player, player) && ReferenceEquals(player.Owner, Owner) &&
        ReferenceEquals(Owner.HundredWeapons, this) && player.UID == uid;
}

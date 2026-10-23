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
        var admission = new SlotStorageAdmission(this);
        for (int index = 0; index < entries.Length; index++)
        {
            var pair = entries[index];
            if (!admission.TryAdmitPhysicalIdentity(pair, out var item) ||
                (requireBitmap && !admission.TryAdmitBitmap(item))) return false;
            result[index] = item;
        }
        if (!ReferenceEquals(source, Objects)) return false;
        rows = result;
        return true;
    }

    private sealed class SlotStorageAdmission
    {
        private readonly LegacyNativeHundredWeapons owner;
        private readonly HashSet<LegacyNativeHundredWeaponInfo> aliases = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<LegacyNativeBitVector32> vectors = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<uint[]> words = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<byte> appearances = new();
        private uint occupied;

        internal SlotStorageAdmission(LegacyNativeHundredWeapons owner) => this.owner = owner;

        internal bool TryAdmitPhysicalIdentity(
            KeyValuePair<LegacyNativeMagicType.WeaponsType, LegacyNativeHundredWeaponInfo> pair,
            out LegacyNativeHundredWeaponInfo item)
        {
            item = pair.Value;
            return item is not null && pair.Key == item.WeaponSubtype &&
                owner.ValidWeapon(item.WeaponSubtype, item.Level) && aliases.Add(item) &&
                (item.AppearancePosition == 0 || appearances.Add(item.AppearancePosition));
        }

        internal bool TryAdmitBitmap(LegacyNativeHundredWeaponInfo item)
        {
            // Both genuine Load and AddWeapon create exactly one 32-bit word.
            // Do not call the native off-by-one Contain reader on unknown storage.
            var vector = item.BitVector;
            var bits = vector?.bits;
            if (vector is null || bits is null || bits.Length != 1 ||
                !vectors.Add(vector) || !words.Add(bits) || (occupied & bits[0]) != 0) return false;
            occupied |= bits[0];
            return true;
        }
    }

    private bool TryAdmitSlotMutation(int position, out LegacyNativePlayerRuntime player,
        out LegacyNativeHundredWeaponInfo[] rows)
    {
        player = null!;
        rows = [];
        return IsSlotPosition(position) && TryGetSlotMutationPlayer(out player) &&
            TrySnapshotSlotStorage(requireBitmap: true, out rows);
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

using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHundredWeapons
{
    public bool checkDuplication(LegacyNativeMagicType.WeaponsType WeaponSubType, int Position,
        out int duplicatedPosition)
    {
        lock (slotSyncRoot)
        {
            duplicatedPosition = 0;
            if (!TrySnapshotSlotStorage(requireBitmap: true, out var rows)) return false;
            var band = Position switch
            {
                >= 2 and <= 4 => (First: 2, Last: 4),
                >= 5 and <= 9 => (First: 5, Last: 9),
                >= 10 and <= 16 => (First: 10, Last: 16),
                >= 17 and <= 25 => (First: 17, Last: 25),
                _ => (First: 0, Last: -1),
            };
            for (int slot = band.First; slot <= band.Last; slot++)
            {
                var item = rows.FirstOrDefault(row => row.BitVector.Contain(slot));
                if (item is null || item.WeaponSubtype != WeaponSubType) continue;
                duplicatedPosition = slot;
                return true;
            }
            return false;
        }
    }

    public bool checkDuplicatedAppearance(LegacyNativeMagicType.WeaponsType WeaponSubType, byte Position,
        out byte duplicatedPosition)
    {
        lock (slotSyncRoot)
        {
            duplicatedPosition = 0;
            if (!TrySnapshotSlotStorage(requireBitmap: false, out var rows)) return false;
            var item = rows.FirstOrDefault(row => row.WeaponSubtype == WeaponSubType && row.AppearancePosition > 0);
            if (item is null) return false;
            duplicatedPosition = item.AppearancePosition;
            return true;
        }
    }
}

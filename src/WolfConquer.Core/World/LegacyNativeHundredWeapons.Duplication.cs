#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHundredWeapons
{
    public bool checkDuplication(LegacyNativeMagicType.WeaponsType WeaponSubType, int Position, out int duplicatedPosition)
    {
        switch (Position)
        {
            case 2:
            case 3:
            case 4:
            {
                for (int l = 2; l <= 4; l++)
                {
                    if (TryGetItem(l) != null && TryGetItem(l).WeaponSubtype == WeaponSubType)
                    {
                        duplicatedPosition = l;
                        return true;
                    }
                }
                break;
            }
            case 5:
            case 6:
            case 7:
            case 8:
            case 9:
            {
                for (int j = 5; j <= 9; j++)
                {
                    if (TryGetItem(j) != null && TryGetItem(j).WeaponSubtype == WeaponSubType)
                    {
                        duplicatedPosition = j;
                        return true;
                    }
                }
                break;
            }
            case 10:
            case 11:
            case 12:
            case 13:
            case 14:
            case 15:
            case 16:
            {
                for (int k = 10; k <= 16; k++)
                {
                    if (TryGetItem(k) != null && TryGetItem(k).WeaponSubtype == WeaponSubType)
                    {
                        duplicatedPosition = k;
                        return true;
                    }
                }
                break;
            }
            case 17:
            case 18:
            case 19:
            case 20:
            case 21:
            case 22:
            case 23:
            case 24:
            case 25:
            {
                for (int i = 17; i <= 25; i++)
                {
                    if (TryGetItem(i) != null && TryGetItem(i).WeaponSubtype == WeaponSubType)
                    {
                        duplicatedPosition = i;
                        return true;
                    }
                }
                break;
            }
        }
        duplicatedPosition = 0;
        return false;
    }

    public bool checkDuplicatedAppearance(LegacyNativeMagicType.WeaponsType WeaponSubType, byte Position, out byte duplicatedPosition)
    {
        foreach (LegacyNativeHundredWeaponInfo value in Objects.Values)
        {
            if (value.WeaponSubtype == WeaponSubType && value.AppearancePosition > 0)
            {
                duplicatedPosition = value.AppearancePosition;
                return true;
            }
        }
        duplicatedPosition = 0;
        return false;
    }
}

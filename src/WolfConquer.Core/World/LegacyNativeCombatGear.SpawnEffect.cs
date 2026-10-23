using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCombatGear
{
    // Complete native CombatGear1039..1094; shapes differ by the requested class family.
    public uint[]? GetEffect(uint Class)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
        {
            return null;
        }
        switch (GetType(Class))
        {
        case Promotion.Warrior:
        {
            Weapon weapon2 = isEquip(Class);
            if (weapon2 != null)
            {
                return new uint[4]
                {
                    0u,
                    (uint)((int)weapon2.ItemID * 10) + weapon2.Transform,
                    weapon2.Exp,
                    0u
                };
            }
            break;
        }
        case Promotion.Archer:
        {
            Weapon weapon3 = isEquip(Class);
            if (weapon3 != null)
            {
                return new uint[3]
                {
                    0u,
                    (uint)weapon3.ItemID,
                    weapon3.Exp
                };
            }
            break;
        }
        case Promotion.Pirate:
        case Promotion.Water:
        case Promotion.Fire:
        {
            Weapon weapon = isEquip(Class);
            if (weapon != null)
            {
                return new uint[2]
                {
                    (uint)weapon.ItemID,
                    weapon.Exp
                };
            }
            break;
        }
        }
        return null;
    }
}

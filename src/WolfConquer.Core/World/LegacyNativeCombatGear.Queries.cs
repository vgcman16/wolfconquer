#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCombatGear
{
    public static Promotion GetType(uint Class)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return Promotion.None;
        return (Class / 1000) switch
        {
            2u => Promotion.Warrior,
            7u => Promotion.Pirate,
            4u => Promotion.Archer,
            13u => Promotion.Water,
            14u => Promotion.Fire,
            _ => Promotion.None,
        };
    }

    public Weapon[] GetItems(uint Class)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return null;
        switch (GetType(Class))
        {
            case Promotion.Warrior:
                return Items.Values.Where((Weapon p) => p.ItemID >= TypeID.Dragonhowl && p.ItemID <= TypeID.Redcurse).ToArray();
            case Promotion.Archer:
                return Items.Values.Where((Weapon p) => p.ItemID >= TypeID.StoneCracker && p.ItemID <= TypeID.TharnCutter).ToArray();
            case Promotion.Water:
            case Promotion.Fire:
                return Items.Values.Where((Weapon p) => p.ItemID >= TypeID.Taoist1 && p.ItemID <= TypeID.Taoist5).ToArray();
            case Promotion.Pirate:
                return Items.Values.Where((Weapon p) => p.ItemID >= TypeID.Pirate1 && p.ItemID <= TypeID.Pirate3).ToArray();
            default:
                return new Weapon[0];
        }
    }

    public Weapon isEquip(uint Class)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return null;
        return (from p in GetItems(Class)
                where p.Equip == 1
                select p).FirstOrDefault();
    }

    public bool isEquip(TypeID ID, uint Class)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return false;
        if (GetItems(Class).Length != 0)
        {
            Weapon weapon = (from p in GetItems(Class)
                             where p.ItemID == ID && p.Equip == 1
                             select p).FirstOrDefault();
            if (weapon != null)
                return true;
        }
        return false;
    }

    public bool Check(uint Class)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return false;
        return GetType(Class) == Promotion.Pirate || GetType(Class) == Promotion.Warrior ||
            GetType(Class) == Promotion.Archer || GetType(Class) == Promotion.Water || GetType(Class) == Promotion.Fire;
    }
}

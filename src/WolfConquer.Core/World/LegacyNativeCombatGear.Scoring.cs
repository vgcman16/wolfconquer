#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCombatGear
{
    // Complete native CombatGear.GetScore1231..1345; unchecked UInt32 arithmetic retained.
    public uint GetScore(uint Class, TypeID WeaponID)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
        {
            return 0u;
        }
        uint num = 0u;
        switch (GetType(Class))
        {
            case Promotion.Warrior:
                switch (WeaponID)
                {
                    case TypeID.None:
                    {
                        Weapon[] items4 = GetItems(Class);
                        foreach (Weapon weapon7 in items4)
                            num += weapon7.Exp + weapon7.Transform * 50000;
                        return num;
                    }
                    case TypeID.Dragonhowl:
                    case TypeID.Bloodlust:
                    case TypeID.Redcurse:
                    {
                        Weapon weapon6 = (from p in GetItems(Class) where p.ItemID == WeaponID select p).FirstOrDefault();
                        if (weapon6 != null)
                            num += weapon6.Exp;
                        return num;
                    }
                }
                break;
            case Promotion.Archer:
                switch (WeaponID)
                {
                    case TypeID.None:
                    {
                        Weapon[] items3 = GetItems(Class);
                        foreach (Weapon weapon5 in items3)
                            num += weapon5.Exp;
                        return num;
                    }
                    case TypeID.StoneCracker:
                    case TypeID.ColdMoon:
                    case TypeID.TharnCutter:
                    {
                        Weapon weapon4 = (from p in GetItems(Class) where p.ItemID == WeaponID select p).FirstOrDefault();
                        if (weapon4 != null)
                            num += weapon4.Exp;
                        return num;
                    }
                }
                break;
            case Promotion.Pirate:
            {
                if (WeaponID != TypeID.None)
                    break;
                Weapon[] items2 = GetItems(Class);
                foreach (Weapon weapon3 in items2)
                    num += weapon3.DBItem.Score + weapon3.MasteryScore;
                foreach (BlackJade value in BlackJadeBag.Values)
                    num += value.DBItem.score;
                num += AirPowers.SenseAipPower;
                num += AirPowers.ArmedAipPower;
                return num + AirPowers.OverLoadAipPower;
            }
            case Promotion.Water:
            case Promotion.Fire:
                switch (WeaponID)
                {
                    case TypeID.None:
                    {
                        Weapon[] items = GetItems(Class);
                        foreach (Weapon weapon2 in items)
                            num += weapon2.Exp + weapon2.MasteryScore;
                        return num;
                    }
                    case TypeID.Taoist1:
                    case TypeID.Taoist2:
                    case TypeID.Taoist3:
                    case TypeID.Taoist4:
                    case TypeID.Taoist5:
                    {
                        Weapon weapon = (from p in GetItems(Class) where p.ItemID == WeaponID select p).FirstOrDefault();
                        if (weapon != null)
                            num += weapon.Exp + weapon.MasteryScore;
                        return num;
                    }
                }
                break;
        }
        return 0u;
    }
}

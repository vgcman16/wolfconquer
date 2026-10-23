using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Whole native CombatGear711..879 required by complete MyCombatGear.Status.
// No host ranking/startup adoption. The disabled table result really is null.
internal partial class LegacyNativeCombatGear
{
    public static VRANK? GetValueRank(uint UserID, uint Class)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
        {
            return null;
        }
        VRANK vRANK = new VRANK();
        switch (GetType(Class))
        {
        case Promotion.Warrior:
        {
            uint myRank2 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Overall_Warrior, UserID);
            if (myRank2 == 1)
            {
                vRANK.LukyStrike += 2.0;
                vRANK.Parry += 3.5;
            }
            myRank2 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Dragonhowl, UserID);
            if (myRank2 == 1)
            {
                vRANK.Parry += 0.5;
            }
            myRank2 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Bloodlust, UserID);
            if (myRank2 == 1)
            {
                vRANK.Parry += 0.5;
            }
            myRank2 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Redcurse, UserID);
            if (myRank2 == 1)
            {
                vRANK.Parry += 0.5;
            }
            break;
        }
        case Promotion.Archer:
        {
            uint myRank4 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Overall_Archer, UserID);
            if (myRank4 == 1)
            {
                vRANK.LukyStrike += 2.0;
                vRANK.Parry += 3.5;
            }
            myRank4 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.WEEK_Archer, UserID);
            if (myRank4 == 1)
            {
                vRANK.PAttack = 500u;
                vRANK.PDefense = 500u;
                vRANK.MDefense = 300u;
            }
            myRank4 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.StoneCracker, UserID);
            if (myRank4 == 1)
            {
                vRANK.Parry += 0.5;
            }
            myRank4 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.ColdMoon, UserID);
            if (myRank4 == 1)
            {
                vRANK.Parry += 0.5;
            }
            myRank4 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.TharnCutter, UserID);
            if (myRank4 == 1)
            {
                vRANK.Parry += 0.5;
            }
            break;
        }
        case Promotion.Water:
        {
            uint myRank3 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Overall_Taoist, UserID);
            if (myRank3 == 1)
            {
                vRANK.Parry += 3.5;
            }
            myRank3 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.WEEK_Taoist, UserID);
            if (myRank3 == 1)
            {
                vRANK.PrestigeScore = 500u;
                vRANK.PDefense = 500u;
                vRANK.MDefense = 300u;
            }
            myRank3 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist1, UserID);
            if (myRank3 == 1)
            {
                vRANK.Parry += 0.5;
            }
            myRank3 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist2, UserID);
            if (myRank3 == 1)
            {
                vRANK.Parry += 0.5;
            }
            myRank3 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist3, UserID);
            if (myRank3 == 1)
            {
                vRANK.Parry += 0.5;
            }
            myRank3 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist4, UserID);
            if (myRank3 == 1)
            {
                vRANK.Parry += 0.5;
            }
            myRank3 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist5, UserID);
            if (myRank3 == 1)
            {
                vRANK.Parry += 0.5;
            }
            break;
        }
        case Promotion.Fire:
        {
            uint myRank5 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Overall_Taoist, UserID);
            if (myRank5 == 1)
            {
                vRANK.CoreStrike += 4.0;
            }
            myRank5 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.WEEK_Taoist, UserID);
            if (myRank5 == 1)
            {
                vRANK.PrestigeScore = 500u;
                vRANK.PDefense = 500u;
                vRANK.MDefense = 300u;
            }
            myRank5 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist1, UserID);
            if (myRank5 == 1)
            {
                vRANK.CoreStrike += 1.0;
            }
            myRank5 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist2, UserID);
            if (myRank5 == 1)
            {
                vRANK.CoreStrike += 1.0;
            }
            myRank5 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist3, UserID);
            if (myRank5 == 1)
            {
                vRANK.CoreStrike += 1.0;
            }
            myRank5 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist4, UserID);
            if (myRank5 == 1)
            {
                vRANK.CoreStrike += 1.0;
            }
            myRank5 = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist5, UserID);
            if (myRank5 == 1)
            {
                vRANK.CoreStrike += 1.0;
            }
            break;
        }
        case Promotion.Pirate:
        {
            uint myRank = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.Overall_Pirate, UserID);
            if (myRank >= 1 && myRank <= 10)
            {
                vRANK.Magic = 21930;
            }
            myRank = LegacyNativeCombatGearRankTable.GetMyRank(LegacyNativeCombatGearRankTable.Type.WEEK_Pirate, UserID);
            if (myRank >= 1 && myRank <= 10)
            {
                vRANK.Magic = 21930;
            }
            if (myRank >= 1 && myRank <= 10)
            {
                vRANK.MagicLevel = (ushort)(10 - (myRank - 1));
            }
            break;
        }
        }
        return vRANK;
    }
}

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
        var promotion = GetType(Class);
        var snapshot = LegacyNativeCombatGearRankTable.CaptureEffectRanks(
            GetEffectRankFamilies(promotion), UserID);
        VRANK vRANK = new VRANK();
        switch (promotion)
        {
        case Promotion.Warrior:
        {
            uint myRank2 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Overall_Warrior);
            if (myRank2 == 1)
            {
                vRANK.LukyStrike += 2.0;
                vRANK.Parry += 3.5;
            }
            myRank2 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Dragonhowl);
            if (myRank2 == 1)
            {
                vRANK.Parry += 0.5;
            }
            myRank2 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Bloodlust);
            if (myRank2 == 1)
            {
                vRANK.Parry += 0.5;
            }
            myRank2 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Redcurse);
            if (myRank2 == 1)
            {
                vRANK.Parry += 0.5;
            }
            break;
        }
        case Promotion.Archer:
        {
            uint myRank4 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Overall_Archer);
            if (myRank4 == 1)
            {
                vRANK.LukyStrike += 2.0;
                vRANK.Parry += 3.5;
            }
            myRank4 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.WEEK_Archer);
            if (myRank4 == 1)
            {
                vRANK.PAttack = 500u;
                vRANK.PDefense = 500u;
                vRANK.MDefense = 300u;
            }
            myRank4 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.StoneCracker);
            if (myRank4 == 1)
            {
                vRANK.Parry += 0.5;
            }
            myRank4 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.ColdMoon);
            if (myRank4 == 1)
            {
                vRANK.Parry += 0.5;
            }
            myRank4 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.TharnCutter);
            if (myRank4 == 1)
            {
                vRANK.Parry += 0.5;
            }
            break;
        }
        case Promotion.Water:
        {
            uint myRank3 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Overall_Taoist);
            if (myRank3 == 1)
            {
                vRANK.Parry += 3.5;
            }
            myRank3 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.WEEK_Taoist);
            if (myRank3 == 1)
            {
                vRANK.PrestigeScore = 500u;
                vRANK.PDefense = 500u;
                vRANK.MDefense = 300u;
            }
            myRank3 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist1);
            if (myRank3 == 1)
            {
                vRANK.Parry += 0.5;
            }
            myRank3 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist2);
            if (myRank3 == 1)
            {
                vRANK.Parry += 0.5;
            }
            myRank3 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist3);
            if (myRank3 == 1)
            {
                vRANK.Parry += 0.5;
            }
            myRank3 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist4);
            if (myRank3 == 1)
            {
                vRANK.Parry += 0.5;
            }
            myRank3 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist5);
            if (myRank3 == 1)
            {
                vRANK.Parry += 0.5;
            }
            break;
        }
        case Promotion.Fire:
        {
            uint myRank5 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Overall_Taoist);
            if (myRank5 == 1)
            {
                vRANK.CoreStrike += 4.0;
            }
            myRank5 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.WEEK_Taoist);
            if (myRank5 == 1)
            {
                vRANK.PrestigeScore = 500u;
                vRANK.PDefense = 500u;
                vRANK.MDefense = 300u;
            }
            myRank5 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist1);
            if (myRank5 == 1)
            {
                vRANK.CoreStrike += 1.0;
            }
            myRank5 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist2);
            if (myRank5 == 1)
            {
                vRANK.CoreStrike += 1.0;
            }
            myRank5 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist3);
            if (myRank5 == 1)
            {
                vRANK.CoreStrike += 1.0;
            }
            myRank5 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist4);
            if (myRank5 == 1)
            {
                vRANK.CoreStrike += 1.0;
            }
            myRank5 = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Taoist5);
            if (myRank5 == 1)
            {
                vRANK.CoreStrike += 1.0;
            }
            break;
        }
        case Promotion.Pirate:
        {
            uint myRank = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.Overall_Pirate);
            if (myRank >= 1 && myRank <= 10)
            {
                vRANK.Magic = 21930;
            }
            myRank = snapshot.GetMyRank(LegacyNativeCombatGearRankTable.Type.WEEK_Pirate);
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

    private static LegacyNativeCombatGearRankTable.Type[] GetEffectRankFamilies(Promotion promotion) =>
        promotion switch
        {
            Promotion.Warrior =>
            [
                LegacyNativeCombatGearRankTable.Type.Overall_Warrior,
                LegacyNativeCombatGearRankTable.Type.Dragonhowl,
                LegacyNativeCombatGearRankTable.Type.Bloodlust,
                LegacyNativeCombatGearRankTable.Type.Redcurse,
            ],
            Promotion.Archer =>
            [
                LegacyNativeCombatGearRankTable.Type.Overall_Archer,
                LegacyNativeCombatGearRankTable.Type.WEEK_Archer,
                LegacyNativeCombatGearRankTable.Type.StoneCracker,
                LegacyNativeCombatGearRankTable.Type.ColdMoon,
                LegacyNativeCombatGearRankTable.Type.TharnCutter,
            ],
            Promotion.Water or Promotion.Fire =>
            [
                LegacyNativeCombatGearRankTable.Type.Overall_Taoist,
                LegacyNativeCombatGearRankTable.Type.WEEK_Taoist,
                LegacyNativeCombatGearRankTable.Type.Taoist1,
                LegacyNativeCombatGearRankTable.Type.Taoist2,
                LegacyNativeCombatGearRankTable.Type.Taoist3,
                LegacyNativeCombatGearRankTable.Type.Taoist4,
                LegacyNativeCombatGearRankTable.Type.Taoist5,
            ],
            Promotion.Pirate =>
            [
                LegacyNativeCombatGearRankTable.Type.Overall_Pirate,
                LegacyNativeCombatGearRankTable.Type.WEEK_Pirate,
            ],
            _ => [],
        };

}

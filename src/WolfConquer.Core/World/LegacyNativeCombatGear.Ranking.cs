using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCombatGear
{
    // Native1445..1505. No host ranking publication or file loading is attached here.
    public void UpdateRank(LegacyNativeCombatGearRankTable.Entry entry)
    {
        if (LegacyNativeCombatGearTable.m_Load)
        {
            switch (GetType(entry.Class))
            {
                case Promotion.Warrior:
                    AddRank(entry, TypeID.None, LegacyNativeCombatGearRankTable.Type.Overall_Warrior);
                    AddRank(entry, TypeID.Dragonhowl, LegacyNativeCombatGearRankTable.Type.Dragonhowl);
                    AddRank(entry, TypeID.Bloodlust, LegacyNativeCombatGearRankTable.Type.Bloodlust);
                    AddRank(entry, TypeID.Redcurse, LegacyNativeCombatGearRankTable.Type.Redcurse);
                    break;
                case Promotion.Archer:
                    AddRank(entry, TypeID.None, LegacyNativeCombatGearRankTable.Type.Overall_Archer);
                    AddRank(entry, TypeID.None, LegacyNativeCombatGearRankTable.Type.WEEK_Archer);
                    AddRank(entry, TypeID.StoneCracker, LegacyNativeCombatGearRankTable.Type.StoneCracker);
                    AddRank(entry, TypeID.ColdMoon, LegacyNativeCombatGearRankTable.Type.ColdMoon);
                    AddRank(entry, TypeID.TharnCutter, LegacyNativeCombatGearRankTable.Type.TharnCutter);
                    break;
                case Promotion.Water:
                case Promotion.Fire:
                    AddRank(entry, TypeID.None, LegacyNativeCombatGearRankTable.Type.Overall_Taoist);
                    AddRank(entry, TypeID.None, LegacyNativeCombatGearRankTable.Type.WEEK_Taoist);
                    AddRank(entry, TypeID.Taoist1, LegacyNativeCombatGearRankTable.Type.Taoist1);
                    AddRank(entry, TypeID.Taoist2, LegacyNativeCombatGearRankTable.Type.Taoist2);
                    AddRank(entry, TypeID.Taoist3, LegacyNativeCombatGearRankTable.Type.Taoist3);
                    AddRank(entry, TypeID.Taoist4, LegacyNativeCombatGearRankTable.Type.Taoist4);
                    AddRank(entry, TypeID.Taoist5, LegacyNativeCombatGearRankTable.Type.Taoist5);
                    break;
                case Promotion.Pirate:
                    AddRank(entry, TypeID.None, LegacyNativeCombatGearRankTable.Type.WEEK_Pirate);
                    AddRank(entry, TypeID.None, LegacyNativeCombatGearRankTable.Type.Overall_Pirate);
                    break;
            }
            if (GetType(entry.Class) != Promotion.Warrior)
                LegacyNativeCombatGearRankTable.RemoveWarrior(entry.UID);
            if (GetType(entry.Class) != Promotion.Archer)
                LegacyNativeCombatGearRankTable.RemoveArcher(entry.UID);
            if (GetType(entry.Class) != Promotion.Water && GetType(entry.Class) != Promotion.Fire)
                LegacyNativeCombatGearRankTable.RemoveTaoist(entry.UID);
            if (GetType(entry.Class) != Promotion.Pirate)
                LegacyNativeCombatGearRankTable.RemovePirate(entry.UID);
        }
    }

    public void AddRank(LegacyNativeCombatGearRankTable.Entry entry, TypeID typeID,
        LegacyNativeCombatGearRankTable.Type type)
    {
        LegacyNativeCombatGearRankTable.Entry entry2 = entry.ShallowCopy();
        uint score = GetScore(entry.Class, typeID);
        entry2.Type = type;
        entry2.TotalPoints = score;
        LegacyNativeCombatGearRankTable.Ranks[entry2.Type].UpdateItem(entry2);
    }
}

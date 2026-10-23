using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeInnerPowerRuntime
{
    // Complete native InnerPower205..303. Integer division precedes multiplication.
    public void UpdateStatus()
    {
        Defence = 0u;
        CriticalStrike = 0u;
        SkillCriticalStrike = 0u;
        Immunity = 0u;
        Breakthrough = 0u;
        Counteraction = 0u;
        MaxLife = 0u;
        AddAttack = 0u;
        AddMagicAttack = 0u;
        AddMagicDefense = 0u;
        FinalAttack = 0u;
        FinalDefense = 0u;
        FinalMagicAttack = 0u;
        FinalMagicDefense = 0u;
        Stage[] stages = Stages;
        foreach (Stage stage in stages)
        {
            if (!stage.UnLocked)
            {
                break;
            }
            LegacyNativeInnerPowerTable.Stage stage2 = LegacyNativeInnerPowerTable.Stages[stage.ID - 1];
            for (int j = 0; j < stage.NeiGongs.Length; j++)
            {
                Stage.NeiGong neiGong = stage.NeiGongs[j];
                if (!neiGong.Unlocked || neiGong.level == 0)
                {
                    break;
                }
                LegacyNativeInnerPowerTable.Stage.NeiGong neiGong2 = stage2.NeiGongAtributes[j];
                for (int k = 0; k < neiGong2.AtributesType.Length; k++)
                {
                    LegacyNativeInnerPowerTable.AtributeType atributType = neiGong2.AtributesType[k];
                    uint num = neiGong2.AtributesValues[k];
                    AddAtributes(atributType, num / neiGong2.MaxLevel * neiGong.level);
                }
            }
            if (stage.Complete)
            {
                for (int l = 0; l < stage2.SpecialAtributesType.Length; l++)
                {
                    LegacyNativeInnerPowerTable.AtributeType atributType2 = stage2.SpecialAtributesType[l];
                    uint atributValue = stage2.AtributesValues[l];
                    AddAtributes(atributType2, atributValue);
                }
            }
        }
    }

    public void AddAtributes(LegacyNativeInnerPowerTable.AtributeType AtributType, uint AtributValue)
    {
        switch (AtributType)
        {
            case LegacyNativeInnerPowerTable.AtributeType.Break: Breakthrough += AtributValue; break;
            case LegacyNativeInnerPowerTable.AtributeType.FinalMAttack: FinalMagicAttack += AtributValue; break;
            case LegacyNativeInnerPowerTable.AtributeType.FinalPAttack: FinalAttack += AtributValue; break;
            case LegacyNativeInnerPowerTable.AtributeType.FinalMDamage: FinalMagicDefense += AtributValue; break;
            case LegacyNativeInnerPowerTable.AtributeType.FinalPDamage: FinalDefense += AtributValue; break;
            case LegacyNativeInnerPowerTable.AtributeType.Immunity: Immunity += AtributValue; break;
            case LegacyNativeInnerPowerTable.AtributeType.MAttack: AddMagicAttack += AtributValue; break;
            case LegacyNativeInnerPowerTable.AtributeType.MaxHP: MaxLife += AtributValue; break;
            case LegacyNativeInnerPowerTable.AtributeType.MDefense: AddMagicDefense += AtributValue; break;
            case LegacyNativeInnerPowerTable.AtributeType.MStrike: SkillCriticalStrike += AtributValue; break;
            case LegacyNativeInnerPowerTable.AtributeType.PAttack: AddAttack += AtributValue; break;
            case LegacyNativeInnerPowerTable.AtributeType.PDefense: Defence += AtributValue; break;
            case LegacyNativeInnerPowerTable.AtributeType.PStrike: CriticalStrike += AtributValue; break;
            case LegacyNativeInnerPowerTable.AtributeType.Conteraction: Counteraction += AtributValue; break;
        }
    }
}

using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Whole native InnerPowerTable10..90 row declarations, state and lookup.
internal static partial class LegacyNativeInnerPowerTable
{
    public enum AtributeType : byte
    {
        MaxHP = 1, PAttack, MAttack, PDefense, MDefense, FinalPAttack, FinalMAttack,
        FinalPDamage, FinalMDamage, PStrike, MStrike, Immunity, Break, Conteraction,
    }

    public class Stage
    {
        public class NeiGong
        {
            public byte ID;
            public byte MaxLevel;
            public AtributeType[] AtributesType = null!;
            public uint[] AtributesValues = null!;
            public uint[] ProgressNeiGongValue = null!;
            public uint ItemID;
            public uint ReqLev;

            public bool CheckAccount(uint reborn, uint level)
            {
                byte b = (byte)(ReqLev / 1000);
                byte b2 = (byte)(ReqLev % 1000);
                return reborn >= b && level >= b2;
            }
        }

        public ushort ID;
        public string Name = "";
        public byte NeiGongNum;
        public AtributeType[] SpecialAtributesType = null!;
        public uint[] AtributesValues = null!;
        public NeiGong[] NeiGongAtributes = null!;
    }

    public static uint Count;
    public static Stage[] Stages = null!;

    public static bool GetDBInfo(uint ID, out Stage? stage, out Stage.NeiGong? gong)
    {
        Stage[] stages = Stages;
        foreach (Stage stage2 in stages)
        {
            Stage.NeiGong[] neiGongAtributes = stage2.NeiGongAtributes;
            foreach (Stage.NeiGong neiGong in neiGongAtributes)
            {
                if (neiGong.ID == ID)
                {
                    stage = stage2;
                    gong = neiGong;
                    return true;
                }
            }
        }
        stage = null;
        gong = null;
        return false;
    }
}

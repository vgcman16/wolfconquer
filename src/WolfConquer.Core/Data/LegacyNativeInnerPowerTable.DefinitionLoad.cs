using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeInnerPowerTable
{
    // Complete native InnerPowerTable92..151. Uses the existing genuine Win32 IniFile owner.
    public static void LoadDBInformation()
    {
        LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("NeiGongInfo.ini");
        Count = iniFile.ReadUInt32("NeiGong", "Num");
        Stages = new Stage[Count];
        for (int i = 1; i <= Count; i++)
        {
            Stage stage = new Stage();
            stage.ID = iniFile.ReadUInt16(i.ToString(), "id", 0);
            stage.Name = iniFile.ReadString(i.ToString(), "Name", "");
            stage.NeiGongNum = iniFile.ReadByte(i.ToString(), "NeiGongNum", 0);
            string text = iniFile.ReadString(i.ToString(), "AttriType", "");
            string[] array = text.Split('-');
            stage.SpecialAtributesType = new AtributeType[stage.NeiGongNum];
            for (int j = 0; j < array.Length; j++)
            {
                stage.SpecialAtributesType[j] = (AtributeType)byte.Parse(array[j]);
            }
            string text2 = iniFile.ReadString(i.ToString(), "AttriValue", "");
            stage.AtributesValues = new uint[stage.NeiGongNum];
            string[] array2 = text2.Split('-');
            for (int k = 0; k < array2.Length; k++)
            {
                stage.AtributesValues[k] = uint.Parse(array2[k]);
            }
            stage.NeiGongAtributes = new Stage.NeiGong[stage.NeiGongNum];
            for (int l = 1; l <= stage.NeiGongNum; l++)
            {
                string section = i + "-" + l;
                Stage.NeiGong neiGong = new Stage.NeiGong();
                neiGong.ID = iniFile.ReadByte(section, "Type", 0);
                neiGong.MaxLevel = iniFile.ReadByte(section, "MaxLev", 0);
                string text3 = iniFile.ReadString(section, "AttriType", "");
                string[] array3 = text3.Split('-');
                neiGong.AtributesType = new AtributeType[array3.Length];
                for (int m = 0; m < array3.Length; m++)
                {
                    neiGong.AtributesType[m] = (AtributeType)byte.Parse(array3[m]);
                }
                string text4 = iniFile.ReadString(section, "AttriValue", "");
                string[] array4 = text4.Split('-');
                neiGong.AtributesValues = new uint[array4.Length];
                for (int n = 0; n < array4.Length; n++)
                {
                    neiGong.AtributesValues[n] = uint.Parse(array4[n]);
                }
                string text5 = iniFile.ReadString(section, "NeiGongValue", "");
                string[] array5 = text5.Split('-');
                neiGong.ProgressNeiGongValue = new uint[array5.Length];
                for (int num = 0; num < array5.Length; num++)
                {
                    neiGong.ProgressNeiGongValue[num] = uint.Parse(array5[num]);
                }
                neiGong.ReqLev = iniFile.ReadUInt32(section, "ReqLev");
                neiGong.ItemID = iniFile.ReadUInt32(section, "ReqItemType");
                stage.NeiGongAtributes[l - 1] = neiGong;
            }
            Stages[i - 1] = stage;
        }
    }
}

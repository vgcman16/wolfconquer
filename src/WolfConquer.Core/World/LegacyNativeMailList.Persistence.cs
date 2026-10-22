#nullable disable
using System.IO;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMailList
{
    public unsafe static void SaveMailList(LegacyNativeGameClientRuntime client)
    {
        LegacyNativeBinaryFile binaryFile = new LegacyNativeBinaryFile();
        if (!binaryFile.Open(LegacyNativeServerConfig.DbLocation + "\\PlayersMailList\\" + client.Player.UID + ".bin", FileMode.Create))
        {
            return;
        }
        LegacyNativeTMailList tMailList = default(LegacyNativeTMailList);
        int count = client.MailList.Values.Count;
        binaryFile.Write(&count, 4);
        foreach (LegacyNativeMailList value in client.MailList.Values)
        {
            tMailList.GetMailList(value);
            binaryFile.Write(&tMailList, sizeof(LegacyNativeTMailList));
        }
        binaryFile.Close();
    }

    public unsafe static void LoadMailList(LegacyNativeGameClientRuntime client)
    {
        LegacyNativeBinaryFile binaryFile = new LegacyNativeBinaryFile();
        if (!binaryFile.Open(LegacyNativeServerConfig.DbLocation + "\\PlayersMailList\\" + client.Player.UID + ".bin", FileMode.Open))
        {
            return;
        }
        int num = default(int);
        binaryFile.Read(&num, 4);
        LegacyNativeTMailList tMailList = default(LegacyNativeTMailList);
        for (int i = 0; i < num; i++)
        {
            binaryFile.Read(&tMailList, sizeof(LegacyNativeTMailList));
            LegacyNativeMailList mailList = tMailList.GetMailList();
            if (mailList != null)
            {
                client.MailList.Add(mailList.id, mailList);
            }
        }
        binaryFile.Close();
    }
}

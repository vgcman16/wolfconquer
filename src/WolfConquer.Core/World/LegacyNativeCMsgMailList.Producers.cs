#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCMsgMailList
{
    public static void Loading(LegacyNativeGameClientRuntime client)
    {
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        List<LegacyNativeMailList> list = client.MailList.Values.ToList();
        if (list.Count == 0)
        {
            return;
        }
        LegacyNativeCMsgMailList cMsgMailList = new LegacyNativeCMsgMailList();
        cMsgMailList.Type = 0;
        cMsgMailList.Count = 1;
        int num = list.Count;
        for (int i = 0; i < list.Count / 10 + 1; i++)
        {
            cMsgMailList.MailList = new Mail[(num > 10) ? 10 : num];
            for (int j = 0; j < cMsgMailList.MailList.Length; j++)
            {
                if (num <= 0)
                {
                    break;
                }
                cMsgMailList.MailList[j] = new Mail(list[num - 1]);
                num--;
            }
            client.Send(stream.CreateMailList(cMsgMailList));
        }
    }

    // Whole native Process body only: no Packet attribute or dispatcher registration.
    private static void Process(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
    {
        List<LegacyNativeMailList> list = user.MailList.Values.OrderByDescending((LegacyNativeMailList p) => p.id).ToList();
        if (list.Count == 0)
        {
            return;
        }
        LegacyNativeCMsgMailList cMsgMailList = new LegacyNativeCMsgMailList();
        cMsgMailList.Type = 0;
        cMsgMailList.Count = 1;
        int num = list.Count;
        for (int num2 = 0; num2 < list.Count / 10 + 1; num2++)
        {
            cMsgMailList.MailList = new Mail[(num > 10) ? 10 : num];
            for (int num3 = 0; num3 < cMsgMailList.MailList.Length; num3++)
            {
                if (num <= 0)
                {
                    break;
                }
                cMsgMailList.MailList[num3] = new Mail(list[num - 1]);
                num--;
            }
            user.Send(stream.CreateMailList(cMsgMailList));
        }
    }
}

#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native ElTabakh.MailList owner; persistence is in its whole-method partial.
// No full LoadCharacter/SaveClient, server lifecycle or modern mailbox adoption is installed.
internal partial class LegacyNativeMailList
{
    public string title;
    public string Sender_name;
    public string content;
    public uint id;
    public uint Expiration_Date;
    public ulong Money;
    public uint EMoney;
    public uint itemid;
    public uint Emoney_Record_Type;
    public uint Flag;
    public DateTime TimeStamp;

    public LegacyNativeMailList()
    {
    }

    public LegacyNativeMailList(LegacyNativeGameClientRuntime client, string _title, string _Sender_name,
        string _content, uint _Expiration_Date, ulong _Money, uint _EMoney, uint _itemid, uint _Flag, uint _Emoney_Record_Type)
    {
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        uint num = 1u;
        LegacyNativePacket stream = recycledPacket.GetStream();
        id = num++;
        while (client.MailList.ContainsKey(id))
        {
            id = num++;
        }
        title = _title;
        Sender_name = _Sender_name;
        content = _content;
        Expiration_Date = _Expiration_Date;
        TimeStamp = DateTime.Now.AddSeconds(Expiration_Date);
        Money = _Money;
        EMoney = _EMoney;
        itemid = _itemid;
        Flag = _Flag;
        Emoney_Record_Type = _Emoney_Record_Type;
        LegacyNativeCMsgMailList cMsgMailList = new LegacyNativeCMsgMailList
        {
            Type = 2,
            MailList = new LegacyNativeCMsgMailList.Mail[1]
        };
        cMsgMailList.MailList[0] = new LegacyNativeCMsgMailList.Mail(this);
        client.Send(stream.CreateMailList(cMsgMailList));
        client.MailList.Add(id, this);
        LegacyNativeCMsgMailNotify.Loading(client);
    }
}

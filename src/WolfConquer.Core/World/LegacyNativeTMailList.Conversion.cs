#nullable disable

namespace WolfConquer.Core.World;

internal partial struct LegacyNativeTMailList
{
    public LegacyNativeTMailList GetMailList(LegacyNativeMailList obj)
    {
        title = obj.title;
        Sender_name = obj.Sender_name;
        content = obj.content;
        id = obj.id;
        Money = obj.Money;
        EMoney = obj.EMoney;
        itemid = obj.itemid;
        Emoney_Record_Type = obj.Emoney_Record_Type;
        Flag = obj.Flag;
        Timer = obj.TimeStamp.Ticks;
        return this;
    }

    public LegacyNativeMailList GetMailList()
    {
        if (Timer > 0 && DateTime.Now < DateTime.FromBinary(Timer))
        {
            LegacyNativeMailList mailList = new LegacyNativeMailList();
            mailList.title = title;
            mailList.Sender_name = Sender_name;
            mailList.content = content;
            mailList.id = id;
            mailList.Money = Money;
            mailList.EMoney = EMoney;
            mailList.itemid = itemid;
            mailList.Emoney_Record_Type = Emoney_Record_Type;
            mailList.Flag = Flag;
            mailList.TimeStamp = DateTime.FromBinary(Timer);
            mailList.Expiration_Date = unchecked((uint)(mailList.TimeStamp - DateTime.Now).TotalMinutes * 60);
            return mailList;
        }
        return null;
    }
}

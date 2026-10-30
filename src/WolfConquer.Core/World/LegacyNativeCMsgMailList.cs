#nullable disable
using ProtoBuf;

namespace WolfConquer.Core.World;

// Complete native physical protobuf contract. Producers/whole receiver are separate partials.
[ProtoContract]
internal partial class LegacyNativeCMsgMailList
{
    [ProtoContract]
    public class Mail
    {
        [ProtoMember(1, IsRequired = true)] public ulong Id;
        [ProtoMember(2, IsRequired = true)] public ulong Money;
        [ProtoMember(3, IsRequired = true)] public ulong EMoney;
        [ProtoMember(4, IsRequired = true)] public ulong Expiration_Date;
        [ProtoMember(5, IsRequired = true)] public ulong Itemid;
        [ProtoMember(6, IsRequired = true)] public ulong Emoney_Record_Type;
        [ProtoMember(7, IsRequired = true)] public ulong Flag;
        [ProtoMember(8, IsRequired = true)] public string Title;
        [ProtoMember(9, IsRequired = true)] public string Sender_Name;

        public Mail(LegacyNativeMailList obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            Id = obj.id;
            Money = obj.Money;
            EMoney = obj.EMoney;
            Expiration_Date = obj.Expiration_Date;
            Itemid = obj.itemid;
            Emoney_Record_Type = obj.Emoney_Record_Type;
            Flag = obj.Flag;
            Sender_Name = obj.Sender_name;
            Title = obj.title;
        }

        internal Mail(Mail source)
        {
            ArgumentNullException.ThrowIfNull(source);
            Id = source.Id;
            Money = source.Money;
            EMoney = source.EMoney;
            Expiration_Date = source.Expiration_Date;
            Itemid = source.Itemid;
            Emoney_Record_Type = source.Emoney_Record_Type;
            Flag = source.Flag;
            Title = source.Title;
            Sender_Name = source.Sender_Name;
        }
    }

    [ProtoMember(1, IsRequired = true)] public int Type;
    [ProtoMember(2, IsRequired = true)] public int Count;
    [ProtoMember(3, IsRequired = true)] public Mail[] MailList;
}

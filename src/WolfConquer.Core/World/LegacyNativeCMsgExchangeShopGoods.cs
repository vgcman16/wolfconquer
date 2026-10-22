#nullable disable
using ProtoBuf;

namespace WolfConquer.Core.World;

[ProtoContract]
internal class LegacyNativeCMsgExchangeShopGoods
{
    [ProtoContract]
    public class Item
    {
        [ProtoMember(1, IsRequired = true)]
        public uint Index;

        [ProtoMember(2, IsRequired = true)]
        public uint Count;

        [ProtoMember(3, IsRequired = true)]
        public uint uk;
    }

    [ProtoMember(1, IsRequired = true)]
    public uint NpcID;

    [ProtoMember(2, IsRequired = true)]
    public uint ShopID;

    [ProtoMember(3, IsRequired = true)]
    public uint UID;

    [ProtoMember(4, IsRequired = true)]
    public uint TimeInSeconds;

    [ProtoMember(5, IsRequired = true)]
    public uint uk5;

    [ProtoMember(6, IsRequired = true)]
    public Item[] Items;
}

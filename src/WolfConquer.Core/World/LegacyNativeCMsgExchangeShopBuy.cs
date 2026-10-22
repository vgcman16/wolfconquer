#nullable disable
using ProtoBuf;

namespace WolfConquer.Core.World;

// Complete native contract only. Process1 requires the real inventory mutation
// graph and is absent, not replaced with an acknowledged no-op purchase.
[ProtoContract]
internal class LegacyNativeCMsgExchangeShopBuy
{
    [ProtoContract]
    public class Item
    {
        [ProtoMember(1, IsRequired = true)]
        public uint ID;

        [ProtoMember(2, IsRequired = true)]
        public uint Count;
    }

    [ProtoMember(1, IsRequired = true)]
    public uint Type;

    [ProtoMember(2, IsRequired = true)]
    public uint NpcID;

    [ProtoMember(3, IsRequired = true)]
    public uint ShopID;

    [ProtoMember(4, IsRequired = true)]
    public uint Count;

    [ProtoMember(5, IsRequired = true)]
    public uint Index;

    [ProtoMember(6, IsRequired = true)]
    public Item[] Items;
}

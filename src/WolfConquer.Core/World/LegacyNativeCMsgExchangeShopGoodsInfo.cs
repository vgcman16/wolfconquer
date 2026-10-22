using ProtoBuf;

namespace WolfConquer.Core.World;

// Complete native contract/enum. Process2 is not installed by these codecs.
[ProtoContract]
internal class LegacyNativeCMsgExchangeShopGoodsInfo
{
    public enum _Type : byte
    {
        Refresh = 1,
        Show
    }

    [ProtoMember(1, IsRequired = true)]
    public uint Type;

    [ProtoMember(2, IsRequired = true)]
    public uint NpcID;

    [ProtoMember(3, IsRequired = true)]
    public uint ShopID;
}

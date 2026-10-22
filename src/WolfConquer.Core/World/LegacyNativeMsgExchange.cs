using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native MsgExchange primitive codec family, not host registration.
internal static class LegacyNativeMsgExchange
{
    public const ushort CMsgExchangeShopGoods = 2317;
    public const ushort CMsgExchangeShopGoodsInfo = 2212;
    public const ushort CMsgExchangeShopBuy = 2109;

    public static LegacyNativePacket CreateExchangeShopBuy(this LegacyNativePacket stream, LegacyNativeCMsgExchangeShopBuy obj)
    {
        stream.InitWriter();
        stream.ProtoBufferSerialize(obj);
        stream.Finalize(2109);
        return stream;
    }

    public static LegacyNativePacket CreateExchangeShopGoods(this LegacyNativePacket stream, LegacyNativeCMsgExchangeShopGoods obj)
    {
        stream.InitWriter();
        stream.ProtoBufferSerialize(obj);
        stream.Finalize(2317);
        return stream;
    }

    public static void GetExchangeShopBuy(this LegacyNativePacket stream, out LegacyNativeCMsgExchangeShopBuy pQuery)
    {
        pQuery = new LegacyNativeCMsgExchangeShopBuy();
        pQuery = stream.ProtoBufferDeserialize(pQuery);
    }

    public static void GetExchangeShopGoods(this LegacyNativePacket stream, out LegacyNativeCMsgExchangeShopGoods pQuery)
    {
        pQuery = new LegacyNativeCMsgExchangeShopGoods();
        pQuery = stream.ProtoBufferDeserialize(pQuery);
    }

    public static void GetExchangeShopGoodsInfo(this LegacyNativePacket stream, out LegacyNativeCMsgExchangeShopGoodsInfo pQuery)
    {
        pQuery = new LegacyNativeCMsgExchangeShopGoodsInfo();
        pQuery = stream.ProtoBufferDeserialize(pQuery);
    }
}

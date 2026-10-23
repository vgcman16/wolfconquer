using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native MsgTradePartner metadata and asymmetric request/response codecs.
internal static class LegacyNativeTradePartnerPacket
{
    public enum Action : byte
    {
        RequestPartnership, RejectRequest, AddOnline, AddOffline, BreakPartnership, AddPartner
    }

    public static void GetTradePartner(this LegacyNativePacket stream, out uint UID, out Action mode, out bool online, out int HoursLeft)
    {
        UID = stream.ReadUInt32();
        mode = (Action)stream.ReadInt8();
        online = stream.ReadInt8() == 1;
        HoursLeft = stream.ReadInt32();
    }

    public static LegacyNativePacket TradePartnerCreate(this LegacyNativePacket stream, uint UID, Action Typ,
        bool online, int HoursLeft, uint Lookface, uint Level, uint NobilityRank, string PersonalSignature, string Name)
    {
        stream.InitWriter();
        stream.Write(UID);
        stream.Write((byte)Typ);
        stream.Write((byte)(online ? 1u : 0u));
        stream.Write((ushort)HoursLeft);
        stream.Write(Lookface);
        stream.Write(Level);
        stream.Write(NobilityRank);
        stream.Write(0u);
        stream.Write(PersonalSignature, 64);
        stream.Write(1u);
        stream.Write(Name, 32);
        stream.Finalize(2004);
        return stream;
    }
}

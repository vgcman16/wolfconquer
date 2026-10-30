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
        ArgumentNullException.ThrowIfNull(stream);
        var parsed = stream.ReadPrepared(prepared =>
        {
            uint uid = prepared.ReadUInt32();
            var action = (Action)prepared.ReadInt8();
            bool isOnline = prepared.ReadInt8() == 1;
            int hours = prepared.ReadInt32();
            return (Uid: uid, Mode: action, Online: isOnline, Hours: hours);
        });
        UID = parsed.Uid;
        mode = parsed.Mode;
        online = parsed.Online;
        HoursLeft = parsed.Hours;
    }

    public static LegacyNativePacket TradePartnerCreate(this LegacyNativePacket stream, uint UID, Action Typ,
        bool online, int HoursLeft, uint Lookface, uint Level, uint NobilityRank, string PersonalSignature, string Name)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(PersonalSignature);
        ArgumentNullException.ThrowIfNull(Name);
        stream.PrepareFrame(2004, prepared =>
        {
            prepared.Write(UID);
            prepared.Write((byte)Typ);
            prepared.Write((byte)(online ? 1u : 0u));
            prepared.Write((ushort)HoursLeft);
            prepared.Write(Lookface);
            prepared.Write(Level);
            prepared.Write(NobilityRank);
            prepared.Write(0u);
            prepared.Write(PersonalSignature, 64);
            prepared.Write(1u);
            prepared.Write(Name, 32);
        });
        return stream;
    }
}

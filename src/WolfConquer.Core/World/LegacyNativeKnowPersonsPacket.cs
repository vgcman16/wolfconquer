using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native MsgKnowPersons metadata and codecs, not its host receiver.
internal static class LegacyNativeKnowPersonsPacket
{
    public enum Action : byte
    {
        RequestFriendship = 10, AcceptRequest = 11, AddOnline = 12, AddOffline = 13,
        RemovePerson = 14, AddFriend = 15, RemoveEnemy = 18, AddEnemy = 19
    }

    public static void GetKnowPersons(this LegacyNativePacket stream, out uint UID, out Action mode, out bool online)
    {
        UID = stream.ReadUInt32();
        mode = (Action)stream.ReadInt8();
        online = stream.ReadInt8() == 1;
    }

    public static LegacyNativePacket KnowPersonsCreate(this LegacyNativePacket stream, Action Typ, uint UID,
        bool online, uint NobilityRank, uint UserInfo, uint LookFace, uint Level, string PersonalSignature, string Name)
    {
        stream.InitWriter();
        stream.Write(UID);
        stream.Write((byte)Typ);
        stream.Write((byte)(online ? 1u : 0u));
        stream.Write((ushort)0);
        stream.Write(NobilityRank);
        stream.Write(UserInfo);
        stream.Write(LookFace);
        stream.Write(Level);
        stream.Write(PersonalSignature, 64);
        stream.Write(1u);
        stream.Write(Name, 32);
        stream.Finalize(2139);
        return stream;
    }
}

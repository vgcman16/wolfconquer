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
        ArgumentNullException.ThrowIfNull(stream);
        var parsed = stream.ReadPrepared(prepared =>
        {
            uint uid = prepared.ReadUInt32();
            var action = (Action)prepared.ReadInt8();
            bool isOnline = prepared.ReadInt8() == 1;
            return (Uid: uid, Mode: action, Online: isOnline);
        });
        UID = parsed.Uid;
        mode = parsed.Mode;
        online = parsed.Online;
    }

    public static LegacyNativePacket KnowPersonsCreate(this LegacyNativePacket stream, Action Typ, uint UID,
        bool online, uint NobilityRank, uint UserInfo, uint LookFace, uint Level, string PersonalSignature, string Name)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(PersonalSignature);
        ArgumentNullException.ThrowIfNull(Name);
        stream.PrepareFrame(2139, prepared =>
        {
            prepared.Write(UID);
            prepared.Write((byte)Typ);
            prepared.Write((byte)(online ? 1u : 0u));
            prepared.Write((ushort)0);
            prepared.Write(NobilityRank);
            prepared.Write(UserInfo);
            prepared.Write(LookFace);
            prepared.Write(Level);
            prepared.Write(PersonalSignature, 64);
            prepared.Write(1u);
            prepared.Write(Name, 32);
        });
        return stream;
    }
}

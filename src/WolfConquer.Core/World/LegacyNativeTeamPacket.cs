using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native MsgTeam enum and two codecs; no team receiver/host adoption.
internal static class LegacyNativeTeamPacket
{
    public enum TeamTypes : uint
    {
        Create, JoinRequest, ExitTeam, AcceptInvitation, InviteRequest,
        AcceptJoinRequest, Dismiss, Kick, ForbidJoining, UnforbidJoining,
        LootMoneyOff, LootMoneyOn, LootItemsOff, LootItemsOn, RejectInvitation, Send
    }

    public static void GetTeamPacket(this LegacyNativePacket stream, out TeamTypes Typ, out uint UID)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var prepared = stream.ReadPrepared(static packet =>
            (Type: (TeamTypes)packet.ReadUInt32(), Uid: packet.ReadUInt32()));
        Typ = prepared.Type;
        UID = prepared.Uid;
    }

    public static LegacyNativePacket TeamCreate(this LegacyNativePacket stream, TeamTypes Typ, uint UID)
    {
        stream.InitWriter();
        stream.Write((uint)Typ);
        stream.Write(UID);
        stream.Finalize(2243);
        return stream;
    }
}

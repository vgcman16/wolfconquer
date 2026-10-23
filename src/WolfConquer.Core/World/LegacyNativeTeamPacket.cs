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
        Typ = (TeamTypes)stream.ReadUInt32();
        UID = stream.ReadUInt32();
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

using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native TeamLeadership declaration and MsgTeamLeadership codecs only.
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct LegacyNativeTeamLeadership
{
    public LegacyNativeTeamLeadershipPacket.Mode Typ;
    public uint UID;
    public uint LeaderUID;
    public int Count;
    public uint UnKnow;
}

internal static class LegacyNativeTeamLeadershipPacket
{
    public enum Mode : uint
    {
        Leader = 1u,
        Teammate
    }

    public static unsafe void GetTeamLeadership(this LegacyNativePacket stream, LegacyNativeTeamLeadership* pQuery)
    {
        stream.ReadUnsafe(pQuery, sizeof(LegacyNativeTeamLeadership));
    }

    public static unsafe LegacyNativePacket TeamLeadershipCreate(this LegacyNativePacket stream, LegacyNativeTeamLeadership* pQuery)
    {
        stream.InitWriter();
        stream.WriteUnsafe(pQuery, sizeof(LegacyNativeTeamLeadership));
        stream.Finalize(2022);
        return stream;
    }
}

using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

[StructLayout(LayoutKind.Sequential, Size = 1)]
internal partial struct LegacyNativeMsgTeamEliteSetTeamName
{
    public enum State : uint { Apprend, SuccessfulName, RenameWasSuccessfulName, Remove }
}

internal static class LegacyNativeTeamEliteSetTeamNamePacket
{
    // Whole native MsgBuilder3252..3268.
    public static void GetEliteSetTeamName(this LegacyNativePacket stream, out uint dwparam1, out uint dwparam2, out string name)
    {
        dwparam1 = stream.ReadUInt32();
        dwparam2 = stream.ReadUInt32();
        name = stream.ReadCString(32);
    }

    public static LegacyNativePacket TeamEliteSetTeamName(this LegacyNativePacket stream, ushort ID, LegacyNativeMsgTeamEliteSetTeamName.State state, uint TeamUID, string name)
    {
        stream.InitWriter();
        stream.Write((uint)state);
        stream.Write(TeamUID);
        stream.Write(name, 32);
        stream.Finalize(ID);
        return stream;
    }
}

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
        ArgumentNullException.ThrowIfNull(stream);
        var parsed = stream.ReadPrepared(prepared =>
        {
            uint first = prepared.ReadUInt32(), second = prepared.ReadUInt32();
            string text = prepared.ReadCString(32);
            return (First: first, Second: second, Name: text);
        });
        dwparam1 = parsed.First;
        dwparam2 = parsed.Second;
        name = parsed.Name;
    }

    public static LegacyNativePacket TeamEliteSetTeamName(this LegacyNativePacket stream, ushort ID, LegacyNativeMsgTeamEliteSetTeamName.State state, uint TeamUID, string name)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(name);
        stream.PrepareFrame(ID, prepared =>
        {
            prepared.Write((uint)state);
            prepared.Write(TeamUID);
            prepared.Write(name, 32);
        });
        return stream;
    }
}

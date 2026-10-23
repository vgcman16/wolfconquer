using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

[StructLayout(LayoutKind.Sequential, Size = 1)]
internal struct LegacyNativeMsgTeamElitePKMatchUI
{
    public enum State : uint { BeginMatch = 2u, Effect = 3u, EndMatch = 4u, Information = 7u, Reward = 8u }
    public enum EffectTyp : uint { Effect_Win = 1u, Effect_Lose = 0u }
}

internal static class LegacyNativeTeamEliteMatchUiPacket
{
    // Whole native MsgBuilder3187..3204, including the ID2270-only extra word.
    public static LegacyNativePacket TeamElitePKMatchUICreate(this LegacyNativePacket stream, ushort ID, LegacyNativeMsgTeamElitePKMatchUI.State state, LegacyNativeMsgTeamElitePKMatchUI.EffectTyp effect, uint OpponentUID, string OpponentName, uint TimeLeft)
    {
        stream.InitWriter();
        stream.Write((uint)state);
        stream.Write((uint)effect);
        if (ID == 2270)
        {
            stream.Write(0);
        }
        stream.Write(OpponentUID);
        stream.Write(0u);
        stream.Write(OpponentName, 32);
        stream.ZeroFill(8);
        stream.Write(TimeLeft);
        stream.Write(0u);
        stream.Finalize(ID);
        return stream;
    }
}

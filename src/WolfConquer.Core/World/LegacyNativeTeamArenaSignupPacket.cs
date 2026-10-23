using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeTeamArenaSignupPacket
{
    // Whole native MsgBuilder3038..3061. This does not admit the Team-dependent receiver.
    public static void GetTeamArenaSignup(this LegacyNativePacket stream, out LegacyNativeMsgTeamArenaSignup.DialogType DType, out LegacyNativeMsgTeamArenaSignup.DialogButton DButton)
    {
        DType = (LegacyNativeMsgTeamArenaSignup.DialogType)stream.ReadUInt32();
        DButton = (LegacyNativeMsgTeamArenaSignup.DialogButton)stream.ReadUInt32();
    }

    public static LegacyNativePacket TeamArenaSignupCreate(this LegacyNativePacket stream, LegacyNativeMsgTeamArenaSignup.DialogType DType, LegacyNativeMsgTeamArenaSignup.DialogButton DButton, LegacyNativeGameClientRuntime client)
    {
        stream.InitWriter();
        stream.Write((uint)DType);
        stream.Write((uint)DButton);
        stream.Write(client.Player.UID);
        stream.Write(client.Player.Name, 32);
        stream.Write(client.TeamArenaStatistic!.Info.TodayRank);
        stream.Write(client.Player.Class);
        stream.Write(0);
        stream.Write(client.TeamArenaStatistic!.Info.ArenaPoints);
        stream.Write((uint)client.Player.Level);
        stream.Finalize(2060);
        return stream;
    }
}

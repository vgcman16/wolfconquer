using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeArenaSignupPacket
{
    // Whole native MsgBuilder832..847, with physical Player/ArenaStatistic owners.
    public static LegacyNativePacket ArenaSignupCreate(this LegacyNativePacket stream,
        LegacyNativeMsgArenaSignup.DialogType DialogID, LegacyNativeMsgArenaSignup.DialogButton OptionID,
        LegacyNativeGameClientRuntime user)
    {
        stream.InitWriter();
        stream.Write((uint)DialogID);
        stream.Write((uint)OptionID);
        stream.Write(user.Player.UID);
        stream.Write(0u);
        stream.Write(user.Player.Name, 32);
        stream.Write(user.ArenaStatistic!.Info.TodayRank);
        stream.Write(user.Player.Class);
        stream.Write(0u);
        stream.Write(user.ArenaStatistic!.Info.ArenaPoints);
        stream.Write((uint)user.Player.Level);
        stream.Finalize(2140);
        return stream;
    }

    // Whole native MsgBuilder849..853. Unknown enum values are not rejected.
    public static void GetArenaSignup(this LegacyNativePacket stream,
        out LegacyNativeMsgArenaSignup.DialogType DialogID, out LegacyNativeMsgArenaSignup.DialogButton OptionID)
    {
        DialogID = (LegacyNativeMsgArenaSignup.DialogType)stream.ReadUInt32();
        OptionID = (LegacyNativeMsgArenaSignup.DialogButton)stream.ReadUInt32();
    }
}

using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeArenaMatchRuntime
{
    // Whole native MsgArena.Match334..337; user need not be a Match participant.
    public void SendSignUp(LegacyNativePacket stream, LegacyNativeGameClientRuntime user)
    {
        user.Send(stream.ArenaSignupCreate(LegacyNativeMsgArenaSignup.DialogType.StartCountDown,
            LegacyNativeMsgArenaSignup.DialogButton.SignUp, user));
    }
}

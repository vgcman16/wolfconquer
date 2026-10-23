using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

// Entire native physical layout/enums. The whole Handler is separately typed-held.
[StructLayout(LayoutKind.Explicit, Size = 60)]
internal partial struct LegacyNativeMsgArenaSignup
{
    public enum DialogType : uint
    {
        ArenaIconOn = 0u, ArenaIconOff = 1u, ArenaGui = 3u, StartCountDown = 2u,
        OpponentGaveUp = 4u, Match = 5u, YouAreKicked = 6u, StartTheFight = 7u,
        Dialog = 8u, Dialog2 = 9u, Continue = 10u
    }

    public enum DialogButton : uint
    {
        Lose = 3u, Win = 1u, DoGiveUp = 2u, Accept = 1u, MatchOff = 3u,
        MatchOn = 5u, SignUp = 0u
    }
}

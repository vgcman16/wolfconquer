using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

[StructLayout(LayoutKind.Explicit, Size = 60)]
internal partial struct LegacyNativeMsgTeamArenaSignup
{
    public enum DialogType : uint
    {
        ArenaIconOn = 0u, ArenaIconOff = 1u, ArenaGui = 3u, StartCountDown = 2u,
        OpponentGaveUp = 4u, BuyPoints = 5u, Match = 6u, YouAreKicked = 7u,
        StartTheFight = 8u, Dialog = 9u, Dialog2 = 10u, Continue = 11u
    }

    public enum DialogButton : uint
    {
        Lose = 3u, Win = 1u, DoGiveUp = 2u, Accept = 1u,
        MatchOff = 3u, MatchOn = 5u, SignUp = 0u
    }
}

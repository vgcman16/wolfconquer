using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

// Complete native physical declarations only. The whole Handler remains absent until
// genuine Player message-box state/admission and blocked-teleport authority are available.
[StructLayout(LayoutKind.Sequential, Size = 1)]
internal struct LegacyNativeMsgStaticMessage
{
    public enum Messages : uint
    {
        None = 0u,
        GuildWar = 10515u,
        ClassPk = 10519u,
        WeeklyPKWar = 10521u,
        MonthlyPk = 10532u,
        HorseRace = 10525u,
        MonthlyPK = 10527u,
        WeeklyPK = 10529u,
        PowerArena = 10531u,
        ElitePKTournament = 10533u,
        CapturetheFlag = 10535u,
        GuildContest = 10537u,
        SkillTeamPKTournament = 10541u,
        ChampionsArena = 10543u,
        CashRain = 10545u,
        FlameLit = 10547u,
        TeamPkTournament = 10543u
    }

    public enum Action : uint
    {
        Append = 6u
    }
}

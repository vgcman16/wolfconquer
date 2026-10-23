using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Entire native ProcesType/TournamentType/ITournament contracts, not host adapters.
internal enum LegacyNativeProcesType : byte { Alive = 1, Idle, Dead }

internal enum LegacyNativeTournamentType : byte
{
    None = 0, DragonWar = 1, FootBall = 2, BattleField = 3, DBShower = 4,
    TeamDeathMatch = 5, LastManStand = 6, TopFight = 14, FindGM = 16,
    ExtremePk = 7, KillerOfElite = 8, TreasureThief = 9, FreezeWar = 10,
    SkillTournament = 11, KingOfTheHill = 12, Count = 13, QuizShow = 15
}

internal interface ILegacyNativeTournament
{
    LegacyNativeProcesType Process { get; set; }
    LegacyNativeTournamentType Type { get; set; }
    void Open();
    bool Join(LegacyNativeGameClientRuntime user, LegacyNativePacket stream);
    void CheckUp();
    bool InTournament(LegacyNativeGameClientRuntime user);
}

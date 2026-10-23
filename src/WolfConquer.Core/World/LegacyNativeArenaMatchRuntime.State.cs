using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Native solo Match physical state and COMPLETE Match307..318 constructor.
// Export/import/win/death/teleport gameplay methods remain whole held/absent.
internal partial class LegacyNativeArenaMatchRuntime
{
    public List<uint> Cheerers = new List<uint>();
    public ConcurrentDictionary<uint, LegacyNativeGameClientRuntime> Watchers = new();
    public bool Done;
    public bool Imported = false;
    public uint dinamicID;
    private uint UID;
    public DateTime DoneStamp;
    public DateTime FightStamp;
    public DateTime StartTimer;
    public LegacyNativeGameClientRuntime[] Players;
    public uint MatchUID => UID;

    public LegacyNativeArenaMatchRuntime(LegacyNativeGameClientRuntime user1,
        LegacyNativeGameClientRuntime user2, uint _uid)
    {
        Players = new LegacyNativeGameClientRuntime[2];
        Players[0] = user1;
        Players[1] = user2;
        UID = _uid;
        DoneStamp = default(DateTime);
        user1.ArenaMatch = (user2.ArenaMatch = this);
        user1.ArenaStatistic!.QualifierStatus = (user2.ArenaStatistic!.QualifierStatus = LegacyNativeArenaUser.MatchStatus.None);
        StartTimer = DateTime.Now;
    }
}

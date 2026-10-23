using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Genuine MsgArena instance state/constructor; static state is the SAME actual
// LegacyNativeArenaRankings owner from the frozen inventory prerequisite package.
internal partial class LegacyNativeArenaRuntime
{
    public enum ArenaIDs : uint { ShowPlayerRankList = 10u, QualifierList = 6u }

    public List<uint> BlockArenaMaps = new List<uint>
    {
        6011u, 4020u, 2351u, 2352u, 2353u, 3820u, 5040u, 2057u, 3825u, 4000u,
        6546u, 6525u, 8009u, 6729u, 4003u, 5342u, 4006u, 4008u, 4009u, 6000u,
        6001u, 6521u, 9988u, 6891u, 5599u, 8521u, 1017u, 1080u, 1081u, 2060u,
        6002u, 6003u, 601u, 700u, 1038u, 1764u, 1036u, 1764u, 2068u, 3868u
    };
    public ConcurrentDictionary<uint, LegacyNativeGameClientRuntime> Registered;
    public LegacyNativeCounter MatchCounter = new LegacyNativeCounter(1u);
    public ConcurrentDictionary<uint, LegacyNativeArenaMatchRuntime> MatchesRegistered;
    public LegacyNativeProcesType Proces { get; set; }

    // Whole native MsgArena607..612, including both true distinct dictionaries.
    public LegacyNativeArenaRuntime()
    {
        Proces = LegacyNativeProcesType.Dead;
        Registered = new ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>();
        MatchesRegistered = new ConcurrentDictionary<uint, LegacyNativeArenaMatchRuntime>();
    }
}

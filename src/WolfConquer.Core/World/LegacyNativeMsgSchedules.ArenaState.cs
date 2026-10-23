namespace WolfConquer.Core.World;

// Native scheduler physical prerequisites, not full Create/scheduler behavior.
// Tournaments13 is its ONLY nondefault static initializer (whole source checked).
// The omitted unrelated reference fields are implicit null, not constructed here.
internal partial class LegacyNativeMsgSchedules
{
    public static Dictionary<LegacyNativeTournamentType, ILegacyNativeTournament> Tournaments = new();
    public static ILegacyNativeTournament? CurrentTournament;
    internal static LegacyNativeArenaRuntime? Arena;
    // TeamArena is retained with its genuine whole owner in production-held;
    // source Create does not assign it, and this prerequisite does not repair that.
}

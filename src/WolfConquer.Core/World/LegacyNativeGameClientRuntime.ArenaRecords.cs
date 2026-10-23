namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Exact native GameClient177/179 nullable slots; no client birth allocation.
    internal LegacyNativeTeamArenaUser? TeamArenaStatistic;
    internal LegacyNativeArenaUser? ArenaStatistic;

    // Whole native GameClient342..417. No record birth, packet or fake point award.
    public uint ArenaPoints
    {
        get
        {
            if (ArenaStatistic == null) return 0u;
            return ArenaStatistic.Info.ArenaPoints;
        }
        set
        {
            if (ArenaStatistic != null) ArenaStatistic.Info.ArenaPoints = value;
        }
    }

    public uint TeamArenaPoints
    {
        get
        {
            if (TeamArenaStatistic == null) return 0u;
            return TeamArenaStatistic.Info.ArenaPoints;
        }
        set
        {
            if (TeamArenaStatistic != null) TeamArenaStatistic.Info.ArenaPoints = value;
        }
    }

    public uint TeamArenaHonorPoints
    {
        get
        {
            if (TeamArenaStatistic == null) return 0u;
            return TeamArenaStatistic.Info.CurrentHonor;
        }
        set
        {
            if (TeamArenaStatistic != null) TeamArenaStatistic.Info.CurrentHonor = value;
        }
    }

    public uint ArenaHonorPoints
    {
        get
        {
            if (ArenaStatistic == null) return 0u;
            return ArenaStatistic.Info.CurrentHonor;
        }
        set
        {
            if (ArenaStatistic != null) ArenaStatistic.Info.CurrentHonor = value;
        }
    }
}

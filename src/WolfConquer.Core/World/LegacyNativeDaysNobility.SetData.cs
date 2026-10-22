namespace WolfConquer.Core.World;

internal partial class LegacyNativeDaysNobility
{
    public void SetData(uint ID = 0u, ulong lastDS = 0uL, byte rank = 0, long tick = 0L, bool act = false)
    {
        if (ID != 0)
        {
            id = ID;
        }
        if (lastDS != 0)
        {
            lastdonation = lastDS;
        }
        if (rank != 0)
        {
            paidrnk = (LegacyNativeNobilityRank)rank;
        }
        if (tick != 0)
        {
            periodtick = tick;
        }
        if (act)
        {
            Active = act;
        }
    }

    public void SetDataAnyWay(uint ID = 0u, ulong lastDS = 0uL, byte rank = 0, long tick = 0L, bool act = false)
    {
        id = ID;
        lastdonation = lastDS;
        paidrnk = (LegacyNativeNobilityRank)rank;
        periodtick = tick;
        Active = act;
    }
}

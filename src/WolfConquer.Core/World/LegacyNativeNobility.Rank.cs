namespace WolfConquer.Core.World;

internal partial class LegacyNativeNobility
{
    public LegacyNativeNobilityRank Rank
    {
        get
        {
            if (Position >= 0 && Position < 3)
            {
                return LegacyNativeNobilityRank.King;
            }
            if (Position >= 3 && Position < 17)
            {
                return LegacyNativeNobilityRank.Prince;
            }
            if (Position >= 17 && Position < 50)
            {
                return LegacyNativeNobilityRank.Duke;
            }
            if (Donation >= 20000)
            {
                return LegacyNativeNobilityRank.Earl;
            }
            if (Donation >= 10000)
            {
                return LegacyNativeNobilityRank.Baron;
            }
            if (Donation >= 30000)
            {
                return LegacyNativeNobilityRank.Knight;
            }
            return LegacyNativeNobilityRank.Serf;
        }
    }
}

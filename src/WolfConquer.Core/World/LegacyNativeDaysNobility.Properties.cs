namespace WolfConquer.Core.World;

internal partial class LegacyNativeDaysNobility
{
    public bool IsActive => Active;

    public uint Identifier
    {
        get
        {
            return id;
        }
        private set
        {
            id = value;
        }
    }

    public ulong LastNobilityDonation
    {
        get
        {
            return lastdonation;
        }
        private set
        {
            lastdonation = value;
        }
    }

    public LegacyNativeNobilityRank PaidRank
    {
        get
        {
            return paidrnk;
        }
        private set
        {
            paidrnk = value;
        }
    }

    public DateTime PeriodTime
    {
        get
        {
            return DateTime.FromBinary(periodtick);
        }
        private set
        {
            periodtick = value.ToBinary();
        }
    }
}

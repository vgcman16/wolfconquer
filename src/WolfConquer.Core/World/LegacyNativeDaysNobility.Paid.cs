namespace WolfConquer.Core.World;

internal partial class LegacyNativeDaysNobility
{
    public void Paid(int days, LegacyNativeNobilityRank rnk)
    {
        paidrnk = rnk;
        periodtick = DateTime.Now.AddDays(days).ToBinary();
        lastdonation = Owner.Nobility!.Donation;
        LegacyNativePoolNobility.NobilityRanking.RemoveAndUpdateTheRest(id);
        Active = true;
        switch (rnk)
        {
            case LegacyNativeNobilityRank.King:
                Owner.Nobility!.Donation = 0uL;
                Owner.Nobility!.Position = 0;
                Owner.NobilityRank = Owner.Nobility!.Rank;
                break;
            case LegacyNativeNobilityRank.Prince:
                Owner.Nobility!.Donation = 0uL;
                Owner.Nobility!.Position = 6;
                Owner.NobilityRank = Owner.Nobility!.Rank;
                break;
            case LegacyNativeNobilityRank.Duke:
                Owner.Nobility!.Donation = 0uL;
                Owner.Nobility!.Position = 15;
                Owner.NobilityRank = Owner.Nobility!.Rank;
                break;
            default:
                Active = false;
                break;
        }
    }
}

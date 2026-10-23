namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Whole native Player3161..3170. Uses the genuine incoming ChampionPoints owner.
    public void AddChampionPoints(uint value, bool settodayvalue = true)
    {
        if (settodayvalue && TodayChampionPoints > 650)
        {
            Owner.SendSysMesage("Your Champion Points have reached the maximum amount of 650 points. You can collect more points tomorrow.");
            return;
        }
        ChampionPoints += value;
        TodayChampionPoints += value;
    }
}

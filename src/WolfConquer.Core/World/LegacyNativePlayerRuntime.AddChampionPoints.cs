namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Retain source eligibility/message defaults; prepare every balance before commit.
    public void AddChampionPoints(uint value, bool settodayvalue = true)
    {
        lock (SpellAdmissionSyncRoot)
        {
            if (settodayvalue && TodayChampionPoints > 650)
            {
                Owner.SendSysMesage("Your Champion Points have reached the maximum amount of 650 points. You can collect more points tomorrow.");
                return;
            }
            uint current = checked(_ChampionPoints + value);
            uint today = checked(TodayChampionPoints + value);
            uint history = Math.Max(HistoryChampionPoints, current);
            // No ordinary throwing work or output after the first field assignment.
            _ChampionPoints = current;
            TodayChampionPoints = today;
            HistoryChampionPoints = history;
        }
    }
}

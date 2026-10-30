namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Direct raw writers do not cooperate with the award/setter gate.
    public uint TodayChampionPoints = 0u;
    public uint HistoryChampionPoints = 0u;
    private uint _ChampionPoints;

    public uint ChampionPoints
    {
        get
        {
            lock (SpellAdmissionSyncRoot) return _ChampionPoints;
        }
        set
        {
            lock (SpellAdmissionSyncRoot)
            {
                _ChampionPoints = value;
                if (value > HistoryChampionPoints) HistoryChampionPoints = value;
            }
        }
    }
}

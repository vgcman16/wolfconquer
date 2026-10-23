namespace WolfConquer.Core.World;
public sealed partial class LegacyNativePlayerRuntime
{
public uint TodayChampionPoints = 0u;

public uint HistoryChampionPoints = 0u;

private uint _ChampionPoints;

public uint ChampionPoints
	{
		get
		{
			return _ChampionPoints;
		}
		set
		{
			_ChampionPoints = value;
			if (_ChampionPoints > HistoryChampionPoints)
			{
				HistoryChampionPoints = value;
			}
		}
	}
}

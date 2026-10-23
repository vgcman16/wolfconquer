using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
namespace WolfConquer.Core.World;

// Whole nested physical state, actual map/dynamic-ID constructor and scalar queries.
internal partial class LegacyNativeEliteGroupRuntime
{
public partial class Match
	{
		public enum StatusFlag : ushort
		{
			AcceptingWagers,
			Watchable,
			SwitchOut,
			InFight,
			OK
		}

		private DateTime ImportTime;

		public ushort Index;

		public uint ID;

		public StatusFlag Flag;

		public LegacyNativeDbSafeDictionary<uint, LegacyNativeGameClientRuntime> Players;

		public DateTime ExportTimer = default(DateTime);

		public LegacyNativeDbSafeDictionary<uint, FighterStats> MatchStats;

		public List<uint> Cheerers = new List<uint>();

		public LegacyNativeDbSafeDictionary<uint, LegacyNativeGameClientRuntime> Watchers = new LegacyNativeDbSafeDictionary<uint, LegacyNativeGameClientRuntime>();

		public LegacyNativeGameClientRuntime? PlayerWaiting = null;

		private bool Done = false;

		private bool Exported = false;

		private bool Imported = false;

		private LegacyNativeGameMap Map;

		public uint DinamicID;

		public LegacyNativeEliteGroupRuntime? elitepkgroup;


public Match(LegacyNativeEliteGroupRuntime? elitegroup)
		{
			elitepkgroup = elitegroup;
			Players = new LegacyNativeDbSafeDictionary<uint, LegacyNativeGameClientRuntime>();
			Map = LegacyNativePoolMaps.ServerMaps![700u];
			DinamicID = Map.GenerateDynamicID();
			Flag = StatusFlag.AcceptingWagers;
			MatchStats = new LegacyNativeDbSafeDictionary<uint, FighterStats>();
		}
public uint TimeLeft
		{
			get
			{
				int num = (ImportTime.AddSeconds(420.0).AllMilliseconds() - DateTime.Now.AllMilliseconds()) / 1000;
				if (num < 0)
				{
					num = 0;
				}
				return (uint)num;
			}
		}
public LegacyNativeGameClientRuntime[] PlayersArray => Players.GetValues();
public int GroupID => (int)ID / 100000 - 1;
public int Count => Players.Count;
public bool IsFinishd()
		{
			return Exported;
		}
    }
}

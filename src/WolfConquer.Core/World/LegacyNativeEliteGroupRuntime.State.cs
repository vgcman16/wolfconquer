using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
namespace WolfConquer.Core.World;

// Whole native physical outer state786..843 and full constructor868..879.
internal partial class LegacyNativeEliteGroupRuntime
{
	public enum States : byte
	{
		T_Organize,
		T_CreateMatches,
		T_Import,
		T_Fights,
		T_Finished,
		T_ReOrganize
	}

	public const ushort WaitingAreaID = 2068;

	public LegacyNativeGameMap Map = null!;

	public uint DinamycID;

	public LegacyNativeMsgElitePKBrackets.GuiTyp State;

	private States pState = States.T_Organize;

	public FighterStats[] Top8 = new FighterStats[0];

	public uint MatchIndex = 0u;

	public DateTime StartTimer = default(DateTime);

	public DateTime WaitForFinish = default(DateTime);

	public LegacyNativeDbSafeDictionary<uint, LegacyNativeGameClientRuntime> Players;

	public LegacyNativeDbSafeDictionary<uint, Match>? Matches;

	public LegacyNativeDbSafeDictionary<uint, Match>? Top4Matches;

	public LegacyNativeDbSafeDictionary<uint, Match>? ThreeQualiferMatch;

	public LegacyNativeDbSafeDictionary<uint, Match>? FinalMatch;

	private LegacyNativeCounter MatchCounter;

	private DateTime pStamp;

	public LegacyNativeProcesType Proces;

	public LegacyNativeEliteTournamentGroupType GroupTyp;

	public ushort TimeLeft
	{
		get
		{
			int num = (pStamp.AllMilliseconds() - DateTime.Now.AllMilliseconds()) / 1000;
			if (num < 0)
			{
				return 0;
			}
			return (ushort)num;
		}
	}
public LegacyNativeEliteGroupRuntime(LegacyNativeEliteTournamentGroupType group)
	{
		Proces = LegacyNativeProcesType.Dead;
		GroupTyp = group;
		Players = new LegacyNativeDbSafeDictionary<uint, LegacyNativeGameClientRuntime>();
		MatchCounter = new LegacyNativeCounter((uint)((int)group * 100000 + 100000));
		Top8 = new FighterStats[8];
		for (int i = 0; i < Top8.Length; i++)
		{
			Top8[i] = new FighterStats(0u, "None", 0u, 0u, 0u);
		}
	}
public bool GetReward(LegacyNativeGameClientRuntime client, out byte Rank)
	{
		if (Top8.Length == 8)
		{
			for (int i = 0; i < Top8.Length; i++)
			{
				if (Top8[i] != null && Top8[i].UID == client.Player.UID)
				{
					if (Top8[i].ClaimReward == 0)
					{
						Top8[i].ClaimReward = 1;
						Rank = (byte)(i + 1);
						return true;
					}
					Rank = (byte)(i + 1);
					return false;
				}
			}
		}
		Rank = 0;
		return false;
	}
public void SubscribeTimer()
	{
		Proces = LegacyNativeProcesType.Idle;
	}
}

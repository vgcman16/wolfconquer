using WolfConquer.Core.Networking;
namespace WolfConquer.Core.World;
internal partial class LegacyNativeMsgSteedRace
{
public class UsableRacePotion
	{
		public LegacyNativeMsgRacePotion.RaceItemType Type;

		public int Count;
	}

	public enum Maps : uint
	{
		MarketRace = 1950u,
		IceRace = 2064u,
		DungeonRace = 2063u,
		LavaRace = 2062u,
		IslandRace = 2061u
	}

	public uint[][] RaceSettings = new uint[5][]
	{
		new uint[18]
		{
			1950u, 88u, 149u, 420u, 431u, 4u, 65u, 174u, 621u, 123u,
			243u, 60u, 214u, 334u, 70u, 346u, 459u, 100u
		},
		new uint[18]
		{
			2064u, 175u, 250u, 200u, 153u, 6u, 154u, 267u, 621u, 146u,
			392u, 70u, 283u, 351u, 100u, 295u, 79u, 100u
		},
		new uint[18]
		{
			2061u, 60u, 400u, 899u, 816u, 10u, 96u, 392u, 621u, 220u,
			234u, 200u, 472u, 160u, 200u, 777u, 464u, 300u
		},
		new uint[18]
		{
			2063u, 450u, 520u, 682u, 484u, 10u, 435u, 559u, 621u, 471u,
			759u, 200u, 714u, 598u, 250u, 489u, 679u, 20u
		},
		new uint[18]
		{
			2062u, 150u, 350u, 330u, 170u, 6u, 101u, 397u, 623u, 327u,
			553u, 100u, 526u, 477u, 200u, 283u, 275u, 100u
		}
	};

	public static ushort MAPID = 1950;

	public static uint[]? Settings;

	public static uint RaceRecord;

	private LegacyNativeGameMap? Map;

	private bool isOn;

	private bool InvitationsOut;

	private bool InvitationsExpired;

	private bool FiveSecondsLeft;

	private bool GateOpen;

	private DateTime InvitationsSentOut;

	private DateTime InvitationsExpireDate;

	private DateTime Last5Seconds;

	private DateTime GateOpened;

	private LegacyNativeSobNpcRuntime? Gate;

	private ushort GateSetX;

	private ushort GateSetY;

	private volatile int Records;


    public bool CanJoin => isOn && !GateOpen;
    public bool IsOn => isOn;
    public ushort GateX => GateSetX;
    public ushort GateY => GateSetY;
public bool InSteedRace(uint mapid)
	{
		return mapid == 1950 || mapid == 2064 || mapid == 2063 || mapid == 2062 || mapid == 2061;
	}
}

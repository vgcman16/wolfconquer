using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMonsterRole : ILegacyNativeMapObj
{
	public const ushort UintStamp = 4;

	public const ushort Uint_Mesh = 8;

	public const ushort Uint_UID = 12;

	public const ushort BitVector32 = 26;

	public const ushort Uint16_HitPoints = 94;

	public const ushort Uint16_Level = 100;

	public const ushort Uint16_X = 102;

	public const ushort Uint16_Y = 104;

	public const ushort Byte_Fascing = 108;

	public const ushort Byte_Action = 109;

	public const ushort Byte_Boss = 203;

	public const ushort Str_Count = 257;

	public const ushort Str_NameLenght = 258;

	public const ushort Str_Name = 259;

	public const ushort Uint16_PLenght = 260;

	public DateTime RemoveFloor = DateTime.Now;

	public int StampFloorSecounds = 0;

	public static List<uint> SpecialMonsters = new List<uint>
	{
		3130u, 3134u, 4171u, 4212u, 4220u, 29300u, 29360u, 29363u, 29370u, 8311u,
		6819u, 60979u, 213883u, 3970u, 3971u, 3976u, 3977u, 3978u, 3979u, 3981u,
		3982u, 3983u, 3985u, 3986u, 2984u
	};

	public LegacyNativeGameClientRuntime? AttackerScarofEarthl;

	public LegacyNativeMagicType.Magic? ScarofEarthl;

	public LegacyNativeGameClientRuntime? OwnerFloor;

	public LegacyNativeMagicType.Magic? DBSpell;

	public ushort SpellLevel = 0;

	public DateTime FloorStampTimer = default(DateTime);

	public bool IsFloor = false;

	public LegacyNativeMsgItemPacket? FloorPacket;

	public bool BlackSpot = false;

	public DateTime Stamp_BlackSpot = default(DateTime);

	public byte PoisonLevel = 0;

	private DateTime DeadStamp = default(DateTime);

	private DateTime FadeAway = default(DateTime);

	public DateTime RespawnStamp = default(DateTime);

	public DateTime MoveStamp = default(DateTime);

	public object scoreObj = new object();

	public Dictionary<uint, LegacyNativeMsgBossHarmRankingEntry> Hunters = new Dictionary<uint, LegacyNativeMsgBossHarmRankingEntry>();

	public bool logged = false;

	public LegacyNativeMonsterFamily Family;

	public LegacyNativeMonsterView View;

	public LegacyNativeMobStatus State;

	public LegacyNativePlayerRuntime? Target = null;

	public DateTime AttackSpeed = default(DateTime);

	public LegacyNativeStatusFlagsBigVector32 BitVector;

	public byte OpenBoss = 0;

	public string? Name = "";

	public byte Boss = 0;

	public uint Mesh = 0u;

	public byte Level = 0;

	public uint HitPoints;

	public ushort RespawnX;

	public ushort RespawnY;

	public ushort PX = 0;

	public ushort PY = 0;

	public ushort _xx;

	public ushort _yy;

	public LegacyNativePlayerMapFlags.ConquerAction Action = LegacyNativePlayerMapFlags.ConquerAction.None;

	public LegacyNativePlayerMapFlags.ConquerAngle Facing = LegacyNativePlayerMapFlags.ConquerAngle.East;

	public string LocationSpawn = "";

	public LegacyNativeGameMap? GMap;

	public bool RemoveOnDead = false;

	public uint PetFlag = 0u;

	public int ExtraDamage => Family.extra_damage;

	public int BattlePower => Family.extra_battlelev;

	public bool AllowDynamic { get; set; }

	public uint IndexInScreen { get; set; }

	public int SizeAdd => Family.AttackRange;

	public uint Map { get; set; }

	public uint DynamicID { get; set; }

	public LegacyNativeMapObjectType ObjType { get; set; }

	public uint UID { get; set; }

	public ushort X
	{
		get
		{
			return _xx;
		}
		set
		{
			PX = _xx;
			_xx = value;
		}
	}

	public ushort Y
	{
		get
		{
			return _yy;
		}
		set
		{
			PY = _yy;
			_yy = value;
		}
	}

	public bool Alive => HitPoints != 0;

	public bool IsTrap()
	{
		return false;
	}

	public LegacyNativeMonsterRole(LegacyNativeMonsterFamily Famil, uint _UID, string locationspawn, LegacyNativeGameMap? _map)
	{
		AllowDynamic = false;
		GMap = _map;
		LocationSpawn = locationspawn;
		ObjType = LegacyNativeMapObjectType.Monster;
		Name = Famil.Name;
		Family = Famil;
		UID = _UID;
		Mesh = Famil.Mesh;
		Level = (byte)Famil.Level;
		HitPoints = (uint)Famil.MaxHealth;
		View = new LegacyNativeMonsterView(this);
		State = LegacyNativeMobStatus.Idle;
		BitVector = new LegacyNativeStatusFlagsBigVector32(320);
		Boss = Family.Boss;
		Facing = (LegacyNativePlayerMapFlags.ConquerAngle)LegacyNativePoolRandom.GetRandom.Next(0, 8);
	}
}

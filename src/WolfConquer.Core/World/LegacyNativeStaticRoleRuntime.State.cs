using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Genuine complete physical state and native birth; RemoveRole is genuinely empty.
internal partial class LegacyNativeStaticRoleRuntime : ILegacyNativeMapObj
{
	public LegacyNativeStatusFlagsBigVector32 BitVector;

	public uint SetBy = 0u;

	public bool Viable = false;

	private static Tuple<uint, string, ushort>[] raceItems = new Tuple<uint, string, ushort>[10]
	{
		new Tuple<uint, string, ushort>(760u, "GuardPotion", 1),
		new Tuple<uint, string, ushort>(762u, "SluggishPotion", 3),
		new Tuple<uint, string, ushort>(763u, "DizzyHammer", 1),
		new Tuple<uint, string, ushort>(764u, "RestorePotion", 3),
		new Tuple<uint, string, ushort>(765u, "ScreamBomb", 1),
		new Tuple<uint, string, ushort>(766u, "SuperExclamationMark", 1),
		new Tuple<uint, string, ushort>(767u, "SuperQuestionMark", 1),
		new Tuple<uint, string, ushort>(768u, "SpiritPotion", 1),
		new Tuple<uint, string, ushort>(769u, "ExcitementPotion", 1),
		new Tuple<uint, string, ushort>(770u, "ChaosBomb", 1)
	};

	public LegacyNativeGameMap GMap => LegacyNativePoolMaps.ServerMaps![Map];

	public bool AllowDynamic { get; set; }

	public uint IndexInScreen { get; set; }

	public string Name { get; set; }

	public uint Mesh { get; set; }

	public uint UID { get; set; }

	public byte Level { get; set; }

	public uint HitPoints { get; set; }

	public uint Map { get; set; }

	public uint DynamicID { get; set; }

	public LegacyNativeMapObjectType ObjType { get; set; }

	public ushort X { get; set; }

	public ushort Y { get; set; }

	public LegacyNativePlayerMapFlags.ConquerAction Action { get; set; }

	public LegacyNativePlayerMapFlags.ConquerAngle Facing { get; set; }

	public bool QuestionMark => Mesh == 767;


	public bool Alive => HitPoints != 0;

	public LegacyNativeStaticRoleRuntime(ushort x, ushort y, string _Namee = "Flag", uint FlagMesh = 8910u)
	{
		AllowDynamic = false;
		Name = _Namee;
		X = x;
		Y = y;
		UID = (uint)(x * 1000 + y);
		Mesh = FlagMesh;
		Level = 1;
		ObjType = LegacyNativeMapObjectType.StaticRole;
		BitVector = new LegacyNativeStatusFlagsBigVector32(320);
	}

	public bool IsTrap()
	{
		return false;
	}

	public void RemoveRole(ILegacyNativeMapObj obj)
	{
	}
}

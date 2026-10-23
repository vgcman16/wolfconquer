using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Genuine native Npc physical owner. Whole GMap/Send/interface live in a partial;
// the packet receiver remains held on its actual tournament/inventory graph.
internal partial class LegacyNativeNpcRuntime
{
	public const byte SeedDistance = 19;

	public DateTime Respawn = default(DateTime);

	public ushort Leng;

	public ushort PacketID;

	public int Stamp;

	public uint UnKnow;

	public ushort Mesh;

	public LegacyNativeNpcType NpcType;

	public ushort Sort;

	public ushort ServerID;

	private LegacyNativeGameMap? _gmap;

	public uint IndexInScreen { get; set; }

	public uint UID { get; set; }

	public ushort X { get; set; }

	public ushort Y { get; set; }

	public LegacyNativeMapObjectType ObjType { get; set; }

	public bool Alive { get; set; }

	public uint DynamicID { get; set; }

	public uint Map { get; set; }

	public bool AllowDynamic { get; set; }
}

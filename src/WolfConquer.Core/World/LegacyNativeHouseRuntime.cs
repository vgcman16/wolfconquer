using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Native-compatible House owner; loads validate complete files before publication.
// Saves validate current ownership and a complete image before one-file replacement.
// This does not transact caller-level furniture/economy state or authorize house entry.
// Npc sender/interface adoption is separate and not required by this persistence owner.
internal partial class LegacyNativeHouseRuntime
{
	public struct DBNpc
	{
		public uint UID;

		public uint UnKnow;

		public ushort X;

		public ushort Y;

		public ushort Mesh;

		public LegacyNativeNpcType NpcType;

		public LegacyNativeMapObjectType ObjType;

		public ushort Sort;

		public uint DynamicID;

		public uint Map;

		public static DBNpc Create(LegacyNativeNpcRuntime npc)
		{
			ArgumentNullException.ThrowIfNull(npc);
			return new DBNpc
			{
				UID = npc.UID,
				UnKnow = npc.UnKnow,
				X = npc.X,
				Y = npc.Y,
				Mesh = npc.Mesh,
				NpcType = npc.NpcType,
				ObjType = npc.ObjType,
				Sort = npc.Sort,
				DynamicID = npc.DynamicID,
				Map = npc.Map
			};
		}

		public static LegacyNativeNpcRuntime GetServerNpc(DBNpc Dbnpc)
		{
			LegacyNativeNpcRuntime npc = new LegacyNativeNpcRuntime();
			npc.UID = Dbnpc.UID;
			npc.UnKnow = Dbnpc.UnKnow;
			npc.X = Dbnpc.X;
			npc.Y = Dbnpc.Y;
			npc.Mesh = Dbnpc.Mesh;
			npc.NpcType = Dbnpc.NpcType;
			npc.ObjType = Dbnpc.ObjType;
			npc.Sort = Dbnpc.Sort;
			npc.DynamicID = Dbnpc.DynamicID;
			npc.Map = Dbnpc.Map;
			return npc;
		}
	}

	public static ConcurrentDictionary<uint, LegacyNativeHouseRuntime> HousePoll = new ConcurrentDictionary<uint, LegacyNativeHouseRuntime>();

	public byte Level = 1;

	public ConcurrentDictionary<uint, LegacyNativeNpcRuntime> Furnitures = new ConcurrentDictionary<uint, LegacyNativeNpcRuntime>();

	public LegacyNativeHouseRuntime(uint UID)
	{
		if (UID == 0) throw new ArgumentOutOfRangeException(nameof(UID));
		OwnerUid = UID;
		SourcePath = ConfiguredHousePath(UID);
		lock (LoadGate)
		{
			HousePoll.TryAdd(UID, this);
			if (!LegacyNativeConstants.BlockAttackMap.Contains(UID))
				LegacyNativeConstants.BlockAttackMap.Add(UID);
		}
	}

	internal static void Load(LegacyNativeGameClientRuntime client) => TryLoad(client);

}

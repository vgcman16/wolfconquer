using System.Collections.Concurrent;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Whole native House owner; genuine furniture references and BinaryFile only.
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
		if (!HousePoll.ContainsKey(UID))
		{
			HousePoll.TryAdd(UID, this);
		}
		if (!LegacyNativeConstants.BlockAttackMap.Contains(UID))
		{
			LegacyNativeConstants.BlockAttackMap.Add(UID);
		}
	}

	internal unsafe static void Load(LegacyNativeGameClientRuntime client)
	{
		if (HousePoll.ContainsKey(client.Player.UID))
		{
			client.MyHouse = HousePoll[client.Player.UID];
			return;
		}
		LegacyNativeBinaryFile binaryFile = new LegacyNativeBinaryFile();
		if (!binaryFile.Open(LegacyNativeServerConfig.DbLocation + "\\Houses\\" + client.Player.UID + ".bin", FileMode.Open))
		{
			return;
		}
		int num = 0;
		byte level = 0;
		binaryFile.Read(&level, 1);
		client.MyHouse = new LegacyNativeHouseRuntime(client.Player.UID);
		client.MyHouse.Level = level;
		binaryFile.Read(&num, 4);
		for (int i = 0; i < num; i++)
		{
			DBNpc dbnpc = default(DBNpc);
			binaryFile.Read(&dbnpc, sizeof(DBNpc));
			LegacyNativeNpcRuntime serverNpc = DBNpc.GetServerNpc(dbnpc);
			if (!client.MyHouse.Furnitures.ContainsKey(serverNpc.UID))
			{
				client.MyHouse.Furnitures.TryAdd(serverNpc.UID, serverNpc);
			}
		}
		binaryFile.Close();
	}

	internal unsafe static void Save(LegacyNativeGameClientRuntime client)
	{
		if (client.MyHouse == null)
		{
			return;
		}
		LegacyNativeBinaryFile binaryFile = new LegacyNativeBinaryFile();
		if (!binaryFile.Open(LegacyNativeServerConfig.DbLocation + "\\Houses\\" + client.Player.UID + ".bin", FileMode.Create))
		{
			return;
		}
		byte level = client.MyHouse.Level;
		int count = client.MyHouse.Furnitures.Count;
		binaryFile.Write(&level, 1);
		binaryFile.Write(&count, 4);
		foreach (LegacyNativeNpcRuntime value in client.MyHouse.Furnitures.Values)
		{
			LegacyNativeNpcRuntime npc = value;
			DBNpc dBNpc = DBNpc.Create(npc);
			binaryFile.Write(&dBNpc, sizeof(DBNpc));
		}
		binaryFile.Close();
	}
}

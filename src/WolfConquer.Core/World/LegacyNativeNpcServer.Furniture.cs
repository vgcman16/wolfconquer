using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNpcServer
{
	public class Furniture
	{
		public string? Name;

		public uint ItemID;

		public int MoneyCost;

		public uint UID;

		public ushort Mesh;
	}

	public static Dictionary<uint, Furniture> FurnitureInformations = new Dictionary<uint, Furniture>();

	public static Furniture? GetNpc(uint ItemID)
	{
		foreach (Furniture value in FurnitureInformations.Values)
		{
			if (value.ItemID == ItemID)
			{
				return value;
			}
		}
		return null;
	}

	public static Furniture? GetNpcFromMesh(uint mesh)
	{
		foreach (Furniture value in FurnitureInformations.Values)
		{
			if (value.Mesh == mesh)
			{
				return value;
			}
		}
		return null;
	}


}

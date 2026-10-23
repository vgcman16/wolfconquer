using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Whole native race row/enum and codecs; gameplay receiver remains held.
internal static partial class LegacyNativeMsgRacePotion
{
	public enum RaceItemType : ushort
	{
		Null = 8329,
		ChaosBomb,
		SpiritPotion,
		ExcitementPotion,
		FrozenTrap,
		ScreamBomb,
		SluggishPotion,
		GuardPotion,
		DizzyHammer,
		TransformItem,
		RestorePotion,
		SuperExcitementPotion
	}

	public class RacePotion
	{
		public ushort Amount;

		public RaceItemType PotionType;

		public int Location;

		public uint dwParam;
	}

	public static LegacyNativePacket CreateRecePotion(this LegacyNativePacket stream, RacePotion item)
	{
		stream.InitWriter();
		stream.Write(item.Amount);
		stream.Write((ushort)item.PotionType);
		stream.Write(item.Location);
		stream.Write(item.dwParam);
		stream.Finalize(2180);
		return stream;
	}

	public static void GetRecePotion(this LegacyNativePacket stream, out RacePotion item)
	{
		item = new RacePotion();
		item.Amount = stream.ReadUInt16();
		item.PotionType = (RaceItemType)stream.ReadUInt16();
		item.Location = stream.ReadInt32();
		item.dwParam = stream.ReadUInt32();
	}


}

using System.IO;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeChiRuntime
{
	public struct ChiAttribute
	{
		public LegacyNativeChiAttribute Type;

		public ushort Value;

		public bool Locked;

		public static ChiAttribute[] ShallowCopy(ChiAttribute[] item)
		{
			ChiAttribute[] array = new ChiAttribute[item.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = (ChiAttribute)item[i].MemberwiseClone();
			}
			return array;
		}

		public ChiAttribute(LegacyNativeChiAttribute type, ushort value, bool locked)
		{
			Type = type;
			Value = value;
			Locked = locked;
		}

		public void Serialize(BinaryWriter writer)
		{
			writer.Write((byte)Type);
			writer.Write(Value);
			writer.Write(Locked);
		}

		public void Deserialize(BinaryReader reader)
		{
			Type = (LegacyNativeChiAttribute)reader.ReadByte();
			Value = reader.ReadUInt16();
			Locked = reader.ReadBoolean();
		}

		public static implicit operator int(ChiAttribute attribute)
		{
			return (byte)attribute.Type * 10000 + attribute.Value;
		}
	}
}

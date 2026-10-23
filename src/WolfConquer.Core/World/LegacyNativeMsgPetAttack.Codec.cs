using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgPetAttack
{
	public static void GetPetAttack(this LegacyNativePacket stream, out uint TagertUID, out byte CloneCount, out uint CloneOne, out uint CloneTwo)
	{
		TagertUID = stream.ReadUInt32();
		CloneCount = stream.ReadUInt8();
		CloneOne = stream.ReadUInt32();
		CloneTwo = stream.ReadUInt32();
	}

	public static LegacyNativePacket PetAttackCreate(this LegacyNativePacket stream, uint TagertUID, byte CloneCount, uint CloneOne, uint CloneTwo)
	{
		stream.InitWriter();
		stream.Write(TagertUID);
		stream.Write(CloneCount);
		stream.Write(CloneOne);
		stream.Write(CloneTwo);
		stream.Finalize(2812);
		return stream;
	}
}

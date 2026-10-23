using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal static class LegacyNativeMsgPetInfo
{
	public static LegacyNativePacket CreatePetInfo(this LegacyNativePacket stream, uint UID, uint PetID, uint PetType, uint Mesh, ushort Range, ushort X, ushort Y, string Name)
	{
		stream.InitWriter();
		stream.Write(UID);
		stream.Write(PetID);
		stream.Write(PetType);
		stream.Write((ulong)Mesh);
		stream.Write(Range);
		stream.Write(X);
		stream.Write(Y);
		stream.Write(Name, 32);
		stream.Finalize(2192);
		return stream;
	}
}

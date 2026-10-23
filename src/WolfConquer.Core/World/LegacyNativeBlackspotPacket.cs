using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeBlackspotPacket
{
    // Whole native MsgBuilder1338..1345; source inactive=1, active=0, opcode2188.
	public static LegacyNativePacket BlackspotCreate(this LegacyNativePacket stream, bool Active, uint UID)
	{
		stream.InitWriter();
		stream.Write((!Active) ? 1u : 0u);
		stream.Write(UID);
		stream.Finalize(2188);
		return stream;
	}
}

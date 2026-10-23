using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using Player = WolfConquer.Core.World.LegacyNativePlayerRuntime;
using GroupServerList = WolfConquer.Core.World.LegacyNativeGroupServerList;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeHeroInfoPackets
{
	// Whole native MsgBuilder1702..1716, including byte Level/Rank narrowing.
	public static LegacyNativePacket LoaderHeroInfo(this LegacyNativePacket stream, Player client)
	{
		stream.InitWriter();
		stream.Write(client.UID);
		stream.Write(client.Mesh);
		stream.Write((ushort)GroupServerList.MyServerInfo!.ID);
		stream.Write((byte)client.Level);
		stream.Write((byte)client.NobilityRank);
		stream.Write(client.VipLevel);
		stream.Write(client.Class);
		stream.Write((ushort)100);
		stream.Write(client.Name);
		stream.Finalize(22210);
		return stream;
	}
}

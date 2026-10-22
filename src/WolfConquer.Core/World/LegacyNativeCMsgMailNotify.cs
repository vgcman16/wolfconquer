#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal class LegacyNativeCMsgMailNotify
{
    public static void Loading(LegacyNativeGameClientRuntime client)
    {
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        stream.InitWriter();
        stream.Write((byte)((client.MailList.Count > 0) ? 3u : 2u));
        stream.Finalize(2066);
        client.Send(stream);
    }
}

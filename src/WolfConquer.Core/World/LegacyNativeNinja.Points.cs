using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public void AddPoints(uint _Points)
    {
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        unchecked
        {
            Exp += _Points;
        }
        LegacyNativeMsgNinja.MsgNinjaUser msgNinjaUser = new LegacyNativeMsgNinja.MsgNinjaUser();
        msgNinjaUser.Type = 2u;
        msgNinjaUser.Points = Exp;
        user.Send(stream.CreateNinjaUser(msgNinjaUser));
    }
}

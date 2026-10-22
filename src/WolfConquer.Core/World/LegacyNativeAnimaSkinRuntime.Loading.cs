using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeAnimaSkinRuntime
{
    public void Loading()
    {
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        if (isEquip == 0)
        {
            LegacyNativeDragonSkin.MsgDragonSkin msgDragonSkin = new LegacyNativeDragonSkin.MsgDragonSkin();
            msgDragonSkin.Type = LegacyNativeDragonSkin.MsgDragonSkin.TypeID.Restore;
            msgDragonSkin.UserID = user.Player.UID;
            msgDragonSkin.SkinType = 0u;
            msgDragonSkin.OK = true;
            user.Send(stream.CreateAnimaSkin(msgDragonSkin));
        }
        else
        {
            LegacyNativeDragonSkin.MsgDragonSkin msgDragonSkin2 = new LegacyNativeDragonSkin.MsgDragonSkin();
            msgDragonSkin2.Type = LegacyNativeDragonSkin.MsgDragonSkin.TypeID.MyShow;
            msgDragonSkin2.UserID = user.Player.UID;
            msgDragonSkin2.SkinType = isEquip;
            user.Send(stream.CreateAnimaSkin(msgDragonSkin2));
        }
    }
}

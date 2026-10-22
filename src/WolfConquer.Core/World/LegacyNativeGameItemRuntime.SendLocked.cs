using WolfConquer.Core.Networking;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeGameItemRuntime
{
    public void SendItemLocked(LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
    {
        if (Locked == 2 && client.Player.OnMyOwnServer)
        {
            if (UnLockTimer == 0)
            {
                Locked = 0;
                Mode = Flags.ItemMode.Update;
                client.Send(ItemCreate(stream, this));
            }
            else if (DateTime.Now > LegacyNativeRoleCore.GetTimer(UnLockTimer))
            {
                Locked = 0;
                Mode = Flags.ItemMode.Update;
                client.Send(ItemCreate(stream, this));
            }
            else
            {
                client.Send(stream.ItemLockCreate(Uid, LegacyNativeItemLockPacket.TypeLock.UnlockDate, 0, (uint)UnLockTimer));
            }
        }
    }
}

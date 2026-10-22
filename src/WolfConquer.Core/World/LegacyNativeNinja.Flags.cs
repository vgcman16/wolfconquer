using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public void AddFlag(ulong flag)
    {
        if (!ContainsFlag(flag))
        {
            using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
            {
                LegacyNativePacket stream = recycledPacket.GetStream();
                Flag |= flag;
                LegacyNativeMsgNinja.MsgNinjaUser msgNinjaUser = new LegacyNativeMsgNinja.MsgNinjaUser();
                msgNinjaUser.Type = 1u;
                msgNinjaUser.Flag = Flag;
                user.Send(stream.CreateNinjaUser(msgNinjaUser));
            }
        }
    }

    public bool ContainsFlag(ulong flag)
    {
        ulong flag2 = Flag;
        flag2 &= ~flag;
        return flag2 != Flag;
    }

    public void RemoveFlag(ulong flag)
    {
        if (ContainsFlag(flag))
        {
            using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
            {
                LegacyNativePacket stream = recycledPacket.GetStream();
                Flag &= ~flag;
                LegacyNativeMsgNinja.MsgNinjaUser msgNinjaUser = new LegacyNativeMsgNinja.MsgNinjaUser();
                msgNinjaUser.Type = 1u;
                msgNinjaUser.Flag = Flag;
                user.Send(stream.CreateNinjaUser(msgNinjaUser));
            }
        }
    }
}

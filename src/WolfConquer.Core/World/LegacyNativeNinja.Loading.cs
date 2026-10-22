#nullable disable

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public void Loading()
    {
        UpdateRank();
        if (LegacyNativeAtributesStatus.IsNinja(user.Player.Class))
        {
            Alternate();
            GetLevel();
            LegacyNativeMsgNinja.MsgNinjaUser.Create(user, this, user.Player.UID);
            LegacyNativeMsgNinja.MsgNinjaItem.Create(user, this, LegacyNativeMsgNinja.MsgNinjaItem.TypeID.Load, user.Player.UID);
        }
    }
}

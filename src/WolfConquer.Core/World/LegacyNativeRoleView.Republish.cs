using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeRoleView
{
    public void ReSendView(LegacyNativePacket stream)
    {
        SendView(Owner.Player.GetArray(stream, WindowsView: false, st: false), me: false);
    }

    public void CheckScrennColors(LegacyNativePacket stream)
    {
        if (InColorScrenn)
        {
            InColorScrenn = false;
            LegacyNativeActionQuery pQuery = new LegacyNativeActionQuery
            {
                ObjId = Owner.Player.UID,
                Type = 104,
                dwParam = 0u,
                PositionX = Owner.Player.X,
                PositionY = Owner.Player.Y
            };
            Owner.Send(stream.ActionCreate(pQuery));
        }
    }
}

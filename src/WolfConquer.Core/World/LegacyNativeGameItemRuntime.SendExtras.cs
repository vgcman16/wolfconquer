using WolfConquer.Core.Networking;
using MsgItemExtra = WolfConquer.Core.World.LegacyNativeItemExtra;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeGameItemRuntime
{
    public void SendItemExtra(LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
    {
        if (Purification.ItemUID == 0 && Refinary.ItemUID == 0)
        {
            return;
        }
        MsgItemExtra msgItemExtra = new MsgItemExtra();
        if (Purification.InLife)
        {
            if (Purification.SecoundsLeft == 0)
            {
                Purification.Typ = MsgItemExtra.Typing.Stabilization;
            }
            else
            {
                Purification.Typ = MsgItemExtra.Typing.PurificationAdding;
            }
            msgItemExtra.Purifications.Add(Purification);
        }
        if (Refinary.InLife)
        {
            Refinary.Typ = MsgItemExtra.Typing.RefinaryAdding;
            if (Refinary.EffectDuration == 0)
            {
                Refinary.Typ = MsgItemExtra.Typing.PermanentRefinery;
            }
            msgItemExtra.Refinerys.Add(Refinary);
        }
        client.Send(msgItemExtra.CreateArray(stream));
    }
}

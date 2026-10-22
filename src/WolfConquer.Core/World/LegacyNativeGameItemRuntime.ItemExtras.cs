using MsgItemExtra = WolfConquer.Core.World.LegacyNativeItemExtra;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeGameItemRuntime
{
    // MsgGameItem.SendItemExtra: mutate the stored value types, then serialize
    // their list copies. InLife and SecoundsLeft retain their native clock reads.
    internal byte[]? CreateItemExtraFrame()
    {
        if (Purification.ItemUID == 0 && Refinary.ItemUID == 0)
        {
            return null;
        }

        var purifications = new List<MsgItemExtra.Purification>();
        var refinerys = new List<MsgItemExtra.Refinery>();
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

            purifications.Add(Purification);
        }

        if (Refinary.InLife)
        {
            Refinary.Typ = MsgItemExtra.Typing.RefinaryAdding;
            if (Refinary.EffectDuration == 0)
            {
                Refinary.Typ = MsgItemExtra.Typing.PermanentRefinery;
            }

            refinerys.Add(Refinary);
        }

        // Nonzero native ItemUID presence sends even when both effects expired.
        return LegacyNativeItemExtraFrame.CreateArray(purifications, refinerys);
    }
}

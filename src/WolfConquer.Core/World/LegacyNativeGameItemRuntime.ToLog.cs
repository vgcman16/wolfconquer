#nullable disable

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeGameItemRuntime
{
    public string ToLog()
    {
        return "Name: " + LegacyNativePoolItems.ItemsBase[ITEM_ID].Name + ": UID: " + Uid + " | ID: " + ITEM_ID + " | Plus: " + Plus + " | Bless: " + Bless + " | Anima: " + AnimaItemID + " | Perfection Level: " + PerfectionLevel + " | S1: " + SocketOne.ToString() + " | S2: " + SocketTwo;
    }
}

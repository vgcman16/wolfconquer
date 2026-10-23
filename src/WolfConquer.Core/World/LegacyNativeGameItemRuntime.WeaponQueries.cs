namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeGameItemRuntime
{
    // Whole native MsgGameItem106; do not replace with metadata's IsTwoHand.
    public bool IsWeapon => (LegacyNativeItemPosition.ItemPosition(ITEM_ID) == 4 ||
        LegacyNativeItemPosition.ItemPosition(ITEM_ID) == 5) && !LegacyNativeItemTypeRuneMetadata.IsArrow(ITEM_ID);

    // Whole native MsgGameItem367..384. Narrow first, then compare full quotient.
    public bool IsTwoHander()
    {
        uint num = ITEM_ID / 1000;
        bool flag = (ushort)num >= 500 && (ushort)num <= 580;
        if (flag)
        {
            flag = num != 900;
            if (flag)
            {
                flag = num != 601;
                if (flag)
                {
                    flag = num != 610;
                }
            }
        }
        return flag;
    }
}

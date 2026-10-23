namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    // Complete native Rune157..211. Count and worn-selection policies are intentionally distinct.
    public byte EquippedCount
    {
        get
        {
            byte b = 0;
            LegacyNativeGameItemRuntime[] array = objects;
            foreach (LegacyNativeGameItemRuntime msgGameItem in array)
            {
                if (!Owner!.Equipment!.Alternante)
                {
                    if (msgGameItem != null && msgGameItem.Position >= 101 && msgGameItem.Position <= 115)
                    {
                        b++;
                    }
                }
                else if (msgGameItem != null && msgGameItem.Position >= 121 && msgGameItem.Position <= 135)
                {
                    b++;
                }
            }
            return b;
        }
    }

    public LegacyNativeGameItemRuntime? EquippedRedRune
    {
        get
        {
            LegacyNativeGameItemRuntime[] wornObjects = WornObjects;
            foreach (LegacyNativeGameItemRuntime msgGameItem in wornObjects)
            {
                if (msgGameItem.Position == 101 || msgGameItem.Position == 121)
                {
                    return msgGameItem;
                }
            }
            return null;
        }
    }

    public LegacyNativeGameItemRuntime? EquippedBlueRune
    {
        get
        {
            LegacyNativeGameItemRuntime[] wornObjects = WornObjects;
            foreach (LegacyNativeGameItemRuntime msgGameItem in wornObjects)
            {
                if (msgGameItem.Position == 102 || msgGameItem.Position == 122)
                {
                    return msgGameItem;
                }
            }
            return null;
        }
    }
}

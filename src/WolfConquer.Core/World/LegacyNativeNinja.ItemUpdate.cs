namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public partial class Item
    {
        // The original native Ninja.Item.UPDATE body is genuinely empty.
        // This preserves that no-op; it does not stand in for equipment recalculation.
        public void UPDATE()
        {
        }
    }
}

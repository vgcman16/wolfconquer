namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient213/215, no eager construction at client birth.
    internal LegacyNativeVendorRuntime? MyVendor;
    internal LegacyNativeTradeRuntime? MyTrade;

    // Whole native418..440.
    internal bool IsVendor
    {
        get
        {
            if (MyVendor != null)
                return MyVendor.InVending;
            return false;
        }
    }
    internal bool InTrade
    {
        get
        {
            if (MyTrade != null)
                return MyTrade.WindowOpen;
            return false;
        }
    }
}

public sealed partial class LegacyNativePlayerRuntime
{
    // Native923.
    public uint targetTrade = 0u;
}

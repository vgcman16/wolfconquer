using System.Collections.Concurrent;
namespace WolfConquer.Core.World;

// Native Vendor10..40, including the actual constructor and source-null lifecycle slots.
internal partial class LegacyNativeVendorRuntime
{
    public class VendorItem
    {
        public LegacyNativeMsgItemView.ActionMode CostType;
        public LegacyNativeGameItemRuntime DataItem = null!;
        public long AmountCost;
    }
    public static LegacyNativeCounter VendorCounter = new LegacyNativeCounter(100000u);
    public const byte MaxItems = 20;
    public LegacyNativeGameClientRuntime Owner;
    public ConcurrentDictionary<uint, VendorItem> Items;
    public LegacyNativeMsgMessage? HalkMeesaje = null;
    public LegacyNativeSobNpcRuntime? VendorNpc;
    public uint VendorUID;
    public bool InVending;

    public LegacyNativeVendorRuntime(LegacyNativeGameClientRuntime client)
    {
        Items = new ConcurrentDictionary<uint, VendorItem>();
        Owner = client;
    }
}

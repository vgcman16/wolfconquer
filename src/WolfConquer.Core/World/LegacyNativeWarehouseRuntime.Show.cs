using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Parent intake adds only `partial` to the existing genuine sealed declaration.
internal sealed partial class LegacyNativeWarehouseRuntime
{
    public static bool IsWarehouse(LegacyNativeNpcID ID)
    {
        return ID==LegacyNativeNpcID.WHTwin || ID==LegacyNativeNpcID.wHPheonix || ID==LegacyNativeNpcID.WHMarket ||
            ID==LegacyNativeNpcID.WHBird || ID==LegacyNativeNpcID.WHDesert || ID==LegacyNativeNpcID.WHApe ||
            ID==LegacyNativeNpcID.PokerWarehouseman || ID==LegacyNativeNpcID.WHStone || ID==(LegacyNativeNpcID)65535u;
    }

    // Prepare every actual frame before any shown marker, item change or Send.
    public void Show(uint NpcID, LegacyNativeMsgWarehouse.DepositActionID Action, LegacyNativePacket stream)
    {
        lock (this)
        {
            var banks = ClientItems ?? throw new InvalidOperationException("Warehouse banks are unavailable.");
            if (!banks.TryGetValue(NpcID, out var bank)) return;
            var shown = IsShow ?? throw new InvalidOperationException("Warehouse shown state is unavailable.");
            if (shown.Contains(NpcID)) return;
            ArgumentNullException.ThrowIfNull(stream);
            var context = CaptureShow(NpcID, Action, banks, bank, shown);
            // Serialize cooperating item lock transitions in a stable UID order.
            var items = context.Entries.OrderBy(entry => entry.Key).Select(entry => entry.Value).ToArray();
            int acquired = 0;
            try
            {
                for (; acquired < items.Length; acquired++) Monitor.Enter(items[acquired]);
                using var plan = PrepareShow(context, stream);
                CommitShow(plan, stream);
            }
            finally
            {
                while (acquired > 0) Monitor.Exit(items[--acquired]);
            }
        }
    }
}

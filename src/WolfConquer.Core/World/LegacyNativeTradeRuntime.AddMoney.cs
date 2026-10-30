using WolfConquer.Core.Networking;
namespace WolfConquer.Core.World;

internal partial class LegacyNativeTradeRuntime
{
    public void AddMoney(LegacyNativeMsgTrade.Trade trade, LegacyNativePacket stream)
    {
        ArgumentNullException.ThrowIfNull(trade);
        ArgumentNullException.ThrowIfNull(stream);
        // Native Trade has no binding to the managed persisted wallet claim and pair journal.
        // Pair/window/transport references alone cannot authorize even a partial debit.
        throw new InvalidOperationException("Native trade currency requires a bound durable escrow owner.");
    }
}

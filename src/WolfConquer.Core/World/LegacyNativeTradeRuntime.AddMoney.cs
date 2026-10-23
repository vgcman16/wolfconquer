using WolfConquer.Core.Networking;
namespace WolfConquer.Core.World;

internal partial class LegacyNativeTradeRuntime
{
    // Whole native47..56. The money setter and target transport are genuine owners.
    public void AddMoney(LegacyNativeMsgTrade.Trade trade, LegacyNativePacket stream)
    {
        if (Target!.InTrade && Owner.Player.Money >= trade.Money)
        {
            Owner.Player.Money -= trade.Money;
            Money += trade.Money;
            trade.Action = 8uL;
            Target.Send(stream.CreateTradeInfo(trade));
        }
    }
}

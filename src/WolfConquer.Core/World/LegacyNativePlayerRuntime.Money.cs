using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player1063/1133 physical slots; owned publication never invokes
    // the emitting setter or its unbound warehouse/poker spill branch.
    public long WHMoney;
    private long _Money;

    // Native Player2494..2518 successful unbound poker/chat/update behavior;
    // unsafe balances are refused before those effects.
    public long Money
    {
        get
        {
            return Volatile.Read(ref _Money);
        }
        set
        {
            SetCurrentMoney(value);
        }
    }

    private void WriteUnboundMoney(long value)
    {
        if (value > 500000000000L)
        {
            long num = value - 500000000000L;
            if (WHMoney < 0) throw new InvalidOperationException("nativeMoneyWarehouseBalanceInvalid");
            long warehouseAfter = checked(WHMoney + num);
            WHMoney = warehouseAfter;
            value -= num;
            if (Owner.PokerPlayer != null)
            {
                Owner.PokerPlayer.CurrentMoney = value;
            }
            Owner.SendSysMesage(num + " Gold has transfare to your warehouse you can't hold more than 500,000,000,000 into your inventory.",
                LegacyNativeMsgMessage.ChatMode.Monster, LegacyNativeMsgMessage.MsgColor.yellow);
        }
        Volatile.Write(ref _Money, value);
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        SendUpdate(stream, value, LegacyNativeMsgUpdate.DataType.Money);
    }
}

using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player1063/1133 physical slots, no loader or snapshot projection.
    public long WHMoney;
    private long _Money;

    // Whole native Player2494..2518, including the real poker and chat path.
    public long Money
    {
        get
        {
            return _Money;
        }
        set
        {
            if (value > 500000000000L)
            {
                long num = value - 500000000000L;
                WHMoney += num;
                value -= num;
                if (Owner.PokerPlayer != null)
                {
                    Owner.PokerPlayer.CurrentMoney = value;
                }
                Owner.SendSysMesage(num + " Gold has transfare to your warehouse you can't hold more than 500,000,000,000 into your inventory.",
                    LegacyNativeMsgMessage.ChatMode.Monster, LegacyNativeMsgMessage.MsgColor.yellow);
            }
            _Money = value;
            using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
            LegacyNativePacket stream = recycledPacket.GetStream();
            SendUpdate(stream, value, LegacyNativeMsgUpdate.DataType.Money);
        }
    }
}

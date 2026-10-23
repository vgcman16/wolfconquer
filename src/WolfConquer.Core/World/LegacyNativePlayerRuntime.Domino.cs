using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player1137/1139 physical saved slots.
    private long _domino;
    private ulong _dominoc;

    // Whole native Player2553..2570.
    public long DominoCoins
    {
        get
        {
            return _domino;
        }
        set
        {
            _domino = value;
            if (Owner.FullLoading)
            {
                using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
                {
                    SendUpdate(recycledPacket.GetStream(), value, LegacyNativeMsgUpdate.DataType.DominoCoins);
                }
            }
        }
    }

    // Whole native Player2572..2582, not a currency award or bank transaction.
    public ulong DominoCode
    {
        get
        {
            return _dominoc;
        }
        set
        {
            _dominoc = value;
        }
    }
}

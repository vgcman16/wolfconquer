using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Exact native Player1131 slot and spelling.
    private uint _MyDontion;

    // Whole native Player2478..2492. Two separate one-entry updates share stream.
    public uint MyDontion
    {
        get
        {
            return _MyDontion;
        }
        set
        {
            _MyDontion = value;
            using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
            LegacyNativePacket stream = recycledPacket.GetStream();
            SendUpdate(stream, value, LegacyNativeMsgUpdate.DataType.MyDontion);
            SendUpdate(stream, value, LegacyNativeMsgUpdate.DataType.DonationPoints);
        }
    }
}

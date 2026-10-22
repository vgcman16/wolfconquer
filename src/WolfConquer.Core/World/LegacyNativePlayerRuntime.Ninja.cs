using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player351 physical backing byte; implicit zero birth.
    private byte _NiniaP0;

    // Complete native Player1328..1341; Take occurs before publishing the byte.
    public byte NiniaP0
    {
        get
        {
            return _NiniaP0;
        }
        set
        {
            using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
            _NiniaP0 = value;
            LegacyNativePacket stream = recycledPacket.GetStream();
            SendUpdate(stream, value, LegacyNativeMsgUpdate.DataType.NiniaP0);
        }
    }
}

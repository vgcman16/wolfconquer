using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player1125, implicit zero; property1617..1634 commits before sending.
    private uint _classexp;

    public uint ClassExperience
    {
        get
        {
            return _classexp;
        }
        set
        {
            _classexp = value;
            if (Owner.FullLoading)
            {
                using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
                {
                    LegacyNativePacket stream = recycledPacket.GetStream();
                    SendUpdate(stream, value, LegacyNativeMsgUpdate.DataType.ClassExperience);
                }
            }
        }
    }
}

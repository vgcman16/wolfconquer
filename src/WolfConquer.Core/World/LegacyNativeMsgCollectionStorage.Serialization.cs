using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgCollectionStorage
{
    public LegacyNativePacket ToArray()
    {
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        stream.InitWriter();
        stream.ProtoBufferSerialize(this);
        stream.Finalize(2450);
        return stream;
    }

    public static implicit operator LegacyNativePacket(LegacyNativeMsgCollectionStorage obj)
    {
        return obj.ToArray();
    }
}

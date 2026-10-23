using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Whole native Player3751..3762. Direct invocation does not test FullLoading;
    // only the still-held native HitPoints setter gates its call on FullLoading.
    public void SendUpdateHP()
    {
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, UID);
        stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.MaxHitpoints, Owner.Status.MaxHitpoints);
        stream = msgUpdate.GetArray(stream);
        Owner.Send(stream);
        stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.Hitpoints, HitPoints);
        stream = msgUpdate.GetArray(stream);
        Owner.Send(stream);
    }
}

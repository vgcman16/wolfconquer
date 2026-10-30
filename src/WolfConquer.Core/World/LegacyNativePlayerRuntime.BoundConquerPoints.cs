using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player1141 physical signed slot, implicit zero.
    private int _bountCps;

    // Whole native Player2584..2605; commit precedes the FullLoading gate.
    public int BoundConquerPoints
    {
        get
        {
            return Volatile.Read(ref _bountCps);
        }
        set
        {
            RequireUnboundCpsSetter();
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Bound ConquerPoints cannot be negative.");
            RequireCpsActorOrder();
            lock (Owner.SpellAdmissionSyncRoot)
            lock (SpellAdmissionSyncRoot)
            {
                RequireUnboundCpsSetter();
                Volatile.Write(ref _bountCps, value);
                if (Owner.FullLoading)
                {
                    using var recycledPacket = new LegacyNativeRecycledPacket();
                    LegacyNativePacket stream = recycledPacket.GetStream();
                    LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, UID);
                    stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.BoundConquerPoints, value);
                    stream = msgUpdate.GetArray(stream);
                    Owner.Send(stream);
                }
            }
        }
    }
}

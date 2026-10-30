using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player1135 UInt32 slot; ordinary hydration never invokes its
    // emitting setter or the separately unsupported spill owner.
    private uint _cps;

    public uint ConquerPoints
    {
        get => Volatile.Read(ref _cps);
        set => SetCurrentConquerPoints(value);
    }

    private void SetCurrentConquerPoints(uint value)
    {
        RequireUnboundCpsSetter();
        RequireCpsActorOrder();
        lock (Owner.SpellAdmissionSyncRoot)
        lock (SpellAdmissionSyncRoot)
        {
            RequireUnboundCpsSetter();
            // The real source spill also requires ServerDatabase.LoginQueue,
            // which has no actual owner here. Do not move bank CP or create a
            // dialog/accepting queue to simulate that missing successful path.
            if (value > 2_000_000_000U)
                throw new InvalidOperationException("nativeConquerPointsSpillOwnerUnavailable");
            Volatile.Write(ref _cps, value);
            if (Owner.FullLoading)
            {
                using var recycled = new LegacyNativeRecycledPacket();
                var stream = recycled.GetStream();
                var update = new LegacyNativeMsgUpdate(stream, UID);
                stream = update.Append(stream, LegacyNativeMsgUpdate.DataType.ConquerPoints, value);
                Owner.Send(update.GetArray(stream));
            }
        }
    }
}

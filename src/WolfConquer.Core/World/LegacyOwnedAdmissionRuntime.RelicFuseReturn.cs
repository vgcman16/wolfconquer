namespace WolfConquer.Core.World;

public sealed partial class LegacyOwnedAdmissionRuntime
{
    // A read under the actual selected Frame/Runtime/registry lifecycle; it
    // never reserves a range and does not authorize an inventory change.
    internal uint RequireRelicFuseReturnHighWater(WorldServerFrameHandler actualFrame)
    {
        lock (gate)
        {
            if (!RelicFuseFrameIsCurrent(actualFrame, out var reason)) throw new InvalidDataException(reason);
            return registry.ReadRelicFuseReturnHighWater();
        }
    }
}

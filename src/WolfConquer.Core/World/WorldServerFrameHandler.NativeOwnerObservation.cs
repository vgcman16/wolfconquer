using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Observation only: return the actual CURRENT stored record; no construction, mutation or fallback.
    internal WorldSession? ReadCurrentSessionForNativeOwnerObservation(ConquerTcpConnection connection)
    {
        return _sessions.TryGet(connection, out var current) ? current : null;
    }
}

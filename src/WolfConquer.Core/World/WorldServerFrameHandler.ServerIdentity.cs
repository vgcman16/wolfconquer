using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Native MsgLoginHandler.LoadMap1044 assigns Player.ServerID before the first
    // ServerVer send. This is that configured physical-field hook, not a new1044 receiver.
    private void PrepareLegacyLocalServerIdentity(ConquerTcpConnection connection, long snapshotUid)
    {
        if (_runtimeUpdateLoop.LocalServerIdentity is not { } identity) return;
        var initialized = false;
        var currentMatched = _sessions.WithCurrentNativeClientIdentity(connection, snapshotUid, _ =>
        {
            if (!_sessions.TryGet(connection, out var current) || current is null ||
                current.Character.Uid != snapshotUid) return;
            initialized = LegacyServerIdentityRuntime.TryInitialize(connection, current, identity);
        });
        if (!initialized)
            _log.WriteLine($"WorldServer local server identity retained fail-closed state uid={snapshotUid}; " +
                $"reason={(currentMatched ? "physicalOwnerMismatch" : "currentSessionMissingOrUidChanged")}.");
    }
}

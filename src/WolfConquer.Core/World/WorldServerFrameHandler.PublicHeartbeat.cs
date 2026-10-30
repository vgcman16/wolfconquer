using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal async ValueTask<PublicWorldSnapshot> CapturePublicHeartbeatAsync(CancellationToken cancellationToken)
    {
        try
        {
            using (await _worldOperations.EnterSharedAsync(cancellationToken).ConfigureAwait(false))
            lock (_tradeStates.SyncRoot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var recovery = _tradeRecovery.ReadPublicHeartbeatRecovery();
                int? count = _sessions.TryCountPublicHeartbeatSessions(_tradeStates, out var actual) ? actual : null;
                if (_snapshotLoader is null || count is null) recovery = PublicRecoveryState.Unavailable;
                return new(_protocolProfile.ToString(), count, recovery);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            InvalidOperationException or ArgumentException or System.Security.SecurityException)
        {
            return new(_protocolProfile.ToString(), null, PublicRecoveryState.Unavailable);
        }
    }
}

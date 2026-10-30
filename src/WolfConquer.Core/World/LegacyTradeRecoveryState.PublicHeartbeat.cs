using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyTradeRecoveryState
{
    internal PublicRecoveryState ReadPublicHeartbeatRecovery()
    {
        lock (_gate)
        {
            if (_failureReason is not null) return PublicRecoveryState.Required;
            if (!_initialized) return PublicRecoveryState.Starting;
            if (string.IsNullOrWhiteSpace(usersRoot)) return PublicRecoveryState.Unavailable;
            // Observation only: do not initialize, replay or alter the live fence.
            return LegacyTradeJournal.HasPending(usersRoot) ? PublicRecoveryState.Required : PublicRecoveryState.Ready;
        }
    }
}

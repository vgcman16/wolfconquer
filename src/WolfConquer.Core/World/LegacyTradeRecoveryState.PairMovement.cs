namespace WolfConquer.Core.World;

internal sealed partial class LegacyTradeRecoveryState
{
    internal bool IsReadyForPairMovement(string? expectedUsersRoot)
    {
        lock (_gate)
        {
            if (_admissionRoots is not { } configured || !IsReadyForUsers(expectedUsersRoot)) return false;
            try
            {
                return SameReadyRoot(configured.Users, expectedUsersRoot!) &&
                    !LegacyTradeJournal.HasPending(configured.Users);
            }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or
                System.Security.SecurityException or IOException or UnauthorizedAccessException)
            { return false; }
        }
    }
}

namespace WolfConquer.Core.World;

internal sealed partial class LegacyTradeRecoveryState
{
    // Explicit leased startup refuses any retained joint journal; it never
    // replays a newly appeared record merely to manufacture readiness.
    internal bool TryInitializeOwnedAdmission(LegacyOrdinaryAdmissionRoots expected, out string reason)
    {
        lock (_gate)
        {
            reason = _failureReason ?? "ownedAdmissionRecoveryUnavailable";
            if (_failureReason is not null || _admissionRoots != expected) return false;
            try
            {
                if (LegacyTradeJournal.HasPending(expected.Users))
                { reason = "ownedAdmissionJointJournalRecoveryRequired"; Block(reason); return false; }
                if (!_initialized && !LegacyTradeEscrowClaims.TryRecoverAll(expected.Users, out _, out reason))
                { Block(reason); return false; }
                if (LegacyTradeJournal.HasPending(expected.Users))
                { reason = "ownedAdmissionJointJournalRecoveryRequired"; Block(reason); return false; }
                _initialized = true; reason = string.Empty; return true;
            }
            catch (Exception error) when (LegacyOwnedAdmissionOptions.IsConfigurationFailure(error))
            { reason = "ownedAdmissionRecoveryRefused:" + error.GetType().Name; Block(reason); return false; }
        }
    }
}

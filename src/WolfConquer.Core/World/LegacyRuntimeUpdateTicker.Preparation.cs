namespace WolfConquer.Core.World;

public sealed partial class LegacyRuntimeUpdateTicker
{
    // DATA preparation only. The private Frame worker retains this copy only
    // after its actual current-owner Store publication succeeds.
    internal LegacyRuntimeUpdateTicker CopyForPreparation() => new(this);

    private LegacyRuntimeUpdateTicker(LegacyRuntimeUpdateTicker source)
    {
        _uid = source._uid;
        _statusFlags = (ulong[])source._statusFlags.Clone();
        _hasRuntimeStatusFlags = source._hasRuntimeStatusFlags;
        _hasOverwhelmRune = source._hasOverwhelmRune;
        _hasRevelatorStartupEffect = source._hasRevelatorStartupEffect;
        _topInHoursInvitations = source._topInHoursInvitations.CopyForPreparation();
        _nextXpAt = source._nextXpAt;
        _lastStaminaAt = source._lastStaminaAt;
        _nextOnlinePointsAt = source._nextOnlinePointsAt;
        _xpListExpiresAt = source._xpListExpiresAt;
        _xpCount = source._xpCount;
        _maximumStamina = source._maximumStamina;
        _stamina = source._stamina;
        _sentStatusFlags = source._sentStatusFlags;
        _xpListActive = source._xpListActive;
        _isSitting = source._isSitting;
    }
}

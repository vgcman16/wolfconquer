namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal TimeProvider RequireOwnedHawksAmbitionClock(LegacyOrdinarySpellBinding binding) =>
        framePersistenceOwner?.RequireOwnedHawksAmbitionClock(this, binding) ??
        throw new InvalidOperationException("ownedHawksAmbitionRegisteredFrameUnavailable");

    internal bool IsOwnedHawksAmbitionClockCurrentPlain(LegacyOrdinarySpellBinding binding, TimeProvider clock) =>
        framePersistenceOwner?.IsOwnedHawksAmbitionClockCurrentPlain(this, binding, clock) == true;
}

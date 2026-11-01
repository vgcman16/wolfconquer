namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal TimeProvider RequireOwnedHawksAmbitionClock(WorldSessionStore store, LegacyOrdinarySpellBinding binding)
    {
        var clock = _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System;
        if (!IsOwnedHawksAmbitionClockCurrentPlain(store, binding, clock))
            throw new InvalidOperationException("ownedHawksAmbitionClockOwnerUnavailable");
        return clock;
    }

    internal bool IsOwnedHawksAmbitionClockCurrentPlain(WorldSessionStore store, LegacyOrdinarySpellBinding binding,
        TimeProvider clock) => ReferenceEquals(clock, _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System) &&
        IsOwnedPhysicalMetadataCatalogCurrent(store, binding, binding.ItemCatalog);
}

namespace WolfConquer.Core.World;

public sealed record LegacyRuntimeUpdateLoopOptions(
    bool Enabled,
    TimeSpan TickInterval,
    TimeProvider TimeProvider,
    ILegacySignedMillisecondClock? LegacyClock = null,
    ILegacySignedMillisecondClock? LegacyEnvironmentClock = null)
{
    // Explicit local MyServerInfo identity; absent configuration never issues an
    // on-own-server owner or changes the physical Player's ServerID.
    public LegacyLocalServerIdentity? LocalServerIdentity { get; init; }

    public static LegacyRuntimeUpdateLoopOptions Disabled { get; } =
        new(Enabled: false, TimeSpan.FromSeconds(1), TimeProvider.System);

    public static LegacyRuntimeUpdateLoopOptions EnabledDefault { get; } =
        new(Enabled: true, TimeSpan.FromSeconds(1), TimeProvider.System);
}

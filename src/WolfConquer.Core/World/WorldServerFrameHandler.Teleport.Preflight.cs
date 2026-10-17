namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private ValueTask<LegacyTeleportPreflightOutcome> PrepareLegacyRuntimeTeleportAsync(
        WorldActiveSession active,
        LegacyTeleportRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = CreateMonsterRuntimeContext(active) with
        {
            ElitePkStates = _elitePkStates,
            TeamPkStates = _teamPkStates,
            DragonWarStates = _dragonWarStates,
            FreezeWarStates = _freezeWarStates,
        };
        return WorldPacketHandlers.PrepareLegacyTeleportAsync(
            context,
            active.Session,
            request,
            cancellationToken);
    }
}

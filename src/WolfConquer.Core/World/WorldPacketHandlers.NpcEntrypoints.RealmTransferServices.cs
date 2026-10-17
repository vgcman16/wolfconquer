namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    public static async ValueTask<bool> TryHandleLegacyRealmTransferServicesNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyRealmTransferServicesServerTransferNpcId => await TryHandleLegacyRealmTransferServicesServerTransferAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyRealmTransferServicesGoldPrizeTokenNpcId => await TryHandleLegacyRealmTransferServicesGoldPrizeTokenAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyRealmTransferServicesSashFragmentRealmNpcId => await TryHandleLegacyRealmTransferServicesSashFragmentAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyRealmTransferServicesMrRayzoEuNpcId => await TryHandleLegacyRealmTransferServicesKeeperAsync(
                    context,
                    LegacyRealmTransferServicesMrRayzoEuNpcId,
                    optionId,
                    "NpcHandler.MrRayzo_EU",
                    "24258-24303",
                    "EU",
                    1,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyRealmTransferServicesMrRayzoUsNpcId => await TryHandleLegacyRealmTransferServicesKeeperAsync(
                    context,
                    LegacyRealmTransferServicesMrRayzoUsNpcId,
                    optionId,
                    "NpcHandler.MrRayzo_US",
                    "24306-24351",
                    "US",
                    2,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyRealmTransferServicesRealmEnvoyNpcId => await TryHandleLegacyRealmTransferServicesRealmEnvoyAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyRealmTransferServicesKingdomMissionEnvoyNpcId => await TryHandleLegacyRealmTransferServicesKingdomMissionEnvoyAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyRealmTransferServicesGreenSnakeNpcId => await TryHandleLegacyRealmTransferServicesGreenSnakeAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyRealmTransferServicesVoteManagerNpcId => await TryHandleLegacyRealmTransferServicesVoteManagerAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyRealmTransferServicesFootballNpcId => await TryHandleLegacyRealmTransferServicesFootballAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyRealmTransferServicesNorbertNpcId => await TryHandleLegacyRealmTransferServicesNorbertAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyRealmTransferServicesDrYinYangNpcId => await TryHandleLegacyRealmTransferServicesDrYinYangAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }
}

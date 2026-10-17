namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyTeleportallAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.Teleportall",
                    [LegacyTeleportallDialogText],
                    LegacyTeleportallOptions,
                    LegacyTeleportallAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 4)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.Teleportall",
                    [LegacyTeleportallCityDialogText],
                    LegacyTeleportallCityOptions,
                    LegacyTeleportallAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (LegacyTeleportallDestinations.TryGetValue(optionId, out var destination))
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    session,
                    LegacyTeleportallNpcId,
                    optionId,
                    destination,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }
}

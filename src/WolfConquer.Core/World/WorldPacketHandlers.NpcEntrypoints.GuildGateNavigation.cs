namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyGuildGateNavigationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId != LegacyGuildGateKeeperNpcId)
        {
            return false;
        }

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.GuildGateKeeper",
                    [LegacyGuildGateKeeperDialogText],
                    LegacyGuildGateKeeperOptions,
                    avatarId: 7,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    new LegacyNpcTeleportDestination(1_002, 410, 354, "NpcHandler.GuildGateKeeper"),
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }
}

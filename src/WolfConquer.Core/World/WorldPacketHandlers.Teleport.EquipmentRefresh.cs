namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask SendLegacyTeleportGuildWarEquipmentRefreshAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (session.Character.MapId == 1_038)
        {
            await SendPlayerEquipmentAppearanceRefreshAsync(context, session, cancellationToken)
                .ConfigureAwait(false);
        }

        if (session.Character.PreviousMapId == 1_038)
        {
            await SendPlayerEquipmentAppearanceRefreshAsync(context, session, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    public static async ValueTask<bool> TryHandleLegacyRewardServicesNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyShopRilecNpcId => await TryHandleLegacyShopRilecAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyCliamCupNpcId => await TryHandleLegacyCliamCupAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyRelicNpcId => await TryHandleLegacyRelicAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyHosuuNpcId => await TryHandleLegacyHosuuAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyRewardPointsNpcId => await TryHandleLegacyRewardPointsAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyJiangMakerNpcId => await TryHandleLegacyJiangMakerAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }
}

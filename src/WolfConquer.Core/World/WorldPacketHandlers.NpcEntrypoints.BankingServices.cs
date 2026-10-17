namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    public static async ValueTask<bool> TryHandleLegacyBankingServicesNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string input,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyBankCpsNpcId => await TryHandleLegacyBankCpsAsync(
                    context,
                    session,
                    optionId,
                    input,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyMarketCpAdminNpcId => await TryHandleLegacyCpExchangeAdminAsync(
                    context,
                    session,
                    LegacyMarketCpAdminSource,
                    optionId,
                    hasChiExchange: true,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyJailCpAdminNpcId => await TryHandleLegacyCpExchangeAdminAsync(
                    context,
                    session,
                    LegacyJailCpAdminSource,
                    optionId,
                    hasChiExchange: false,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }
}

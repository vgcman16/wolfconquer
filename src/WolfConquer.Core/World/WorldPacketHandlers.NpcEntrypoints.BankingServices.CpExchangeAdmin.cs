namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyCpExchangeAdminAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        bool hasChiExchange,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyCpExchangeAdminRootAsync(
                        context,
                        sourceMethod,
                        hasChiExchange,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["You can exchange money and CPs here."],
                        [
                            new LegacyNpcDialogOption(
                                sourceMethod == LegacyJailCpAdminSource
                                    ? "Exchange 10,000,000 silver for 100 CPs."
                                    : "Exchange 1,000,000 CPs for 100,000,000 silver.",
                                4),
                            new LegacyNpcDialogOption(
                                sourceMethod == LegacyJailCpAdminSource
                                    ? "Exchange 100,000,000 silver for 1,000 CPs."
                                    : "Exchange 100,000,000 CPs for 1,000,000,000 silver.",
                                5),
                            new LegacyNpcDialogOption("No thanks.", byte.MaxValue),
                        ],
                        avatarId: 7,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
            case 2:
            case 4:
            case 5:
            case 9:
            case 10:
                return await TryApplyLegacyCpExchangeAdminMutationAsync(
                        context,
                        session,
                        sourceMethod,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);

            default:
                return false;
        }
    }

    private static async ValueTask SendLegacyCpExchangeAdminRootAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        bool hasChiExchange,
        CancellationToken cancellationToken)
    {
        var options = new List<LegacyNpcDialogOption>
        {
            new("Exchange a DB Scroll.", 1),
            new("Exchange a Dragon Ball.", 2),
        };
        if (hasChiExchange)
        {
            options.Add(new LegacyNpcDialogOption("Exchange a Dragon Ball for Chi Points.", 9));
            options.Add(new LegacyNpcDialogOption("Exchange a DB Scroll for Chi Points.", 10));
        }

        options.Add(new LegacyNpcDialogOption(
            sourceMethod == LegacyJailCpAdminSource
                ? "Exchange money for CPs."
                : "Exchange CPs for money.",
            3));
        options.Add(new LegacyNpcDialogOption("I have nothing.", byte.MaxValue));

        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [
                    "Dragon Balls are powerful. I can exchange Dragon Balls or DB Scrolls for CPs.",
                    hasChiExchange
                        ? "I can also exchange Dragon Balls or DB Scrolls for Chi Points."
                        : "I can also exchange silver and CPs.",
                ],
                options,
                avatarId: 7,
                cancellationToken)
            .ConfigureAwait(false);
    }
}

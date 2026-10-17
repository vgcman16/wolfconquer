namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySocialCraftingServicesLuckySmeltingListNpcId = 23_582;

    private const string LegacySocialCraftingServicesLuckySmeltingListSource = "NpcHandler.LuckySmeltingList";

    private const string LegacySocialCraftingServicesLuckySmeltingListLines = "54937-54987";

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesLuckySmeltingListAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId is not 0 and not 1)
        {
            return false;
        }

        await SendLegacySocialCraftingServicesGapAsync(
                context,
                session,
                LegacySocialCraftingServicesLuckySmeltingListNpcId,
                optionId,
                LegacySocialCraftingServicesLuckySmeltingListSource,
                LegacySocialCraftingServicesLuckySmeltingListLines,
                "Lucky Smelting List reads legacy global smelting success/session state that is not modeled in this runtime",
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }
}

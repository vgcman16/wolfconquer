namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySocialCraftingServicesOfflineTgNpcId = 3_836;

    private const string LegacySocialCraftingServicesOfflineTgSource = "NpcHandler.OflineTGNpc";

    private const string LegacySocialCraftingServicesOfflineTgLines = "53826-53852";

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesOfflineTgAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId != 0)
        {
            return false;
        }

        await SendLegacySocialCraftingServicesGapAsync(
                context,
                session,
                LegacySocialCraftingServicesOfflineTgNpcId,
                optionId,
                LegacySocialCraftingServicesOfflineTgSource,
                LegacySocialCraftingServicesOfflineTgLines,
                "offline TG reward projection requires JoinOfflineTG time, legacy level-exp tables, CalcExpBall, and OfflineTGStats packet parity",
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }
}

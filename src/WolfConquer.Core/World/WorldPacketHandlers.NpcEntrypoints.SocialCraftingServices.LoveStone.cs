namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySocialCraftingServicesLoveStoneNpcId = 390;

    private const string LegacySocialCraftingServicesLoveStoneSource = "NpcHandler.LoveStone";

    private const string LegacySocialCraftingServicesLoveStoneLines = "52913-53035";

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesLoveStoneAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesLoveStoneSource,
                        ["Fate can bring lovers together. I hope you can marry and live happily with your love. What can I do for you?"],
                        [
                            new("Claim double EXP.", 1),
                            new("I want to get married.", 2),
                            new("Can you help me divorce?", 3),
                            new("Nothing special.", 4),
                        ],
                        6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesLoveStoneSource,
                        ["Fate can bring lovers together, and everyone can live happily ever after. What can I do for you?"],
                        [new("I want to get married.", 5)],
                        6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesLoveStoneSource,
                        ["If your marriage left only regret and pain, the legacy source asks for 3 Meteors before divorce."],
                        [new("I have made up my mind.", 8), new("Let me reconsider.", LegacyNpcClosedOption)],
                        6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 5:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesLoveStoneSource,
                        ["Marriage is a promise forever. Are you sure you want to spend the rest of your life with your love?"],
                        [new("I want to get married.", 6), new("I prefer being single.", LegacyNpcClosedOption)],
                        6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 6:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesLoveStoneSource,
                        ["Are you ready to propose?"],
                        [new("Yes. I am ready.", 7), new("Let me think it over.", LegacyNpcClosedOption)],
                        6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 4:
                await context.Log.WriteLineAsync(
                        "WorldServer ignored LegacyNpc SocialCraftingServices no-op " +
                        $"{FormatSession(context)} source={LegacySocialCraftingServicesLoveStoneSource} " +
                        $"sourceLines={LegacySocialCraftingServicesLoveStoneLines} npcId={LegacySocialCraftingServicesLoveStoneNpcId} optionId={optionId}.")
                    .ConfigureAwait(false);
                return true;

            case 1:
            case 7:
            case 8:
                await SendLegacySocialCraftingServicesGapAsync(
                        context,
                        session,
                        LegacySocialCraftingServicesLoveStoneNpcId,
                        optionId,
                        LegacySocialCraftingServicesLoveStoneSource,
                        LegacySocialCraftingServicesLoveStoneLines,
                        "marriage proposal, divorce, and double EXP blessing mutate persistent spouse or blessing state and require client action flows",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }
}

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySocialCraftingServicesJiangHuNpcId = 15_745;

    private const string LegacySocialCraftingServicesJiangHuSource = "NpcHandler.JiangHuNpc";

    private const string LegacySocialCraftingServicesJiangHuLines = "52249-52316";

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesJiangHuAsync(
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
                        LegacySocialCraftingServicesJiangHuSource,
                        ["The Jiang Hu is chaotic. Answer my questions, name a skill of your own, and begin your adventure as a Jiang Hu warrior."],
                        [
                            new("Okay. I am ready!", 1),
                            new("Learn about the Jiang Hu.", 2),
                            new("I will talk to you later.", LegacyNpcClosedOption),
                        ],
                        6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacySocialCraftingServicesGapAsync(
                        context,
                        session,
                        LegacySocialCraftingServicesJiangHuNpcId,
                        optionId,
                        LegacySocialCraftingServicesJiangHuSource,
                        LegacySocialCraftingServicesJiangHuLines,
                        "JiangHu activation opens client action 617 and depends on second rebirth plus persistent JiangHu state",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesJiangHuSource,
                        [
                            "After activating Jiang mode, each study costs 10 Study Points and one Free Course, and lowers Talent Status by one.",
                            "You can perform up to 10 free studies each day. At Basic Status, extra study requires CPs.",
                        ],
                        [
                            new("How do I raise Talent Status?", 3),
                            new("How do I collect Free Courses?", 4),
                            new("What if I want to quit?", 5),
                            new("I will talk to you later.", LegacyNpcClosedOption),
                        ],
                        6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesJiangHuSource,
                        ["Activate Jiang mode and defeat a player whose Talent Status is not lower than yours to raise your status by one level, up to level 4."],
                        [new("Learn about other things.", 2), new("I will talk to you later.", LegacyNpcClosedOption)],
                        6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 4:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesJiangHuSource,
                        ["Jiang Hu fighters gain Free Courses by accumulating Talent Points over time. Twin City grants far more Talent Points while you are online."],
                        [new("Learn about other things.", 2), new("I will talk to you later.", LegacyNpcClosedOption)],
                        6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 5:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesJiangHuSource,
                        ["To leave Jiang Hu for a while, deactivate Jiang mode and wait 10 minutes. You can return whenever you want."],
                        [new("Learn about other things.", 2), new("I will talk to you later.", LegacyNpcClosedOption)],
                        6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }
}

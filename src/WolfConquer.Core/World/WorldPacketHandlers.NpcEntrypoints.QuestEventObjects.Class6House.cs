namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly LegacyNpcDialogOption[] LegacyClass6HouseRootOptions =
    [
        new("Upgrade my house with CPs.", 1),
        new("Use materials to upgrade.", 3),
        new("How do I upgrade my house?", 6),
        new("Where is the exit?", 2),
        new("Just passing by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyClass6HouseMaterialOptions =
    [
        new("100.", 4),
        new("500.", 5),
        new("Let me see.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> TryHandleLegacyClass6HouseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.Class6House";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "Welcome. This is the Class 6 model house with many new features. While you walk around, you can talk to our guides in each area to learn more.",
                            "If you want to upgrade your house to Class 6, I am the right person to serve you. You will need 99,000 Conquer Points to upgrade your house to the next level.",
                        ],
                        LegacyClass6HouseRootOptions,
                        LegacyClass6HouseAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 6:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "There are two ways to upgrade to Class 6: pay 8,999 CPs or submit 500 Class 6 House Packs.",
                            "You can collect Class 6 House Packs from Dis City, Evil Labyrinth, cross-server competitions, other quests, or buy them with Honor Points, Horse Racing Points, or Champion Points.",
                        ],
                        LegacyQuestEventOkayOptions,
                        LegacyClass6HouseAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["We need 500 Class 6 House Packs to upgrade your house to Class 6. How many packs would you like to submit this time?"],
                        LegacyClass6HouseMaterialOptions,
                        LegacyClass6HouseAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyClass6HouseNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_036, 200, 95, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                if (session.Character.ConquerPoints < 99_000)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            sourceMethod,
                            ["Sorry, I cannot help you because you do not have enough CPs. Please bring the money next time."],
                            LegacyQuestEventOkayOptions,
                            LegacyClass6HouseAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await SendLegacyClass6HouseUpgradeGapAsync(
                            context,
                            sourceMethod,
                            optionId,
                            "Class 6 house CP upgrading is not available in this runtime yet.",
                            "source checks trade state, MyHouse, current house occupants, furniture count, house level, mutates ConquerPoints and MyHouse.Level, broadcasts globally, teleports, and plays effect movego; local house persistence/runtime state is not modeled.",
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                return true;

            case 4:
                if (!HasLegacyInventoryStackCount(session.PlayerItems, 3_008_100, 100))
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            sourceMethod,
                            ["I am afraid I cannot help you if you do not have enough materials. Please collect more."],
                            LegacyQuestEventOkayOptions,
                            LegacyClass6HouseAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await SendLegacyClass6HouseUpgradeGapAsync(
                            context,
                            sourceMethod,
                            optionId,
                            "Class 6 house material upgrading is not available in this runtime yet.",
                            "source removes 100 Class 6 House Packs before a random 30 percent upgrade attempt, then mutates MyHouse.Level and teleports occupants; local house runtime state and destructive partial-material consumption are not modeled safely.",
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                return true;

            case 5:
                if (!HasLegacyInventoryStackCount(session.PlayerItems, 3_008_100, 500))
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            sourceMethod,
                            ["I am afraid I cannot help you if you do not have enough materials. Please collect more."],
                            LegacyQuestEventOkayOptions,
                            LegacyClass6HouseAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await SendLegacyClass6HouseUpgradeGapAsync(
                            context,
                            sourceMethod,
                            optionId,
                            "Class 6 house material upgrading is not available in this runtime yet.",
                            "source checks MyHouse, current house occupants, furniture count, house level, removes 500 Class 6 House Packs, broadcasts globally, teleports, and plays effect movego; local house persistence/runtime state is not modeled.",
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                return true;

            default:
                await SendLegacyClass6HouseUpgradeGapAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "That Class 6 house option is not available in this runtime yet.",
                        "source branch is not part of the deterministic Class6House parity slice.",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
        }
    }

    private static async ValueTask SendLegacyClass6HouseUpgradeGapAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string dialogText,
        string reason,
        CancellationToken cancellationToken)
    {
        await SendLegacyQuestEventObjectGapAsync(
                context,
                sourceMethod,
                optionId,
                dialogText,
                reason,
                LegacyClass6HouseAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }
}

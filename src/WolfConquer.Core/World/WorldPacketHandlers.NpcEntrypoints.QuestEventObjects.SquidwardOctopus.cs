namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly LegacyNpcDialogOption[] LegacySquidwardOctopusConfirmOptions =
    [
        new("Do not worry. I am ready.", 31),
        new("I will be back soon.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySquidwardOctopusRewardOptions =
    [
        new("What do you mean by limited?", 21),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySquidwardOctopusLimitedOptions =
    [
        new("I know. Time is money.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySquidwardOctopusTreasureOptions =
    [
        new("What is Treasure in the Blue?", 2),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySquidwardOctopusTreasure2Options =
    [
        new("What are the treasures?", 3),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySquidwardOctopusTreasure3Options =
    [
        new("Special sea monsters?", 4),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySquidwardOctopusTreasure4Options =
    [
        new("So, what do you say? Deal?", 5),
        new("I will think it over.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySquidwardOctopusTreasure5Options =
    [
        new("Any tips?", 6),
    ];

    private static async ValueTask<bool> TryHandleLegacySquidwardOctopusAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.SquidwardOctopus";

        switch (optionId)
        {
            case 0:
                await SendLegacyQuestEventObjectGapAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Treasure in the Blue signup is not available in this runtime yet.",
                        "source branches on MsgSchedules.SquidwardOctopus.Process; local global event process state is not represented.",
                        LegacySquidwardOctopusAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 30:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["It seems you have made up your mind. The Proud Sea is dangerous, so please check your inventory before you go."],
                        LegacySquidwardOctopusConfirmOptions,
                        LegacySquidwardOctopusAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 31:
                if (session.Character.Level < 80)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            sourceMethod,
                            ["You are still too young. Keep practicing and come back when you reach Level 80."],
                            LegacyQuestEventOkayOptions,
                            LegacySquidwardOctopusAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await SendLegacyQuestEventObjectGapAsync(
                            context,
                            sourceMethod,
                            optionId,
                            "The Proud Sea teleport is not available in this runtime yet.",
                            "source requires MsgSchedules.SquidwardOctopus.Process == Alive and randomly chooses one of four Proud Sea destinations on map 3071; local global event state and random event placement are not modeled.",
                            LegacySquidwardOctopusAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                return true;

            case 20:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "Trust me, you will be satisfied with the rewards. Gold Coins can be exchanged for a Small Lottery Ticket and a chance at rare mount armor.",
                            "Silver Coins can bring an Endurance Book and a small gift. Copper Coins bring a mysterious gift. Be quick, because rewards are limited.",
                        ],
                        LegacySquidwardOctopusRewardOptions,
                        LegacySquidwardOctopusAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 21:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "Our team has limited rewards: Gold Coins can be exchanged up to 100 times, Silver Coins up to 200 times, and Copper Coins up to 400 times.",
                            "When the rewards are gone, or one hour passes after the event begins, we stop accepting exchanges.",
                        ],
                        LegacySquidwardOctopusLimitedOptions,
                        LegacySquidwardOctopusAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Our ship sends treasure hunters to the Proud Sea from 12:30-13:29 and 20:30-21:29, Monday through Saturday. If you have reached Level 80, I would be glad to count you in."],
                        LegacySquidwardOctopusTreasureOptions,
                        LegacySquidwardOctopusAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["That is what I want too, but you came too late and all the gifts have been claimed. Come earlier next time."],
                        LegacySquidwardOctopusTreasure2Options,
                        LegacySquidwardOctopusAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Stop kidding me. You do not have any Gold Coins with you. Do I look like a fool?"],
                        LegacySquidwardOctopusTreasure3Options,
                        LegacySquidwardOctopusAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 4:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Really? You have a Silver Coin? If you give it to me, you will receive an Endurance Book. Not enough? How about a Weapon Accessory Pack or Mount Armor?"],
                        LegacySquidwardOctopusTreasure4Options,
                        LegacySquidwardOctopusAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 5:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Mammon Envoy is my partner in the Prize Center. Any of the eight Proud Sea teleporters can send you there, but after returning you must wait three minutes to use one again."],
                        LegacySquidwardOctopusTreasure5Options,
                        LegacySquidwardOctopusAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 6:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["You can get coins from other treasure hunters, but they can target you too. PK points are not counted in the Proud Sea, but inventory items may drop if you are killed."],
                        LegacyQuestEventOkayOptions,
                        LegacySquidwardOctopusAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                await SendLegacyQuestEventObjectGapAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "That Treasure in the Blue option is not available in this runtime yet.",
                        "source branch is not part of the deterministic SquidwardOctopus parity slice.",
                        LegacySquidwardOctopusAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
        }
    }
}

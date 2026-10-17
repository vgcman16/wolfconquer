using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySimonNpcId = 1_152;
    private const uint LegacyLabyrinthFloor1NpcId = 1_153;
    private const uint LegacyLabyrinthFloor2NpcId = 1_154;
    private const uint LegacyLabyrinthFloor3NpcId = 1_155;
    private const uint LegacyLabyrinthFloor4NpcId = 1_156;
    private const uint LegacyStanleyNpcId = 3_623;
    private const uint LegacyFrozenFroto4TeleporterNpcId = 13_124;
    private const uint LegacyFrozenGrottoGuardian5NpcId = 13_125;
    private const uint LegacyDesertFrozenGrotoNpcId = 21_116;

    private const uint LegacyLabyrinthSkyTokenItemId = 721_537;
    private const uint LegacyLabyrinthEarthTokenItemId = 721_538;
    private const uint LegacyLabyrinthSoulTokenItemId = 721_539;
    private const uint LegacyLabyrinthSunDiamondItemId = 721_533;
    private const uint LegacyLabyrinthMoonDiamondItemId = 721_534;
    private const uint LegacyLabyrinthStarDiamondItemId = 721_535;
    private const uint LegacyLabyrinthCloudDiamondItemId = 721_536;
    private const uint LegacyLabyrinthMeteorItemId = 1_088_001;
    private const uint LegacyFrozenGrottoKeyItemId = 721_347;
    private const uint LegacyShadowJadeItemId = 720_599;
    private const ushort LegacyLabyrinthGeneralAvatarId = 31;
    private const ushort LegacyLabyrinthQuestAvatarId = 63;
    private const ushort LegacyFrozenGrottoAvatarId = 31;
    private const ushort LegacyDesertFrozenGrottoAvatarId = 183;
    private const ushort LegacyFrozenGrottoItemAvatarId = 0;

    private static readonly LegacyNpcTeleportDestination LegacyLabyrinthTwinCityDestination =
        new(1_002, 410, 354, "NpcHandler.LabyrinthTwinCity");

    private static readonly LegacyNpcTeleportDestination LegacyLabyrinthEntranceDestination =
        new(1_351, 18, 127, "NpcHandler.Simon");

    private static readonly LegacyNpcDialogOption[] LegacyLabyrinthReturnOptions =
    [
        new("To~Twin~City.", 2),
        new("Let~me~think...", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyLabyrinthStayOptions =
    [
        new("I~will~stay~here.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySimonRootOptions =
    [
        new("Please~tell~me~more.", 1),
        new("What~rewards?", 10),
        new("I~got~SunDiamonds.", 12),
        new("I~got~MoonDiamonds.", 13),
        new("I~got~StarDiamonds.", 14),
        new("I~got~CloudDiamonds.", 15),
        new("Let~me~think...", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySimonRewardNeedOptions =
    [
        new("What~rewards?", 11),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySimonStoryStep1Options =
    [
        new("Wow,~miserable.", 2),
        new("I~have~no~interest.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySimonStoryStep2Options =
    [
        new("Please~continue.", 3),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySimonStoryStep3Options =
    [
        new("What~do~they~drop?", 4),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySimonStoryStep4Options =
    [
        new("I~see.~Thanks.", 5),
        new("I~must~leave~now.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySimonEntryOptions =
    [
        new("Give~you~2000~Virtue~Points.", 6),
        new("I~have~5~Meteors.", 7),
        new("VIP~service.", 8),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyLabyrinthFloorRootOptions =
    [
        new("To~the~next~floor.", 1),
        new("To~Twin~City.", 3),
        new("I~will~stay~here.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyLabyrinthNextFloorConfirmOptions =
    [
        new("Thank~you~very~much.", 2),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyFrozenGrottoGuardian5Options =
    [
        new("How~can~I~go~to~FG6?", 1),
        new("Give~me~a~Shadow~Jade.", 2),
        new("Send~me~to~FG4.", 3),
        new("Let~me~think...", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyFrozenFroto4TeleporterOptions =
    [
        new("Hey,~I`ve~got~a~key~here.", 1),
        new("Frozen~Grotto~F5.", 2),
        new("Let~me~think...", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDesertFrozenGrotoOptions =
    [
        new("Frozen~Grotto~1.", 1),
        new("Frozen~Grotto~2.", 2),
        new("Let~me~think...", LegacyNpcClosedOption),
    ];

    internal static async ValueTask<bool> TryHandleLegacyLabyrinthNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyStanleyNpcId => await TryHandleLegacyStanleyAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyLabyrinthFloor1NpcId or
                LegacyLabyrinthFloor2NpcId or
                LegacyLabyrinthFloor3NpcId or
                LegacyLabyrinthFloor4NpcId =>
                await TryHandleLegacyLabyrinthGeneralAsync(context, session, npcId, optionId, cancellationToken)
                    .ConfigureAwait(false),
            LegacySimonNpcId => await TryHandleLegacySimonAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyFrozenGrottoGuardian5NpcId =>
                await TryHandleLegacyFrozenGrottoGuardian5Async(context, session, optionId, cancellationToken)
                    .ConfigureAwait(false),
            LegacyFrozenFroto4TeleporterNpcId =>
                await TryHandleLegacyFrozenFroto4TeleporterAsync(context, session, optionId, cancellationToken)
                    .ConfigureAwait(false),
            LegacyDesertFrozenGrotoNpcId =>
                await TryHandleLegacyDesertFrozenGrotoAsync(context, session, optionId, cancellationToken)
                    .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyStanleyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.Stanley";
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Hello there. Just let me know if you want to go to Twin City."],
                        LegacyLabyrinthReturnOptions,
                        LegacyLabyrinthQuestAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyTwinCityConfirmAsync(context, sourceMethod, yesOptionId: 3, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyStanleyNpcId,
                        optionId,
                        LegacyLabyrinthTwinCityDestination with { SourceMethod = sourceMethod },
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
            case LegacyNpcClosedOption:
                await LogLegacyLabyrinthClosedOptionAsync(context, LegacyStanleyNpcId, optionId, sourceMethod)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyLabyrinthGeneralAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId == LegacyLabyrinthFloor4NpcId)
        {
            return await TryHandleLegacyLabyrinthFloor4Async(context, session, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!TryGetLegacyLabyrinthFloor(npcId, out var floor))
        {
            return false;
        }

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        floor.SourceMethod,
                        floor.RootTexts,
                        LegacyLabyrinthFloorRootOptions,
                        LegacyLabyrinthGeneralAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                if (HasUnboundLegacyLabyrinthFloorToken(session.PlayerItems, floor.TokenItemId))
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            floor.SourceMethod,
                            ["The next floor is more dangerous. You should be more careful."],
                            LegacyLabyrinthNextFloorConfirmOptions,
                            LegacyLabyrinthGeneralAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            floor.SourceMethod,
                            [floor.MissingTokenText],
                            LegacyLabyrinthStayOptions,
                            floor.MissingTokenAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                return true;

            case 2:
                return await TryAdvanceLegacyLabyrinthFloorAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        floor,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 3:
                await SendLegacyTwinCityConfirmAsync(context, floor.SourceMethod, yesOptionId: 4, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 4:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        LegacyLabyrinthTwinCityDestination with { SourceMethod = floor.SourceMethod },
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case LegacyNpcClosedOption:
                await LogLegacyLabyrinthClosedOptionAsync(context, npcId, optionId, floor.SourceMethod)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyLabyrinthFloor4Async(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.Lab4";
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["I am NorthGeneral. This is the fourth floor. If you want to go back to Twin City, I can help you."],
                        [
                            new LegacyNpcDialogOption("To~Twin~City.", 2),
                            new LegacyNpcDialogOption("I~will~stay~here.", LegacyNpcClosedOption),
                        ],
                        LegacyLabyrinthGeneralAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyTwinCityConfirmAsync(context, sourceMethod, yesOptionId: 3, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyLabyrinthFloor4NpcId,
                        optionId,
                        LegacyLabyrinthTwinCityDestination with { SourceMethod = sourceMethod },
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
            case LegacyNpcClosedOption:
                await LogLegacyLabyrinthClosedOptionAsync(context, LegacyLabyrinthFloor4NpcId, optionId, sourceMethod)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacySimonAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.Simon";
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Great rewards will attract many brave people. I am looking for brave people to help me take my patrimony back."],
                        LegacySimonRootOptions,
                        LegacyLabyrinthQuestAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "My ancestors built a Labyrinth long before. Many treasures were stored there like SunDiamonds, MoonDiamonds, StarDiamonds and more.",
                            "But it was occupied by fierce monsters soon. They expelled our clansmen and kept the treasure.",
                        ],
                        LegacySimonStoryStep1Options,
                        LegacyLabyrinthQuestAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["The Labyrinth contains 4 floors guarded by different monsters. If you can find the treasures and give them to me, I will reward you."],
                        LegacySimonStoryStep2Options,
                        LegacyLabyrinthQuestAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "There are 3 kinds of monsters on each floor. The more numerous monsters are weaker; the rarer ones are stronger.",
                            "The fiercest monsters are always moving around. They drop different tokens.",
                        ],
                        LegacySimonStoryStep3Options,
                        LegacyLabyrinthQuestAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 4:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "SkyToken, EarthToken and SoulToken are needed to enter the next floors. Weak monsters usually drop treasure.",
                            "After you get a token, find a general who will send you to the next floor. Some boss monsters drop rare items.",
                        ],
                        LegacySimonStoryStep4Options,
                        LegacyLabyrinthQuestAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 5:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["There are 3 ways to enter the Labyrinth: pay 2,000 Virtue Points, submit 5 Meteors, or use VIP service."],
                        LegacySimonEntryOptions,
                        LegacyLabyrinthQuestAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 6:
                return await TryHandleLegacySimonVirtueEntryAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);

            case 7:
                return await TryHandleLegacySimonMeteorEntryAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);

            case 8:
                return await TryHandleLegacySimonVipEntryAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);

            case 10:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "I need SunDiamond, MoonDiamond, StarDiamond and CloudDiamond.",
                            "If you get them for me, I will give you some rewards.",
                        ],
                        LegacySimonRewardNeedOptions,
                        LegacyLabyrinthQuestAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 11:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "2 Meteors for 17 SunDiamonds, 4 Meteors for 17 MoonDiamonds, a normal gem for 17 StarDiamonds, and an AncestorBox",
                            "for 17 CloudDiamonds. If you are lucky enough, you will get a big surprise from the box.",
                        ],
                        [new LegacyNpcDialogOption("I~see.~Thank~you.", LegacyNpcClosedOption)],
                        LegacyLabyrinthQuestAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 12:
                return await TryClaimLegacySimonDiamondRewardAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 13:
                return await TryClaimLegacySimonDiamondRewardAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 14:
                return await TryClaimLegacySimonDiamondRewardAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 15:
                return await TryClaimLegacySimonDiamondRewardAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 9:
            case LegacyNpcClosedOption:
                await LogLegacyLabyrinthClosedOptionAsync(context, LegacySimonNpcId, optionId, sourceMethod)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static ValueTask<bool> TryHandleLegacySimonVirtueEntryAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        return TryApplyLegacySimonVirtueEntryMutationAsync(context, session, cancellationToken);
    }

    private static ValueTask<bool> TryHandleLegacySimonMeteorEntryAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        return TryApplyLegacySimonMeteorEntryMutationAsync(context, session, cancellationToken);
    }

    private static async ValueTask<bool> TryHandleLegacySimonVipEntryAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var vipLevel = ReadAttributeUInt64(session.Character.Attributes, "VipLevel", "VIPLevel");
        if (vipLevel < 1)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.Simon",
                    ["Sorry, you don't have VIP."],
                    [new LegacyNpcDialogOption("Sigh...", LegacyNpcClosedOption)],
                    LegacyLabyrinthQuestAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        await SendLegacyNpcTeleportAsync(
                context,
                session,
                LegacySimonNpcId,
                optionId: 8,
                LegacyLabyrinthEntranceDestination,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyFrozenGrottoGuardian5Async(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.FrozenGrottoGuardian5";
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "Who's there? Oh, you are from Twin City, aren't you? Finally, I thought no one would come here to rescue us.",
                            "We've still got some business to carry out on the 6th floor. You're here to go there and clear out the monsters, right?",
                            "I can also help you get to the other floors, if you like.",
                        ],
                        LegacyFrozenGrottoGuardian5Options,
                        LegacyFrozenGrottoAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["There is a portal on this floor at (150,178). You can get to F6 from here. Be careful."],
                        [new LegacyNpcDialogOption("Thanks.~I~will.", LegacyNpcClosedOption)],
                        LegacyFrozenGrottoAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                return await TryHandleLegacyFrozenGrottoShadowJadeAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);

            case 3:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyFrozenGrottoGuardian5NpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(2_054, 379, 470, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case LegacyNpcClosedOption:
                await LogLegacyLabyrinthClosedOptionAsync(
                        context,
                        LegacyFrozenGrottoGuardian5NpcId,
                        optionId,
                        sourceMethod)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static ValueTask<bool> TryHandleLegacyFrozenGrottoShadowJadeAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        return TryGrantLegacyFrozenGrottoShadowJadeAsync(context, session, cancellationToken);
    }

    private static async ValueTask<bool> TryHandleLegacyFrozenFroto4TeleporterAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.FrozenFroto4Teleporter";
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "I guess you've heard about the 5th floor of Frozen Grotto. The place is infested with ferocious monsters that no one dares go up there.",
                            "Monsters on the 3rd and 4th floors carry a Grotto Key. With this key, it is easier to kill the monsters on the 5th floor.",
                            "If you are interested, get a key and I will send you to the 5th floor.",
                            "If your VIP level has reached 4, you can enter the 5th floor of Frozen Grotto every day for free.",
                        ],
                        LegacyFrozenFroto4TeleporterOptions,
                        LegacyFrozenGrottoAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                return await TryEnterLegacyFrozenGrottoWithKeyAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);

            case 2:
                if (ReadAttributeUInt64(session.Character.Attributes, "VipLevel", "VIPLevel") < 4)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            sourceMethod,
                            [
                                "Sorry, but your VIP Level has to reach 4 to enter the 5th floor for free. If you want to go there, you must collect the Grotto Key.",
                            ],
                            [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                            LegacyFrozenGrottoAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyFrozenFroto4TeleporterNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(2_055, 526, 562, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case LegacyNpcClosedOption:
                await LogLegacyLabyrinthClosedOptionAsync(
                        context,
                        LegacyFrozenFroto4TeleporterNpcId,
                        optionId,
                        sourceMethod)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyDesertFrozenGrotoAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.DesertFrozenGroto";
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "The Frozen Grotto is a dangerous and ancient place. Many heroes have died there while searching for its hidden treasures.",
                            "Six layers of the Frozen Grotto have been excavated so far. If you have made up your mind to test your ability there, I can teleport you to the first 2 floors.",
                            "If you want to go to the 5th floor, speak to the Guardian on Frozen Grotto F4 (387,460).",
                        ],
                        LegacyDesertFrozenGrotoOptions,
                        LegacyDesertFrozenGrottoAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyDesertFrozenGrotoNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_762, 338, 354, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyDesertFrozenGrotoNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_927, 392, 625, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case LegacyNpcClosedOption:
                await LogLegacyLabyrinthClosedOptionAsync(context, LegacyDesertFrozenGrotoNpcId, optionId, sourceMethod)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask SendLegacyTwinCityConfirmAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte yesOptionId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                ["Are you sure you want to leave for Twin City?"],
                [
                    new LegacyNpcDialogOption("Yes.", yesOptionId),
                    new LegacyNpcDialogOption("Let~me~think...", LegacyNpcClosedOption),
                ],
                LegacyLabyrinthQuestAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyLabyrinthClosedOptionAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        byte optionId,
        string sourceMethod)
    {
        await context.Log.WriteLineAsync(
                "WorldServer ignored LegacyNpc Labyrinth closed/no-op option " +
                $"{FormatSession(context)} npcId={npcId.ToString(CultureInfo.InvariantCulture)} " +
                $"option={optionId.ToString(CultureInfo.InvariantCulture)} source={sourceMethod}.")
            .ConfigureAwait(false);
    }

    private static bool TryGetLegacyLabyrinthFloor(uint npcId, out LegacyLabyrinthFloor floor)
    {
        floor = npcId switch
        {
            LegacyLabyrinthFloor1NpcId => new LegacyLabyrinthFloor(
                "NpcHandler.Lab1",
                [
                    "I am EastGeneral. This is the first floor of Labyrinth. If you want to enter the next floor, you should give me a",
                    "Sky Token, which is carried by the Gold Ghosts.",
                ],
                LegacyLabyrinthSkyTokenItemId,
                "SkyToken",
                "You have no Sky Token. Hurry to loot one from the Gold Ghosts.",
                LegacyLabyrinthGeneralAvatarId,
                "You have no Sky Token. Hurry to loot one from the Gold Ghosts.",
                LegacyLabyrinthStayOptions,
                LegacyLabyrinthGeneralAvatarId,
                new LegacyNpcTeleportDestination(1_352, 27, 227, "NpcHandler.Lab1"),
                "NpcHandler.cs:34069-34120"),
            LegacyLabyrinthFloor2NpcId => new LegacyLabyrinthFloor(
                "NpcHandler.Lab2",
                [
                    "I am WestGeneral. This is the second floor of Labyrinth. If you want to enter the next floor, you should give me an",
                    "EarthToken, or would you like to go back to Twin City?",
                ],
                LegacyLabyrinthEarthTokenItemId,
                "EarthToken",
                "You don't have an EarthToken. I cannot teleport you to the next floor.",
                LegacyLabyrinthGeneralAvatarId,
                "You don't have an EarthToken. I cannot teleport you to the next floor.",
                LegacyLabyrinthStayOptions,
                LegacyLabyrinthGeneralAvatarId,
                new LegacyNpcTeleportDestination(1_353, 25, 268, "NpcHandler.Lab2"),
                "NpcHandler.cs:34015-34067"),
            LegacyLabyrinthFloor3NpcId => new LegacyLabyrinthFloor(
                "NpcHandler.Lab3",
                [
                    "I am SouthGeneral. This is the third floor of Labyrinth. If you want to enter the next floor, you should give me a",
                    "SoulToken. Or would you like to go back to Twin City?",
                ],
                LegacyLabyrinthSoulTokenItemId,
                "SoulToken",
                "You don't have a SoulToken. I cannot teleport you there.",
                null,
                "Sorry, you don't have 1 SoulToken.",
                [new LegacyNpcDialogOption("Sigh...", LegacyNpcClosedOption)],
                LegacyLabyrinthQuestAvatarId,
                new LegacyNpcTeleportDestination(1_354, 5, 290, "NpcHandler.Lab3"),
                "NpcHandler.cs:33962-34013"),
            _ => default,
        };

        return !string.IsNullOrEmpty(floor.SourceMethod);
    }

    private readonly record struct LegacyLabyrinthFloor(
        string SourceMethod,
        IReadOnlyList<string> RootTexts,
        uint TokenItemId,
        string TokenName,
        string MissingTokenText,
        ushort? MissingTokenAvatarId,
        string MissingTokenConfirmText,
        IReadOnlyList<LegacyNpcDialogOption> MissingTokenConfirmOptions,
        ushort? MissingTokenConfirmAvatarId,
        LegacyNpcTeleportDestination NextFloorDestination,
        string SourceLines);
}

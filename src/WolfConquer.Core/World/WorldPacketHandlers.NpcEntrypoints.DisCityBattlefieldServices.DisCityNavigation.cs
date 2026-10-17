using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyDisCityBattlefieldServicesTwinDisCityMainNpcId = 3_215;
    private const uint LegacyDisCityBattlefieldServicesDisCityMainNpcId = 9_280;
    private const uint LegacyDisCityBattlefieldServicesDisCityMap1NpcId = 4_026_314;
    private const uint LegacyDisCityBattlefieldServicesDisCityMap2NpcId = 4_026_315;
    private const uint LegacyDisCityBattlefieldServicesDicCityMap4NpcId = 4_026_316;

    private static readonly LegacyNpcDialogOption[] LegacyDisCityBattlefieldServicesTwinCityLeaveOptions =
    [
        new("Twin City.", 2),
        new("Let me think.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDisCityBattlefieldServicesTwinCityConfirmOptions =
    [
        new("Yes.", 3),
        new("Let me think.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDisCityBattlefieldServicesDisCityRootOptions =
    [
        new("Could you tell me more?", 1),
        new("I am here to drive away the evils!", 10),
        new("I have the Dark Horn.", 20),
        new("I'd better leave.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDisCityBattlefieldServicesDisCityMoreOptions =
    [
        new("I want to know Hell Gate.", 6),
        new("I want to know Hell Hall.", 7),
        new("I want to know Hell Cloister.", 8),
        new("I want to know Battle Formation.", 9),
        new("Thanks. I know what to do.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDisCityBattlefieldServicesDisCityBackToMoreOptions =
    [
        new("Thank you.", LegacyNpcClosedOption),
        new("I'd like to know more.", 4),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDisCityBattlefieldServicesDisCityMap1Options =
    [
        new("I'm ready.", 2),
        new("Leave Dis City.", 1),
        new("Wait a moment.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> LegacyDisCityBattlefieldServicesTryHandleDicCityMap4Async(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var source = new LegacyDisCityBattlefieldServicesSource("NpcHandler.DicCityMap4", "NpcHandler.cs:28459-28529");
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["Hello there. Let me know if you want to go to Twin City."],
                        [
                            new LegacyNpcDialogOption("Twin City.", 2),
                            new LegacyNpcDialogOption("I have the Dark Horn.", 20),
                            new LegacyNpcDialogOption("Let me think.", LegacyNpcClosedOption),
                        ],
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 20:
                await LegacyDisCityBattlefieldServicesClaimDarkHornRewardAsync(
                        context,
                        LegacyDisCityBattlefieldServicesDicCityMap4NpcId,
                        optionId,
                        source,
                        missingHornAvatarId: LegacyDisCityBattlefieldServicesEventAvatarId,
                        fullInventoryAvatarId: 8,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["Are you sure you want to leave for Twin City?"],
                        LegacyDisCityBattlefieldServicesTwinCityConfirmOptions,
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyDisCityBattlefieldServicesDicCityMap4NpcId,
                        optionId,
                        LegacyDisCityBattlefieldServicesTwinCityDestination(source.SourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyDisCityBattlefieldServicesTryHandleDisCityMap2Async(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var source = new LegacyDisCityBattlefieldServicesSource("NpcHandler.DisCityMap2", "NpcHandler.cs:28530-28617");
        switch (optionId)
        {
            case 0:
                var kills = ReadAttributeUInt64(session.Character.Attributes, "KillersDisCity");
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        [
                            "Hello, can you help me? Kill the required Hell Hall monsters to earn a reward.",
                            $"You have killed {kills.ToString(CultureInfo.InvariantCulture)} monsters.",
                        ],
                        [
                            new LegacyNpcDialogOption("I finished.", 1),
                            new LegacyNpcDialogOption("Sorry, I'll be back.", LegacyNpcClosedOption),
                        ],
                        LegacyDisCityBattlefieldServicesEventAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await LegacyDisCityBattlefieldServicesSendUnavailableAsync(
                        context,
                        LegacyDisCityBattlefieldServicesDisCityMap2NpcId,
                        optionId,
                        source,
                        "Hell Hall advancement and rewards are not available from this runtime yet.",
                        "Source checks KillersDisCity against KillsMap2Records, grants item 723087 and a random refinery pack, then teleports to map 3.",
                        LegacyDisCityBattlefieldServicesEventAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyDisCityBattlefieldServicesTryHandleDisCityMap1Async(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var source = new LegacyDisCityBattlefieldServicesSource("NpcHandler.DisCityMap1", "NpcHandler.cs:28618-28670");
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["Bring me five Soul Stones and I can help you pass Hell Gate and enter the next level."],
                        LegacyDisCityBattlefieldServicesDisCityMap1Options,
                        LegacyDisCityBattlefieldServicesDisCityAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await LegacyDisCityBattlefieldServicesSendUnavailableAsync(
                        context,
                        LegacyDisCityBattlefieldServicesDisCityMap1NpcId,
                        optionId,
                        source,
                        "Soul Stone exchange rewards are not available from this runtime yet.",
                        "Source consumes five item 723085 Soul Stones, grants bound rewards, and calls MsgSchedules.DisCity.TeleportToMap2.",
                        LegacyDisCityBattlefieldServicesDisCityAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyDisCityBattlefieldServicesDisCityMap1NpcId,
                        optionId,
                        LegacyDisCityBattlefieldServicesTwinCityDestination(source.SourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyDisCityBattlefieldServicesTryHandleTwinDisCityMainAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var source = new LegacyDisCityBattlefieldServicesSource("NpcHandler.TwinDisCityMain", "NpcHandler.cs:28671-28813");
        switch (optionId)
        {
            case 0:
                await LegacyDisCityBattlefieldServicesSendDisCityRootAsync(context, source.SourceMethod, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        [
                            "The decisive battle between humans and demons took place here.",
                            "The battle lasted seven days and nights. Countless heroes died, but justice prevailed.",
                        ],
                        [new LegacyNpcDialogOption("And then?", 2)],
                        LegacyDisCityBattlefieldServicesDisCityAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        [
                            "Ultimate Pluto, the guest leader, swore to take revenge after one thousand years.",
                            "Yesterday I found signs that he has returned to threaten humanity again.",
                        ],
                        [new LegacyNpcDialogOption("What should we do?", 3)],
                        LegacyDisCityBattlefieldServicesDisCityAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["Ultimate Pluto is still weak and must rely on a Battle Formation. I am organizing heroes to destroy it before he grows stronger."],
                        [
                            new LegacyNpcDialogOption("I'd like to know more.", 4),
                            new LegacyNpcDialogOption("Sigh, I'm helpless.", LegacyNpcClosedOption),
                        ],
                        LegacyDisCityBattlefieldServicesDisCityAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 4:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        [
                            "Break into Hell Gate, enter Hell Hall, fight through Hell Cloister, and defeat Ultimate Pluto quickly.",
                            "Bring me the Dark Horn so I can disable the formation.",
                        ],
                        LegacyDisCityBattlefieldServicesDisCityMoreOptions,
                        LegacyDisCityBattlefieldServicesDisCityAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 6:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["Hell Gate is blocked by poisonous fog. Gather five Soul Stones from demons and I will help you break through the gate."],
                        LegacyDisCityBattlefieldServicesDisCityBackToMoreOptions,
                        LegacyDisCityBattlefieldServicesDisCityAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 7:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["Hell Hall is where demons swear allegiance to Ultimate Pluto. Only a limited number of heroes can advance to Hell Cloister."],
                        LegacyDisCityBattlefieldServicesDisCityBackToMoreOptions,
                        LegacyDisCityBattlefieldServicesDisCityAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 8:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["In Hell Cloister, heroes attack from both flanks. Defeat enough Wraiths to reach the Battle Formation."],
                        LegacyDisCityBattlefieldServicesDisCityBackToMoreOptions,
                        LegacyDisCityBattlefieldServicesDisCityAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 9:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["Syrens protect the Battle Formation. Defeat them, kill Ultimate Pluto, and bring me the Dark Horn."],
                        LegacyDisCityBattlefieldServicesDisCityBackToMoreOptions,
                        LegacyDisCityBattlefieldServicesDisCityAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 10:
                await LegacyDisCityBattlefieldServicesSendUnavailableAsync(
                        context,
                        LegacyDisCityBattlefieldServicesTwinDisCityMainNpcId,
                        optionId,
                        source,
                        "Dis City entry is not available from this runtime yet.",
                        "Source depends on MsgSchedules.DisCity.AllowJoin, player level and inventory checks, and TeleportMap1.",
                        LegacyDisCityBattlefieldServicesDisCityAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 20:
                await LegacyDisCityBattlefieldServicesClaimDarkHornRewardAsync(
                        context,
                        LegacyDisCityBattlefieldServicesTwinDisCityMainNpcId,
                        optionId,
                        source,
                        missingHornAvatarId: LegacyDisCityBattlefieldServicesDisCityAvatarId,
                        fullInventoryAvatarId: LegacyDisCityBattlefieldServicesDisCityAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 11:
                await context.Connection.SendAsync(
                        LegacyMsgMessagePacket.SystemQualifier("As long as you reach Level 110, you can enter Dis City.").ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 5:
            case >= 12 and <= 19:
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyDisCityBattlefieldServicesTryHandleDisCityMainAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var source = new LegacyDisCityBattlefieldServicesSource("NpcHandler.DisCityMain", "NpcHandler.cs:28814-28939");
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["I have been searching for the meaning of life for decades."],
                        [
                            new LegacyNpcDialogOption("Quest: The Stone Ape (1).", 40),
                            new LegacyNpcDialogOption("Have you found anything?", 41),
                        ],
                        LegacyDisCityBattlefieldServicesDisCityAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 40:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["The Stone Ape was born from rock used to repair cracks in the sky. Guided properly, he could become a great immortal."],
                        [
                            new LegacyNpcDialogOption("Thank you for the information!", 42),
                            new LegacyNpcDialogOption("See you then!", LegacyNpcClosedOption),
                        ],
                        LegacyDisCityBattlefieldServicesDisCityAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 42:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["I have a Divine Calix. Fill it with fountain water from Maple Forest to help the Stone Ape unleash his potential."],
                        [
                            new LegacyNpcDialogOption("Okay!", 43),
                            new LegacyNpcDialogOption("See you then!", LegacyNpcClosedOption),
                        ],
                        LegacyDisCityBattlefieldServicesDisCityAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 43:
                await LegacyDisCityBattlefieldServicesSendUnavailableAsync(
                        context,
                        LegacyDisCityBattlefieldServicesDisCityMainNpcId,
                        optionId,
                        source,
                        "Stone Ape quest rewards are not available from this runtime yet.",
                        "Source grants item 711354, finishes mission 1312, grants experience and silver, and accepts mission 1313.",
                        LegacyDisCityBattlefieldServicesDisCityAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 41:
                await LegacyDisCityBattlefieldServicesSendDisCityRootAsync(context, source.SourceMethod, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask LegacyDisCityBattlefieldServicesSendDisCityRootAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [
                    "Our ancestors defeated the demons and kept the world peaceful for hundreds of years.",
                    "Now the demons have returned. Dis City opens from 7:30-7:35 and 17:30-17:35 every day except Sunday.",
                ],
                LegacyDisCityBattlefieldServicesDisCityRootOptions,
                LegacyDisCityBattlefieldServicesDisCityAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }
}

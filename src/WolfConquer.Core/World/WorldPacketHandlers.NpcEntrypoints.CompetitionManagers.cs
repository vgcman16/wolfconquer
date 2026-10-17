using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyArenaManagerWangNpcId = 1_881;
    private const uint LegacyMissConquerNpcId = 60_112;
    private const uint LegacyMrConquerNpcId = 60_111;
    private const uint LegacyMonthlyPkNpcId = 2_316;
    private const uint LegacyWinMonthlyNpcId = 2_317;
    private const uint LegacyFreezeWarNpcId = 52_004;
    private const uint LegacySuperGuildWarNpcId = 25_015;
    private const uint LegacyPkWarQuitNpcId = 4_026_313;
    private const uint LegacyWeeklyPkNpcId = 31;
    private const uint LegacyTeamDeathMatchNpcId = 777;
    private const uint LegacyExtremePkNpcId = 4_699;
    private const uint LegacyCaptureTheFlagNpcId = 22_234;
    private const uint LegacyArenaNpcId = 10_021;
    private const uint LegacySkillTeamPkManagerNpcId = 8_592;
    private const uint LegacyTeamPkManagerNpcId = 8_158;
    private const uint LegacyElitePkNpcId = 7_879;
    private const uint LegacyClassPkWarNpcId = 16_851;
    private const uint LegacyClassPkEnvoyNpcId = 662;
    private const uint LegacyClassPkEnvoy2NpcId = 6_612;
    private const uint LegacyExitElitePkNpcId = 168_052;
    private const uint LegacyExitSuperGuildWarNpcId = 4_026_311;
    private const uint LegacyExitCaptureTheFlagNpcId = 4_026_312;

    private const ushort LegacyCompetitionDefaultAvatarId = 63;
    private const ushort LegacyCompetitionWarAvatarId = 154;
    private const ushort LegacyArenaManagerWangAvatarId = 31;
    private const ushort LegacySuperGuildWarAvatarId = 6;
    private const ushort LegacyCaptureTheFlagAvatarId = 123;
    private const ushort LegacyCaptureTheFlagRewardAvatarId = 110;
    private const ushort LegacyArenaNpcAvatarId = 7;
    private const long LegacyArenaNpcEntryFee = 50;
    private const uint LegacyRefineryBadgeItemId = 720_148;

    private static readonly LegacyRefineryPackDefinition[] LegacyArenaManagerWangRefineryPacks =
    [
        new(724_154, 724_155, 724_156, "Veneer", "1-handed, Critical Strike"),
        new(724_151, 724_152, 724_153, "Bowstring", "Bow, Critical Strike"),
        new(724_157, 724_158, 724_159, "Red Tassel", "2-handed, Critical Strike"),
        new(724_133, 724_134, 724_135, "Long Tassel", "Backsword, Skill Critical Strike"),
        new(724_136, 724_137, 724_138, "Plate", "Armor, Immunity"),
        new(724_139, 724_140, 724_141, "Visor", "Headgear, Intensification"),
        new(724_160, 724_161, 724_162, "Cutter", "1-handed, Breakthrough"),
        new(724_166, 724_167, 724_168, "Bow Limb", "Bow, Breakthrough"),
        new(724_163, 724_164, 724_165, "Cauterant", "2-handed, Breakthrough"),
        new(724_142, 724_143, 724_144, "Breastplate", "Armor, Counteraction"),
        new(724_172, 724_173, 724_174, "Tri Plume", "Headgear, Detoxication"),
        new(724_175, 724_176, 724_177, "Belt", "Armor, Detoxication"),
        new(724_145, 724_146, 724_147, "Hat Rope", "Headgear, Block"),
        new(724_169, 724_170, 724_171, "Pin", "Shield, Block"),
        new(724_184, 724_185, 724_186, "Hemstitch", "Headgear, Penetration"),
        new(724_130, 724_131, 724_132, "Bell", "Bracelet, Penetration"),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCompetitionCloseOptions =
    [
        new("Okay.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyArenaManagerWangRootOptions =
    [
        new("Enter Arena.", 30),
        new("What are the rules?", 20),
        new("Exchange for refinery packs.", 10),
        new("Not interested.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyArenaManagerWangExchangeOptions =
    [
        new("Primary refinery pack. (2)", 11),
        new("Medium refinery pack. (6)", 14),
        new("Superior refinery pack. (12)", 17),
        new("Not interested.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRefineryPageOneOptions =
    [
        new("Veneer - Critical Strike.", 102),
        new("Bowstring - Critical Strike.", 103),
        new("Red Tassel - Critical Strike.", 104),
        new("Long Tassel - Skill Critical Strike.", 105),
        new("Plate - Immunity.", 106),
        new("Visor - Intensification.", 107),
        new("Cutter - Breakthrough.", 108),
        new("Next page.", 12),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRefineryPageTwoOptions =
    [
        new("Bow Limb - Breakthrough.", 109),
        new("Cauterant - Breakthrough.", 110),
        new("Breastplate - Counteraction.", 111),
        new("Tri Plume - Detoxication.", 112),
        new("Belt - Detoxication.", 113),
        new("Hat Rope - Block.", 114),
        new("Back page.", 11),
        new("Next page.", 13),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRefineryPageThreeOptions =
    [
        new("Pin - Block.", 115),
        new("Hemstitch - Penetration.", 116),
        new("Bell - Penetration.", 117),
        new("Back page.", 12),
        new("I have other things to do.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyMediumRefineryPageOneOptions =
    [
        new("Veneer - Critical Strike.", 120),
        new("Bowstring - Critical Strike.", 121),
        new("Red Tassel - Critical Strike.", 122),
        new("Long Tassel - Skill Critical Strike.", 123),
        new("Plate - Immunity.", 124),
        new("Visor - Intensification.", 125),
        new("Cutter - Breakthrough.", 126),
        new("Next page.", 15),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyMediumRefineryPageTwoOptions =
    [
        new("Bow Limb - Breakthrough.", 127),
        new("Cauterant - Breakthrough.", 128),
        new("Breastplate - Counteraction.", 129),
        new("Tri Plume - Detoxication.", 130),
        new("Belt - Detoxication.", 131),
        new("Hat Rope - Block.", 132),
        new("Back page.", 14),
        new("Next page.", 16),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyMediumRefineryPageThreeOptions =
    [
        new("Pin - Block.", 133),
        new("Hemstitch - Penetration.", 134),
        new("Bell - Penetration.", 135),
        new("Back page.", 15),
        new("I have other things to do.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySuperiorRefineryPageOneOptions =
    [
        new("Veneer - Critical Strike.", 140),
        new("Bowstring - Critical Strike.", 141),
        new("Red Tassel - Critical Strike.", 142),
        new("Long Tassel - Skill Critical Strike.", 143),
        new("Plate - Immunity.", 144),
        new("Visor - Intensification.", 145),
        new("Cutter - Breakthrough.", 146),
        new("Next page.", 18),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySuperiorRefineryPageTwoOptions =
    [
        new("Bow Limb - Breakthrough.", 147),
        new("Cauterant - Breakthrough.", 148),
        new("Breastplate - Counteraction.", 149),
        new("Tri Plume - Detoxication.", 150),
        new("Belt - Detoxication.", 151),
        new("Hat Rope - Block.", 152),
        new("Back page.", 17),
        new("Next page.", 19),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySuperiorRefineryPageThreeOptions =
    [
        new("Pin - Block.", 153),
        new("Hemstitch - Penetration.", 154),
        new("Bell - Penetration.", 155),
        new("Back page.", 18),
        new("I have other things to do.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySignUpOptions =
    [
        new("Sign me up.", 1),
        new("I will come later.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyMonthlyRewardOptions =
    [
        new("Claim prize.", 26),
        new("No thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySuperGuildWarOptions =
    [
        new("Enter the field.", 6),
        new("Claim the reward.", 7),
        new("Just passing by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyPkWarQuitOptions =
    [
        new("Yes.", 1),
        new("Teleport to Twin City.", 2),
        new("Wait.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyTwinCityConfirmOptions =
    [
        new("Yes.", 3),
        new("Let me think.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyTournamentRootOptions =
    [
        new("Tell me more about the rules.", 1),
        new("Sign me up.", 3),
        new("I want to claim my prize.", 5),
        new("Not interested.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyElitePkRuleOptions =
    [
        new("Are there finals?", 2),
        new("Not interested.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyElitePkFinalRuleOptions =
    [
        new("Sign me up.", 3),
        new("Not interested.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCaptureTheFlagOptions =
    [
        new("Sign up for Capture the Flag.", 5),
        new("Claim the winning reward.", 4),
        new("Just passing by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyArenaNpcOptions =
    [
        new("Enter the arena.", 1),
        new("Just passing by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyClassPkWarOptions =
    [
        new("Yes.", 1),
        new("Teleport to Twin City.", 2),
        new("Wait.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyClassPkEnvoyOptions =
    [
        new("Sign up.", 2),
        new("Not interested.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyClassPkEnvoy2Options =
    [
        new("Claim reward.", 1),
        new("Not interested.", LegacyNpcClosedOption),
    ];

    internal static ValueTask<bool> TryHandleLegacyCompetitionManagersNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return TryHandleLegacyCompetitionManagersNpcAsync(
            context,
            session,
            npcId,
            optionId,
            DateTime.Now,
            cancellationToken);
    }

    internal static async ValueTask<bool> TryHandleLegacyCompetitionManagersNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyArenaManagerWangNpcId => await TryHandleLegacyArenaManagerWangAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyMissConquerNpcId => await TryHandleLegacyGenderPkAsync(
                    context,
                    session,
                    optionId,
                    now,
                    LegacyMissConquerNpcId,
                    expectedBody: 2007,
                    sourceMethod: "NpcHandler.MissConquer",
                    rootText: "Hello! The Miss Conquer PK event is held every day from 06:00 to 06:15.",
                    wrongBodyText: "Sorry, this event is only for female characters.",
                    destination: new LegacyNpcTeleportDestination(6_011, 70, 70, "NpcHandler.MissConquer"),
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyMrConquerNpcId => await TryHandleLegacyGenderPkAsync(
                    context,
                    session,
                    optionId,
                    now,
                    LegacyMrConquerNpcId,
                    expectedBody: 1008,
                    sourceMethod: "NpcHandler.MRConquer",
                    rootText: "Hello! The Mr. Conquer PK event is held every day from 06:00 to 06:15.",
                    wrongBodyText: "Sorry, this event is only for male characters.",
                    destination: new LegacyNpcTeleportDestination(6_012, 70, 70, "NpcHandler.MRConquer"),
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyMonthlyPkNpcId => await TryHandleLegacyMonthlyPkAsync(context, session, optionId, now, cancellationToken)
                .ConfigureAwait(false),
            LegacyWinMonthlyNpcId => await TryHandleLegacyWinMonthlyAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyFreezeWarNpcId => await TryHandleLegacyFreezeWarAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacySuperGuildWarNpcId => await TryHandleLegacySuperGuildWarAsync(
                    context,
                    session,
                    optionId,
                    now,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPkWarQuitNpcId => await TryHandleLegacyPkWarQuitAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyWeeklyPkNpcId => await TryHandleLegacySimpleSignUpAsync(
                    context,
                    optionId,
                    "NpcHandler.Weeklypk",
                    "Hello there! The PK War takes place from 13:00 to 13:15.",
                    "PK War entry depends on the legacy PkWar.AllowJoin scheduler.",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyTeamDeathMatchNpcId => await TryHandleLegacySimpleSignUpAsync(
                    context,
                    optionId,
                    "NpcHandler.TeamDeathMatch",
                    "Hello there! Team Death Match starts at 7:00 AM.",
                    "Team Death Match entry depends on MsgSchedules.CurrentTournament.Join.",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyExtremePkNpcId => await TryHandleLegacySimpleSignUpAsync(
                    context,
                    optionId,
                    "NpcHandler.ExtremePk",
                    "Glad to see you! Extreme PK is a daily tournament where kills earn points. It starts at 11:00.",
                    "Extreme PK entry depends on MsgSchedules.CurrentTournament.Join.",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyCaptureTheFlagNpcId => await TryHandleLegacyCaptureTheFlagAsync(context, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyArenaNpcId => await TryHandleLegacyArenaNpcAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacySkillTeamPkManagerNpcId => await TryHandleLegacySkillTeamPkManagerAsync(
                    context,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyTeamPkManagerNpcId => await TryHandleLegacyTeamPkManagerAsync(context, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyElitePkNpcId => await TryHandleLegacyElitePkAsync(context, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyClassPkWarNpcId => await TryHandleLegacyClassPkWarAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyClassPkEnvoyNpcId => await TryHandleLegacyClassPkEnvoyAsync(context, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyClassPkEnvoy2NpcId => await TryHandleLegacyClassPkEnvoy2Async(context, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyExitElitePkNpcId => await TryHandleLegacyCompetitionExitAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    "NpcHandler.ExitElitePk",
                    "Source also clears transformations, toggles InElitePk/InTeamPk flags, and disconnects cross-server players; those runtime fields are not represented here.",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyExitSuperGuildWarNpcId => await TryHandleLegacyCompetitionExitAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    "NpcHandler.ExitSuperGuildWar",
                    gapReason: null,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyExitCaptureTheFlagNpcId => await TryHandleLegacyCompetitionExitAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    "NpcHandler.ExitCaptureTheFlag",
                    gapReason: null,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyArenaManagerWangAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.ArenaManagerWang";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Want to test your power and skill? Power Arena runs from 13:00 to 14:00 and from 20:00 to 20:59."],
                        LegacyArenaManagerWangRootOptions,
                        LegacyArenaManagerWangAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 30:
                await SendLegacyCompetitionUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Power Arena entry is not available from this runtime yet.",
                        "PowerArena.Process, random stage placement, and JoinPowerArenaStamp cooldown are global legacy runtime state.",
                        LegacyArenaManagerWangAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 20:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "Power Arena has 7 stages. Guardians in higher stages are stronger, and being knocked down can send you to a lower stage.",
                            "Each stage has an Agate. Defeat enough guardians to claim rewards, advance, withdraw to a lower stage, or quit.",
                        ],
                        LegacyCompetitionCloseOptions,
                        LegacyArenaManagerWangAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "You can exchange Refinery Badges from the Arena for primary, medium, or superior refinery packs.",
                            "Which refinery pack do you want to exchange your badges for?",
                        ],
                        LegacyArenaManagerWangExchangeOptions,
                        LegacyArenaManagerWangAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                if (TryGetLegacyRefineryPackPage(optionId, out var text, out var options))
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            sourceMethod,
                            [text],
                            options,
                            LegacyArenaManagerWangAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (TryGetLegacyRefineryPackReward(optionId, out var reward))
                {
                    await ExchangeLegacyArenaManagerWangRefineryPackAsync(
                            context,
                            session,
                            sourceMethod,
                            optionId,
                            reward,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyGenderPkAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        DateTime now,
        uint npcId,
        long expectedBody,
        string sourceMethod,
        string rootText,
        string wrongBodyText,
        LegacyNpcTeleportDestination destination,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [rootText],
                        LegacySignUpOptions,
                        LegacyCompetitionDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1 when session.Character.Body != expectedBody:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [wrongBodyText],
                        LegacyCompetitionCloseOptions,
                        LegacyCompetitionDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1 when now.Hour == 6 && now.Minute < 15:
                await SendLegacyNpcTeleportAsync(context, session, npcId, optionId, destination, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Please sign up between 06:00 and 06:15 every day."],
                        LegacyCompetitionCloseOptions,
                        LegacyCompetitionDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyMonthlyPkAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.MonthlyPk";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Hello! Monthly PK sign-up is open every day from 09:00 to 09:15."],
                        LegacySignUpOptions,
                        LegacyCompetitionDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1 when now.Hour == 9 && now.Minute < 15:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyMonthlyPkNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(3_978, 165, 200, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Please sign up between 09:00 and 09:15 every day."],
                        LegacyCompetitionCloseOptions,
                        LegacyCompetitionDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyWinMonthlyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.WinMonthly";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [$"Hey there, {session.Character.Name}. Would you like to claim your prize?"],
                        LegacyMonthlyRewardOptions,
                        LegacyCompetitionDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 26:
                await SendLegacyCompetitionUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Monthly PK reward claiming is not available from this runtime yet.",
                        "Source scans live players on map 3978, checks the 09:15-09:45 reward window, grants items, CPs, study points, inner power, class experience, and a champion flag.",
                        LegacyCompetitionDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyFreezeWarAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.FreezeWar";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "Freeze War is a team skill contest. Attack the enemy team to freeze them, and attack frozen teammates to thaw them.",
                            "The event runs daily from 16:30 to 16:40 and from 03:30 to 03:40. The first team to win 5 rounds, or the team with the most points after 10 minutes, wins.",
                        ],
                        LegacySignUpOptions,
                        LegacyCompetitionDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                {
                    if (context.FreezeWarStates is not { } freezeWarStates ||
                        freezeWarStates.Process != LegacyFreezeWarProcess.Idle ||
                        freezeWarStates.DynamicId == 0)
                    {
                        await SendLegacyNpcDialogAsync(
                                context,
                                sourceMethod,
                                ["We are sorry, but you can't enter right now. Try when the tournament starts."],
                                LegacyCompetitionCloseOptions,
                                LegacyCompetitionDefaultAvatarId,
                                cancellationToken)
                            .ConfigureAwait(false);
                        return true;
                    }

                    if (!HasLegacyFreezeWarLineSpell(session))
                    {
                        await SendLegacyNpcDialogAsync(
                                context,
                                sourceMethod,
                                ["Please learn FastBlade, ScentSword, or DragonSlash before entering."],
                                LegacyCompetitionCloseOptions,
                                LegacyCompetitionDefaultAvatarId,
                                cancellationToken)
                            .ConfigureAwait(false);
                        return true;
                    }

                    if (!HasLegacyFreezeWarLineWeapon(session))
                    {
                        await SendLegacyNpcDialogAsync(
                                context,
                                sourceMethod,
                                ["Please equip a blade, sword, or backsword before entering."],
                                LegacyCompetitionCloseOptions,
                                LegacyCompetitionDefaultAvatarId,
                                cancellationToken)
                            .ConfigureAwait(false);
                        return true;
                    }

                    var preflight = await PrepareLegacyTeleportAsync(
                            context,
                            session,
                            new LegacyTeleportRequest(
                                sourceMethod,
                                LegacyFreezeWarRuntimeStateStore.FreezeWarMapId,
                                freezeWarStates.DynamicId),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (!preflight.Allowed)
                    {
                        return true;
                    }

                    session = preflight.Session;
                    if (!freezeWarStates.TryJoin(session, out var joinedSession, out var team))
                    {
                        await SendLegacyNpcDialogAsync(
                                context,
                                sourceMethod,
                                ["We are sorry, but you can't enter right now. Try when the tournament starts."],
                                LegacyCompetitionCloseOptions,
                                LegacyCompetitionDefaultAvatarId,
                                cancellationToken)
                            .ConfigureAwait(false);
                        return true;
                    }

                    var dynamicId = ReadCharacterDynamicId(joinedSession.Character);
                    var teleportSourceSession = CreateLegacyTeleportMutationSourceSession(
                        session,
                        joinedSession);
                    var appliedSession = await ExecuteLegacyTeleportDestinationAsync(
                            context with { Session = teleportSourceSession },
                            teleportSourceSession,
                            new LegacyTeleportDestination(
                                joinedSession.Character.MapId,
                                joinedSession.Character.X,
                                joinedSession.Character.Y,
                                dynamicId),
                            timestamp: 0,
                            cancellationToken,
                            expectedSession: preflight.Session)
                        .ConfigureAwait(false);
                    if (appliedSession is null)
                    {
                        return true;
                    }

                    var joinedContext = context with { Session = appliedSession };
                    await SendPlayerStatusFlagVectorToVisibleAsync(joinedContext, appliedSession, cancellationToken)
                        .ConfigureAwait(false);

                    var garmentId = team == LegacyFreezeWarTeamType.Blue
                        ? LegacyFreezeWarRuntimeStateStore.BlueGarmentId
                        : LegacyFreezeWarRuntimeStateStore.RedGarmentId;
                    await context.Log.WriteLineAsync(
                            "WorldServer applied LegacyNpc FreezeWar idle join " +
                            $"source=NpcHandler.FreezeWar option={optionId} team={team} " +
                            $"{FormatSession(joinedContext)} " +
                            $"map={LegacyFreezeWarRuntimeStateStore.FreezeWarMapId} dynamicId={dynamicId} " +
                            $"garment={garmentId} statusFlag={LegacyFreezeWarRuntimeStateStore.FreezeStatusFlag}; " +
                            "path=MsgSchedules.CurrentTournament.Join -> MsgFreezeWar.Join.")
                        .ConfigureAwait(false);
                    return true;
                }

            default:
                return false;
        }
    }

    private static bool HasLegacyFreezeWarLineSpell(WorldSession session)
    {
        return session.PlayerSpells.ContainsSpell(1_045) ||
            session.PlayerSpells.ContainsSpell(1_046) ||
            session.PlayerSpells.ContainsSpell(12_350);
    }

    private static bool HasLegacyFreezeWarLineWeapon(WorldSession session)
    {
        return LegacyEquipmentSelector
            .SelectCurrentEquipment(session.PlayerItems, session.AlternateEquipmentActive)
            .Any(static item =>
                item.Position % 50 is 4 or 5 &&
                IsLegacyFreezeWarLineWeapon(item.ItemId));
    }

    private static bool IsLegacyFreezeWarLineWeapon(uint itemId)
    {
        return itemId >= 614_000 && itemId <= 614_439 ||
            itemId >= 620_003 && itemId <= 620_439 ||
            itemId >= 410_003 && itemId <= 410_439 ||
            itemId >= 420_003 && itemId <= 420_439 ||
            itemId >= 421_003 && itemId <= 421_439 ||
            itemId is 410_501 or 410_601 or 410_701 or 410_801 or 410_901;
    }

    private static async ValueTask<bool> TryHandleLegacySuperGuildWarAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.SuperGuildWar";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Super Guild War follows Guild War rules and starts every Tuesday at 21:00."],
                        LegacySuperGuildWarOptions,
                        LegacySuperGuildWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 6 when IsLegacySuperGuildWarAlive(now):
                var protectedSession = session with
                {
                    Character = WithLegacyPlayerAttackProtection(
                        session.Character,
                        new DateTimeOffset(now),
                        TimeSpan.FromMilliseconds(5_000)),
                };
                await SendLegacyNpcTeleportAsync(
                        context,
                        protectedSession,
                        LegacySuperGuildWarNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(3_868, 232, 385, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 6:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Super Guild War has already ended."],
                        LegacyCompetitionCloseOptions,
                        LegacyCaptureTheFlagRewardAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 7:
                await TryClaimLegacySuperGuildWarRewardAsync(
                        context,
                        session,
                        optionId,
                        sourceMethod,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static bool IsLegacySuperGuildWarAlive(DateTime now)
    {
        return now.DayOfWeek == DayOfWeek.Tuesday && now.Hour < 22;
    }

    private static async ValueTask<bool> TryHandleLegacyPkWarQuitAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.PkWarQuit";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Hello, are you the last player alive?"],
                        LegacyPkWarQuitOptions,
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyCompetitionUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "PK War reward claiming is not available from this runtime yet.",
                        "Source requires the 13:00-13:45 reward window, PkWar.IsFinished, PkWar.TheLastPlayer, inventory space, and PkWar.GiveReward.",
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Do you want to teleport back to Twin City?"],
                        LegacyTwinCityConfirmOptions,
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyPkWarQuitNpcId,
                        optionId,
                        CreateLegacyTwinCityDestination(sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacySimpleSignUpAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string sourceMethod,
        string rootText,
        string gapReason,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [rootText],
                        LegacySignUpOptions,
                        LegacyCompetitionDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyCompetitionUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Tournament sign-up is not available from this runtime yet.",
                        gapReason,
                        LegacyCompetitionDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyCaptureTheFlagAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.CTF";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["How can I help you? Capture the Flag runs from 21:00 to 22:00, with a 500,000 CP reward for first place."],
                        LegacyCaptureTheFlagOptions,
                        LegacyCaptureTheFlagAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 4:
                await SendLegacyCompetitionUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Capture the Flag rewards are not available from this runtime yet.",
                        "Source requires CaptureTheFlag.Proces Dead, guild rank, guild CTF rank, claim flags, guild member rewards, CP and money grants, and global winner broadcasts.",
                        LegacyCaptureTheFlagRewardAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 5:
                await SendLegacyCompetitionUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Capture the Flag sign-up is not available from this runtime yet.",
                        "Source requires guild membership and MsgSchedules.CaptureTheFlag.Join.",
                        LegacyCaptureTheFlagAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyArenaNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.ArenaNpc";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "The arena is open. The admission fee is only 50 silver.",
                            "PK in the arena will not make you lose experience or equipped items, and you will revive where you die.",
                        ],
                        LegacyArenaNpcOptions,
                        LegacyArenaNpcAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1 when session.Character.Money < LegacyArenaNpcEntryFee:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["You do not have 50 silver with you."],
                        LegacyCompetitionCloseOptions,
                        LegacyArenaNpcAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyPaidArenaTeleportAsync(context, session, cancellationToken).ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacySkillTeamPkManagerAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.SkillTeamPkManager";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "The Skill Team PK Tournament opens at 19:55 every Wednesday. Contestants receive EXP and honor points each round, and the top 8 teams receive extra prizes.",
                        ],
                        LegacyTournamentRootOptions,
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "Equipment level, rank, quality, bonus, sockets, gems, blessing effects, and most subclass effects are limited. Clan, mentor, and guild Battle Power bonuses do not count.",
                        ],
                        [new LegacyNpcDialogOption("Go ahead.", 10), new LegacyNpcDialogOption("Not interested.", LegacyNpcClosedOption)],
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "You must team up to participate. Only team leaders can claim rewards. The tournament has groups for level 130+, 120-129, 100-119, and 1-99.",
                        ],
                        [new LegacyNpcDialogOption("What else?", 11), new LegacyNpcDialogOption("Not interested.", LegacyNpcClosedOption)],
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 11:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Talk to me at 19:55 to enter the waiting area. No teams may enter after the contest starts, and changing teams during the contest causes disqualification."],
                        LegacyCompetitionCloseOptions,
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyCompetitionUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Skill Team PK sign-up is not available from this runtime yet.",
                        "Source requires local team state and MsgSchedules.SkillTeamPkTournament.SignUp.",
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 5:
                await SendLegacyCompetitionUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Skill Team PK rewards are not available from this runtime yet.",
                        "Source checks inventory space and MsgSchedules.SkillTeamPkTournament.GetReward.",
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyTeamPkManagerAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.TeamPkManager";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Team PK Tournament is held every night at 18:55. The top 8 teams receive rewards, and every player earns EXP and honor points after each match."],
                        LegacyTournamentRootOptions,
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "Team PK starts at 19:00. Teams are divided into level groups: 1-99, 100-119, 120-129, and 130+. Players without a team are removed when the tournament starts.",
                            "Only the leader of a winning team can claim the final reward.",
                        ],
                        LegacyCompetitionCloseOptions,
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyCompetitionUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Team PK sign-up is not available from this runtime yet.",
                        "Source requires local team state and MsgSchedules.TeamPkTournament.SignUp.",
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 5:
                await SendLegacyCompetitionUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Team PK rewards are not available from this runtime yet.",
                        "Source checks inventory space and MsgSchedules.TeamPkTournament.GetReward.",
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
            case 4:
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyElitePkAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.ElitePk";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Elite PK is held every Friday at 23:00. Players are divided by level, top players receive prizes, and each match grants EXP and honor points."],
                        LegacyTournamentRootOptions,
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "Elite PK starts Friday night. Sign up shortly before the event, wait in the waiting area, and complete each round to advance.",
                            "If fewer than 8 players join, the finals are replaced by knockout rounds.",
                        ],
                        LegacyElitePkRuleOptions,
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Final rounds begin when fewer than 8 players remain. The top 8 receive prizes, and the top 3 receive special prizes."],
                        LegacyElitePkFinalRuleOptions,
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyCompetitionUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Elite PK sign-up is not available from this runtime yet.",
                        "Source calls MsgSchedules.ElitePkTournament.SignUp.",
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 5:
                await SendLegacyCompetitionUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Elite PK rewards are not available from this runtime yet.",
                        "Source checks inventory space and MsgSchedules.ElitePkTournament.GetReward.",
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 4:
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyClassPkWarAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.ClassPkWar";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Hello, are you the last player alive?"],
                        LegacyClassPkWarOptions,
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyCompetitionUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Class PK reward claiming is not available from this runtime yet.",
                        "Source depends on MsgSchedules.ClassPkWar.GetWar, CheckJoin, IsFinish, inventory space, and GetMyReward.",
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Do you want to teleport back to Twin City?"],
                        LegacyTwinCityConfirmOptions,
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyClassPkWarNpcId,
                        optionId,
                        CreateLegacyTwinCityDestination(sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyClassPkEnvoyAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.ClassPkEnvoy";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Everyone settle down! Class PK War sign-up starts when the event opens."],
                        LegacyClassPkEnvoyOptions,
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyCompetitionUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Class PK War sign-up is not available from this runtime yet.",
                        "Source requires MsgSchedules.ClassPkWar.GetWar, CheckJoin, and the legacy dynamic map id before teleporting to map 1764.",
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyClassPkEnvoy2Async(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.ClassPkEnvoy2";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Everyone settle down! Class PK War rewards can be claimed during the winner window."],
                        LegacyClassPkEnvoy2Options,
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyCompetitionUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Class PK War winner rewards are not available from this runtime yet.",
                        "Source scans live players on map 10620, checks the 23:40-23:55 reward window, grants items, CPs, study points, inner power, and class experience.",
                        LegacyCompetitionWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyCompetitionExitAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string sourceMethod,
        string? gapReason,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Hello there. Just let me know if you want to go to Twin City."],
                        [new LegacyNpcDialogOption("Twin City.", 2), new LegacyNpcDialogOption("Let me think.", LegacyNpcClosedOption)],
                        LegacyCompetitionDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Are you sure you want to leave for Twin City?"],
                        LegacyTwinCityConfirmOptions,
                        LegacyCompetitionDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        CreateLegacyTwinCityDestination(sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (gapReason is not null)
                {
                    await LogLegacyCompetitionGapAsync(context, sourceMethod, optionId, gapReason).ConfigureAwait(false);
                }

                return true;

            case 1:
                return true;

            default:
                return false;
        }
    }

    private static bool TryGetLegacyRefineryPackPage(
        byte optionId,
        out string text,
        out IReadOnlyList<LegacyNpcDialogOption> options)
    {
        switch (optionId)
        {
            case 11:
                text = "You can exchange 2 Refinery Badges for a primary refinery pack. Which pack do you want?";
                options = LegacyRefineryPageOneOptions;
                return true;
            case 12:
                text = "You can exchange 2 Refinery Badges for a primary refinery pack. Which pack do you want?";
                options = LegacyRefineryPageTwoOptions;
                return true;
            case 13:
                text = "You can exchange 2 Refinery Badges for a primary refinery pack. Which pack do you want?";
                options = LegacyRefineryPageThreeOptions;
                return true;
            case 14:
                text = "You can exchange 6 Refinery Badges for a medium refinery pack. Which pack do you want?";
                options = LegacyMediumRefineryPageOneOptions;
                return true;
            case 15:
                text = "You can exchange 6 Refinery Badges for a medium refinery pack. Which pack do you want?";
                options = LegacyMediumRefineryPageTwoOptions;
                return true;
            case 16:
                text = "You can exchange 6 Refinery Badges for a medium refinery pack. Which pack do you want?";
                options = LegacyMediumRefineryPageThreeOptions;
                return true;
            case 17:
                text = "You can exchange 12 Refinery Badges for a superior refinery pack. Which pack do you want?";
                options = LegacySuperiorRefineryPageOneOptions;
                return true;
            case 18:
                text = "You can exchange 12 Refinery Badges for a superior refinery pack. Which pack do you want?";
                options = LegacySuperiorRefineryPageTwoOptions;
                return true;
            case 19:
                text = "You can exchange 12 Refinery Badges for a superior refinery pack. Which pack do you want?";
                options = LegacySuperiorRefineryPageThreeOptions;
                return true;
            default:
                text = string.Empty;
                options = [];
                return false;
        }
    }

    private static bool TryGetLegacyRefineryPackReward(
        byte optionId,
        out LegacyRefineryPackReward reward)
    {
        int packIndex;
        string tierName;
        ushort badgeCost;
        Func<LegacyRefineryPackDefinition, uint> selectItemId;

        switch (optionId)
        {
            case >= 102 and <= 117:
                packIndex = optionId - 102;
                tierName = "Primary";
                badgeCost = 2;
                selectItemId = static pack => pack.PrimaryItemId;
                break;
            case >= 120 and <= 135:
                packIndex = optionId - 120;
                tierName = "Medium";
                badgeCost = 6;
                selectItemId = static pack => pack.MediumItemId;
                break;
            case >= 140 and <= 155:
                packIndex = optionId - 140;
                tierName = "Superior";
                badgeCost = 12;
                selectItemId = static pack => pack.SuperiorItemId;
                break;
            default:
                reward = default;
                return false;
        }

        var packDefinition = LegacyArenaManagerWangRefineryPacks[packIndex];
        reward = new LegacyRefineryPackReward(
            selectItemId(packDefinition),
            badgeCost,
            tierName,
            packDefinition.PackName,
            packDefinition.EquipmentAndEffect);
        return true;
    }

    private static async ValueTask ExchangeLegacyArenaManagerWangRefineryPackAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        LegacyRefineryPackReward reward,
        CancellationToken cancellationToken)
    {
        if (!TryReadLegacyItemType(context.WorldData, reward.ItemId, out var packItemType))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc CompetitionManagers refinery exchange " +
                    $"{FormatSession(context)} source={sourceMethod} option={optionId} " +
                    $"rewardItemId={reward.ItemId} reason=itemtype-missing; no badges were consumed.")
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyArenaManagerWangRefineryBadges(session.PlayerItems, reward.BadgeCost))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [
                        $"You don`t have enough Refinery Badges! You should prepare {reward.BadgeCost} " +
                        $"Refinery Badges if you want to get a {reward.TierName.ToLowerInvariant()} refinery pack!",
                    ],
                    [new LegacyNpcDialogOption("I see.", LegacyNpcClosedOption)],
                    LegacyArenaManagerWangAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["Your inventory is full! Please make 1 cell before you claim the pack!"],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyArenaManagerWangAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc CompetitionManagers refinery exchange " +
                    $"{FormatSession(context)} source={sourceMethod} option={optionId} " +
                    $"rewardItemId={reward.ItemId} reason=session-store-missing; " +
                    "no item persistence or badge mutation occurred.")
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc CompetitionManagers refinery exchange " +
                    $"{FormatSession(context)} source={sourceMethod} option={optionId} " +
                    $"rewardItemId={reward.ItemId} reason=playerItemsRoot-missing; no badges were consumed.")
                .ConfigureAwait(false);
            return;
        }

        var consumedItems = ConsumeLegacyArenaManagerWangRefineryBadges(
            session.PlayerItems,
            reward.BadgeCost,
            out var consumptionFrames);
        var grantedItem = CreatePurchasedItems(consumedItems, packItemType, count: 1, isBound: false)[0];
        var updatedItems = consumedItems with
        {
            InventoryItems = consumedItems.InventoryItems
                .Concat([grantedItem])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };

        try
        {
            new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                updatedSession.Character.Uid,
                updatedItems);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc CompetitionManagers refinery exchange persistence " +
                    $"{FormatSession(context)} source={sourceMethod} option={optionId} " +
                    $"rewardItemId={reward.ItemId} reason={ex.GetType().Name}; no session mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        foreach (var consumptionFrame in consumptionFrames)
        {
            await context.Connection.SendAsync(consumptionFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(grantedItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyItemFollowupsAsync(updatedContext, grantedItem, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(
                        $"You exchanged {reward.BadgeCost} Refinery Badges for a {reward.TierName} " +
                        $"{reward.PackName} Pack ({reward.EquipmentAndEffect})!")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc CompetitionManagers refinery exchange " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                $"badgeItemId={LegacyRefineryBadgeItemId} badgeCost={reward.BadgeCost} " +
                $"rewardItemId={reward.ItemId} bound=false.")
            .ConfigureAwait(false);
    }

    private static bool HasLegacyArenaManagerWangRefineryBadges(
        PlayerItemSnapshot items,
        ushort requiredCount)
    {
        var count = items.InventoryItems
            .Where(item => item.ItemId == LegacyRefineryBadgeItemId && item.Bound == 0)
            .Sum(item => (long)item.StackSize);
        return count >= requiredCount;
    }

    private static PlayerItemSnapshot ConsumeLegacyArenaManagerWangRefineryBadges(
        PlayerItemSnapshot items,
        ushort count,
        out IReadOnlyList<byte[]> frames)
    {
        var inventoryItems = items.InventoryItems.ToList();
        var mutationFrames = new List<byte[]>();
        var remaining = count;
        for (var index = 0; index < inventoryItems.Count && remaining > 0; index++)
        {
            var item = inventoryItems[index];
            if (item.ItemId != LegacyRefineryBadgeItemId || item.Bound != 0 || item.StackSize == 0)
            {
                continue;
            }

            var consumed = Math.Min(item.StackSize, remaining);
            remaining = checked((ushort)(remaining - consumed));
            if (item.StackSize > consumed)
            {
                var updatedItem = item.WithStackSize(checked((ushort)(item.StackSize - consumed)));
                inventoryItems[index] = updatedItem;
                mutationFrames.Add(
                    LegacyGameItemPacket
                        .FromSnapshotItem(updatedItem, mode: LegacyGameItemPacket.UpdateItemMode)
                        .ToFrameBytes());
                continue;
            }

            mutationFrames.Add(
                new MsgItemUsagePacket(
                    item.Uid,
                    DwParam: 0,
                    MsgItemUsageAction.RemoveInventory,
                    Timestamp: 0,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes());
            inventoryItems.RemoveAt(index);
            index--;
        }

        frames = mutationFrames;
        return items with { InventoryItems = inventoryItems.ToArray() };
    }

    private static async ValueTask SendLegacyPaidArenaTeleportAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.ArenaNpc";

        var preflight = await PrepareLegacyTeleportAsync(
                context,
                session,
                new LegacyTeleportRequest(sourceMethod, 1_005),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return;
        }

        session = preflight.Session;

        var chargedCharacter = session.Character with
        {
            Money = session.Character.Money - LegacyArenaNpcEntryFee,
        };
        var chargedSession = session with { Character = chargedCharacter };
        context.Sessions?.Set(context.Connection, chargedSession);

        await context.Connection.SendAsync(
                new LegacyMsgUpdatePacket(
                        CheckedUInt32(chargedCharacter.Uid, nameof(chargedCharacter.Uid)),
                        [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Money, checked((ulong)chargedCharacter.Money))])
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcTeleportAsync(
                context,
                chargedSession,
                LegacyArenaNpcId,
                optionId: 1,
                new LegacyNpcTeleportDestination(1_005, 51, 69, sourceMethod),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyCompetitionUnavailableAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string text,
        string gapReason,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [text],
                LegacyCompetitionCloseOptions,
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyCompetitionGapAsync(context, sourceMethod, optionId, gapReason).ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyCompetitionGapAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc CompetitionManagers " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}")
            .ConfigureAwait(false);
    }

    private static LegacyNpcTeleportDestination CreateLegacyTwinCityDestination(string sourceMethod)
    {
        return new LegacyNpcTeleportDestination(1_002, 410, 354, sourceMethod);
    }

    private sealed record LegacyRefineryPackDefinition(
        uint PrimaryItemId,
        uint MediumItemId,
        uint SuperiorItemId,
        string PackName,
        string EquipmentAndEffect);

    private readonly record struct LegacyRefineryPackReward(
        uint ItemId,
        ushort BadgeCost,
        string TierName,
        string PackName,
        string EquipmentAndEffect);
}

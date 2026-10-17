using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyJailGuildServicesNemesysConductortNpcId = 9_464;
    private const uint LegacyJailGuildServicesWardenZhangNpcId = 1_002;
    private const uint LegacyJailGuildServicesJailWardenNpcId = 42;
    private const uint LegacyJailGuildServicesJailJoinNpcId = 43;

    private const uint LegacyJailGuildServicesConfiscatorNpcId = 4_450;
    private const uint LegacyJailGuildServicesJailConfiscatorNpcId = 4_542;

    private const long LegacyJailGuildServicesNemesysTeleportCost = 100;
    private const long LegacyJailGuildServicesJailVisitCost = 1_000;

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesNemesysConductortSource =
        new("NpcHandler.NemesysConductort", "31092-31149");

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesWardenZhangSource =
        new("NpcHandler.WardenZhang", "31477-31538");

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesJailWardenSource =
        new("NpcHandler.JailWarden", "31540-31645");

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesJailJoinSource =
        new("NpcHandler.JailJoin", "31647-31716");

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesConfiscatorSource =
        new("NpcHandler.Confiscator", "39577-39640");

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesJailConfiscatorSource =
        new("NpcHandler.JailConfiscator", "39642-39705");

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesNemesysConductortAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesNemesysConductortSource.SourceMethod,
                    ["With only 100 silver, I can teleport you somewhere far away. Please choose your destination."],
                    [
                        new("Desert City.", 1),
                        new("Just passing by.", LegacyNpcClosedOption),
                    ],
                    avatarId: 1,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId is not (1 or 2))
        {
            return false;
        }

        if (session.Character.Money < LegacyJailGuildServicesNemesysTeleportCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesNemesysConductortSource.SourceMethod,
                    ["You do not have 100 silver with you."],
                    [new LegacyNpcDialogOption("I see.", LegacyNpcClosedOption)],
                    avatarId: 1,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var chargedSession = await ChargeLegacyNpcMoneyAsync(
                context,
                session,
                LegacyJailGuildServicesNemesysTeleportCost,
                LegacyJailGuildServicesNemesysConductortSource.SourceMethod,
                cancellationToken)
            .ConfigureAwait(false);
        var destination = optionId == 1
            ? new LegacyNpcTeleportDestination(1_000, 502, 646, LegacyJailGuildServicesNemesysConductortSource.SourceMethod)
            : new LegacyNpcTeleportDestination(1_036, 200, 204, LegacyJailGuildServicesNemesysConductortSource.SourceMethod);
        await SendLegacyNpcTeleportAsync(
                context,
                chargedSession,
                LegacyJailGuildServicesNemesysConductortNpcId,
                optionId,
                destination,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesWardenZhangAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesWardenZhangSource.SourceMethod,
                    [
                        "We always need bounty hunters. Murderers and thieves can evade our guards.",
                        "If you slay black-name criminals, you may receive a reward after they pay bail.",
                    ],
                    [
                        new("I want to claim my reward.", 1),
                        new("None of my business.", LegacyNpcClosedOption),
                    ],
                    avatarId: 37,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId != 1)
        {
            return false;
        }

        await SendLegacyJailGuildServicesUnavailableAsync(
                context,
                LegacyJailGuildServicesWardenZhangSource,
                LegacyJailGuildServicesWardenZhangNpcId,
                optionId,
                "bounty PK compensation state is not modeled",
                avatarId: 37,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesJailWardenAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
            {
                var banCount = ReadAttributeUInt64(session.Character.Attributes, "BanCount");
                if (banCount < 10)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            LegacyJailGuildServicesJailWardenSource.SourceMethod,
                            [
                                "Lucky for you, the player who killed you may drop the murder charges if you pay for your crimes.",
                                "Pay properly and you can leave this place.",
                            ],
                            [
                                new("Let me out of here.", 1),
                                new("Any chance for a discount?", 2),
                                new("I would rather stay here.", LegacyNpcClosedOption),
                            ],
                            avatarId: 37,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesJailWardenSource.SourceMethod,
                        ["I can help you leave here for 50 donation points."],
                        [new LegacyNpcDialogOption("I have the points.", 5)],
                        avatarId: 286,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesJailWardenSource.SourceMethod,
                        [
                            "Red-name players with 30-99 PK points can leave for free.",
                            "Black-name players with 100 or more PK points must pay CPs if they killed many people.",
                        ],
                        [
                            new("Okay, I will pay.", 1),
                            new("I would rather stay here.", LegacyNpcClosedOption),
                        ],
                        avatarId: 37,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                if (session.Character.PkPoints < 100)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            LegacyJailGuildServicesJailWardenSource.SourceMethod,
                            ["You do not need to stay here anymore. Let me help you out."],
                            [
                                new("Okay, thanks.", 3),
                                new("I want to stay here.", LegacyNpcClosedOption),
                            ],
                            avatarId: 37,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesJailWardenSource.SourceMethod,
                        [
                            "You murdered a lot. If you pay " +
                            LegacyJailGuildServicesJailBailCost(session.Character.PkPoints).ToString(CultureInfo.InvariantCulture) +
                            " CPs for your sins, I will let you go.",
                        ],
                        [
                            new("Here is my payment.", 4),
                            new("No, thanks.", LegacyNpcClosedOption),
                        ],
                        avatarId: 37,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyJailGuildServicesJailWardenNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_002, 410, 354, LegacyJailGuildServicesJailWardenSource.SourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 4:
            {
                var cost = LegacyJailGuildServicesJailBailCost(session.Character.PkPoints);
                if (session.Character.ConquerPoints < cost)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            LegacyJailGuildServicesJailWardenSource.SourceMethod,
                            [$"You do not have {cost.ToString(CultureInfo.InvariantCulture)} CPs with you."],
                            LegacyJailGuildServicesISeeOptions,
                            avatarId: 37,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await SendLegacyJailGuildServicesUnavailableAsync(
                        context,
                        LegacyJailGuildServicesJailWardenSource,
                        LegacyJailGuildServicesJailWardenNpcId,
                        optionId,
                        "PK compensation, crime table writes, CP debit, and random release teleport are not modeled",
                        avatarId: 37,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            case 5:
                await SendLegacyJailGuildServicesUnavailableAsync(
                        context,
                        LegacyJailGuildServicesJailWardenSource,
                        LegacyJailGuildServicesJailWardenNpcId,
                        optionId,
                        "donation-point jail release accounting is not modeled",
                        avatarId: 286,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesJailJoinAsync(
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
                        LegacyJailGuildServicesJailJoinSource.SourceMethod,
                        ["What can I do for you?"],
                        [
                            new("Visit the jail.", 1),
                            new("Just passing by.", LegacyNpcClosedOption),
                        ],
                        avatarId: 37,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesJailJoinSource.SourceMethod,
                        ["Give me 1000 silver and I will teleport you there."],
                        [
                            new("Here are 1000 silver.", 2),
                            new("I will stay here.", LegacyNpcClosedOption),
                        ],
                        avatarId: 37,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                if (session.Character.PkPoints >= 30)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            LegacyJailGuildServicesJailJoinSource.SourceMethod,
                            ["Sorry, your PK points are 30 or higher."],
                            LegacyJailGuildServicesISeeOptions,
                            avatarId: 37,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (session.Character.Money <= LegacyJailGuildServicesJailVisitCost)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            LegacyJailGuildServicesJailJoinSource.SourceMethod,
                            ["You do not have 1000 silver with you."],
                            LegacyJailGuildServicesISeeOptions,
                            avatarId: 37,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                var chargedSession = await ChargeLegacyNpcMoneyAsync(
                        context,
                        session,
                        LegacyJailGuildServicesJailVisitCost,
                        LegacyJailGuildServicesJailJoinSource.SourceMethod,
                        cancellationToken)
                    .ConfigureAwait(false);
                await SendLegacyNpcTeleportAsync(
                        context,
                        chargedSession,
                        LegacyJailGuildServicesJailJoinNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(6_000, 29, 72, LegacyJailGuildServicesJailJoinSource.SourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }


    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesConfiscatorAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        LegacyJailGuildServicesSourceInfo source,
        uint npcId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        [
                            "Red-name and black-name players can redeem detained equipment within 7 days.",
                            "The CPs they pay are rewarded to the player who killed them. If they do not pay, the detained equipment becomes a reward.",
                        ],
                        [
                            new("I want to redeem my equipment.", 1),
                            new("I want to claim my rewards.", 3),
                            new("What?", LegacyNpcClosedOption),
                        ],
                        avatarId: 102,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["You can pay some CPs to redeem your equipment."],
                        [
                            new("I want my equipment.", 2),
                            new("Let me think.", LegacyNpcClosedOption),
                        ],
                        avatarId: 102,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyJailGuildServicesUnavailableAsync(
                        context,
                        source,
                        npcId,
                        optionId,
                        "confiscated item redeem state and detained-item GUI are not modeled",
                        avatarId: 102,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        [
                            "The red-name or black-name player you killed can redeem equipment within 7 days.",
                            "You will be rewarded with the CPs they pay, or the detained equipment if they do not pay.",
                        ],
                        [
                            new("I want my equipment.", 4),
                            new("Let me think.", LegacyNpcClosedOption),
                        ],
                        avatarId: 102,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 4:
                await SendLegacyJailGuildServicesUnavailableAsync(
                        context,
                        source,
                        npcId,
                        optionId,
                        "confiscated item claim state and detained-item GUI are not modeled",
                        avatarId: 102,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }


    private static long LegacyJailGuildServicesJailBailCost(long pkPoints)
    {
        return 5_000 + pkPoints * 10;
    }
}

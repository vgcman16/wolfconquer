namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyJailGuildServicesGuildConductorNpcId = 380;
    private const uint LegacyJailGuildServicesTCCaptainNpcId = 2_001;
    private const uint LegacyJailGuildServicesPCCaptainNpcId = 2_002;
    private const uint LegacyJailGuildServicesAMCaptainNpcId = 2_003;
    private const uint LegacyJailGuildServicesDCCaptainNpcId = 2_004;
    private const uint LegacyJailGuildServicesBCaptainNpcId = 2_005;
    private const uint LegacyJailGuildServicesMCaptainNpcId = 2_006;

    private const string LegacyJailGuildServicesGuildWarAliveProcess = "Alive";

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesGuildConductorSource =
        new("NpcHandler.GuildConductor", "38191-38361");

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesMCaptainSource =
        new("NpcHandler.MCaptain", "38801-38917");

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesBCaptainSource =
        new("NpcHandler.BCaptain", "38919-39035");

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesDCCaptainSource =
        new("NpcHandler.DCCaptain", "39037-39153");

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesAMCaptainSource =
        new("NpcHandler.AMCaptain", "39155-39271");

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesPCCaptainSource =
        new("NpcHandler.PCCaptain", "39273-39389");

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesTCCaptainSource =
        new("NpcHandler.TCCaptain", "39391-39575");

    private static readonly IReadOnlyDictionary<uint, LegacyJailGuildServicesSourceInfo> LegacyJailGuildServicesCaptainSources =
        new Dictionary<uint, LegacyJailGuildServicesSourceInfo>
        {
            [LegacyJailGuildServicesMCaptainNpcId] = LegacyJailGuildServicesMCaptainSource,
            [LegacyJailGuildServicesBCaptainNpcId] = LegacyJailGuildServicesBCaptainSource,
            [LegacyJailGuildServicesDCCaptainNpcId] = LegacyJailGuildServicesDCCaptainSource,
            [LegacyJailGuildServicesAMCaptainNpcId] = LegacyJailGuildServicesAMCaptainSource,
            [LegacyJailGuildServicesPCCaptainNpcId] = LegacyJailGuildServicesPCCaptainSource,
            [LegacyJailGuildServicesTCCaptainNpcId] = LegacyJailGuildServicesTCCaptainSource,
        };

    private static readonly IReadOnlyDictionary<byte, LegacyNpcTeleportDestination> LegacyJailGuildServicesCaptainTeleports =
        new Dictionary<byte, LegacyNpcTeleportDestination>
        {
            [200] = new(1_000, 476, 634, "NpcHandler.Captain"),
            [201] = new(1_000, 82, 321, "NpcHandler.Captain"),
            [211] = new(1_011, 229, 258, "NpcHandler.Captain"),
            [215] = new(1_015, 791, 568, "NpcHandler.Captain"),
            [220] = new(1_020, 569, 620, "NpcHandler.Captain"),
        };

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesGuildConductorAsync(
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
                        LegacyJailGuildServicesGuildConductorSource.SourceMethod,
                        ["What can I do for you? The guild war runs on Friday from 21:00 to 22:00."],
                        [
                            new("Enter the guild area.", 1),
                            new("Buy statue.", 2),
                            new("Just passing by.", LegacyNpcClosedOption),
                        ],
                        avatarId: 123,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
            {
                var process = ReadAttributeString(
                    session.Character.Attributes,
                    "LegacyNewGuildWarProcess",
                    "NewGuildWarProcess",
                    "GuildWarProcess");
                if (string.IsNullOrWhiteSpace(process))
                {
                    await SendLegacyJailGuildServicesUnavailableAsync(
                            context,
                            LegacyJailGuildServicesGuildConductorSource,
                            LegacyJailGuildServicesGuildConductorNpcId,
                            optionId,
                            "guild war process state is not loaded",
                            avatarId: 123,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (process.Equals(LegacyJailGuildServicesGuildWarAliveProcess, StringComparison.OrdinalIgnoreCase))
                {
                    await SendLegacyJailGuildServicesUnavailableAsync(
                            context,
                            LegacyJailGuildServicesGuildConductorSource,
                            LegacyJailGuildServicesGuildConductorNpcId,
                            optionId,
                            "alive guild war random entry teleport is not modeled",
                            avatarId: 123,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyJailGuildServicesGuildConductorNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_038, 349, 340, LegacyJailGuildServicesGuildConductorSource.SourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesGuildConductorSource.SourceMethod,
                        ["A statue costs 50,000 silver. Are you sure you want to buy one?"],
                        [
                            new("Yes.", 3),
                            new("No, thanks.", LegacyNpcClosedOption),
                        ],
                        avatarId: 123,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
            case 4:
            case 5:
                await SendLegacyJailGuildServicesUnavailableAsync(
                        context,
                        LegacyJailGuildServicesGuildConductorSource,
                        LegacyJailGuildServicesGuildConductorNpcId,
                        optionId,
                        "guild statue purchase inventory grant and silver debit are not modeled in this slice",
                        avatarId: 123,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 6:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyJailGuildServicesGuildConductorNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(3_868, 232, 385, LegacyJailGuildServicesGuildConductorSource.SourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesCaptainAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        LegacyJailGuildServicesSourceInfo source,
        CancellationToken cancellationToken)
    {
        if (LegacyJailGuildServicesCaptainTeleports.TryGetValue(optionId, out var destination))
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    destination with { SourceMethod = source.SourceMethod },
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (npcId == LegacyJailGuildServicesTCCaptainNpcId)
        {
            return await TryHandleLegacyJailGuildServicesTwinCityCaptainAsync(
                    context,
                    session,
                    optionId,
                    source,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await TryHandleLegacyJailGuildServicesRegularCaptainAsync(
                context,
                session,
                npcId,
                optionId,
                source,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesRegularCaptainAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        LegacyJailGuildServicesSourceInfo source,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyJailGuildServicesCaptainDailyDialogAsync(context, session, source, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 103:
                await SendLegacyJailGuildServicesUnavailableAsync(
                        context,
                        source,
                        npcId,
                        optionId,
                        "Demon Exterminator captain quest state and dynamic monster list are not modeled",
                        avatarId: 102,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesTwinCityCaptainAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        LegacyJailGuildServicesSourceInfo source,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        [
                            "Good day, my friend. I am the Twin City Captain, in charge of public security here.",
                            "Would you like to contribute a bit to the public? You will be rewarded.",
                        ],
                        [
                            new("What can I do for you?", 240),
                            new("I want to take a Daily Quest.", 230),
                        ],
                        avatarId: 10,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 240:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        [
                            "The Pheasants outside Twin City are becoming more aggressive.",
                            "Defeat 10 of them for the safety of the people in Twin City.",
                        ],
                        [
                            new("Okay, right on my way.", 232),
                            new("I want to take a Daily Quest.", 230),
                        ],
                        avatarId: 10,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 231:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["You need professional guidance to grow stronger. Find your class NPC in the Job Center for promotion."],
                        [
                            new("Job promotion 1.", 234),
                            new("I want to take a Daily Quest.", 230),
                        ],
                        avatarId: 10,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 233:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["Good job. You returned peace to Twin City."],
                        [new LegacyNpcDialogOption("Thank you.", 233)],
                        avatarId: 10,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 230:
                await SendLegacyJailGuildServicesCaptainDailyDialogAsync(context, session, source, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 103:
            case 232:
            case 234:
                await SendLegacyJailGuildServicesUnavailableAsync(
                        context,
                        source,
                        LegacyJailGuildServicesTCCaptainNpcId,
                        optionId,
                        "Twin City captain quest or Demon Exterminator state is not modeled",
                        avatarId: 102,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask SendLegacyJailGuildServicesCaptainDailyDialogAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyJailGuildServicesSourceInfo source,
        CancellationToken cancellationToken)
    {
        var captainTimes = ReadAttributeUInt64(session.Character.Attributes, "TCCaptainTimes", "CaptainDailyTimes");
        if (captainTimes >= 3)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source.SourceMethod,
                    ["You already helped me out today, warrior. Please come back tomorrow."],
                    LegacyJailGuildServicesOkayOptions,
                    avatarId: 102,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await SendLegacyNpcDialogAsync(
                context,
                source.SourceMethod,
                ["We are always looking for brave adventurers to protect the city from monsters. Are you interested in helping?"],
                [
                    new("Yes, please.", 103),
                    new("I am busy.", LegacyNpcClosedOption),
                ],
                avatarId: 102,
                cancellationToken)
            .ConfigureAwait(false);
    }

}

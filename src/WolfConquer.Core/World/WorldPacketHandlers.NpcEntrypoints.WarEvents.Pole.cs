namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyPoleDominationNpcId = 770_001;
    private const uint LegacyTopPoleNpcId = 43_431;
    private const uint LegacyQoutTopPoleNpcId = 43_432;
    private const uint LegacyGuildWarScoreNpcId = 6_525;
    private const ushort LegacyPoleWarEventAvatarId = 63;
    private const ushort LegacyPoleDominationAvatarId = 110;

    private static readonly LegacyNpcDialogOption[] LegacyPoleDominationOptions =
    [
        new("Join the first pole battle.", 5),
        new("Claim the first pole reward.", 1),
        new("Claim the second pole reward.", 2),
        new("Claim the third pole reward.", 3),
        new("Claim the fourth pole reward.", 4),
        new("Thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyTopPoleOptions =
    [
        new("Enter.", 1),
        new("Claim reward.", 2),
        new("Just passing by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyQoutTopPoleOptions =
    [
        new("Return to Twin City.", 1),
        new("Just passing by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyGuildWarScoreOptions =
    [
        new("Enter.", 1),
        new("Claim reward.", 2),
        new("Just looking around.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyPoleWarEventOkayOptions =
    [
        new("Okay.", LegacyNpcClosedOption),
    ];

    internal static ValueTask<bool> TryHandleLegacyPoleWarEventNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return TryHandleLegacyPoleWarEventNpcAsync(
            context,
            session,
            npcId,
            optionId,
            DateTime.Now,
            cancellationToken);
    }

    internal static async ValueTask<bool> TryHandleLegacyPoleWarEventNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyPoleDominationNpcId => await TryHandleLegacyPoleDominationAsync(
                    context,
                    session,
                    optionId,
                    now,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyTopPoleNpcId => await TryHandleLegacyTopPoleAsync(
                    context,
                    session,
                    optionId,
                    now,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyQoutTopPoleNpcId => await TryHandleLegacyQoutTopPoleAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyGuildWarScoreNpcId => await TryHandleLegacyGuildWarScoreAsync(
                    context,
                    session,
                    optionId,
                    now,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyGuild6PoleWarNpcId => await TryHandleLegacyGuild6PoleWarAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyPoleDominationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.PoleDomination";

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [
                        "This is the Pole Domination reward center.",
                        "Only the winning guild leader can claim rewards. The war starts at 15:00.",
                        "There are four pole battles, and a new one starts every 15 minutes.",
                    ],
                    LegacyPoleDominationOptions,
                    LegacyPoleDominationAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 5)
        {
            if (now.Hour == 15 && now.Minute < 55)
            {
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyPoleDominationNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(11_083, 231, 242, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Sorry, the war has not started yet. It starts at 15:00."],
                        LegacyPoleWarEventOkayOptions,
                        LegacyPoleDominationAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return true;
        }

        if (optionId is >= 1 and <= 4)
        {
            var rewardLane = optionId switch
            {
                1 => "PoleDomination",
                2 => "PoleDominationPC",
                3 => "PoleDominationDC",
                4 => "PoleDominationBI",
                _ => "PoleDomination",
            };
            await SendLegacyPoleWarRewardUnavailableAsync(
                    context,
                    sourceMethod,
                    LegacyPoleDominationAvatarId,
                    optionId,
                    $"{rewardLane} reward lane is kept safe-fenced: no direct NpcHandler.PoleDomination branch was found in the external source references.",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyTopPoleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.TopPole";

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["Hi, I am the Top Pole leader. The event runs from 11:00 to 11:30. How can I help?"],
                    LegacyTopPoleOptions,
                    LegacyPoleWarEventAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            if (now.Hour == 11 && now.Minute < 30)
            {
                await SendLegacyNpcTeleportAsync(
                        context,
                        ReviveLegacyPoleWarEventSessionIfNeeded(session),
                        LegacyTopPoleNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_737, 90, 90, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await context.Connection.SendAsync(
                        LegacyMsgMessagePacket
                            .SystemTalk("The Top Pole battle has ended. Please come back after 11:00.")
                            .ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return true;
        }

        if (optionId == 2)
        {
            await TryClaimLegacyTopPoleRewardAsync(
                    context,
                    session,
                    sourceMethod,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyQoutTopPoleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.QoutTopPole";

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["Do you want to return to Twin City?"],
                    LegacyQoutTopPoleOptions,
                    LegacyPoleWarEventAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    ReviveLegacyPoleWarEventSessionIfNeeded(session),
                    LegacyQoutTopPoleNpcId,
                    optionId,
                    new LegacyNpcTeleportDestination(1_002, 400, 355, sourceMethod),
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyGuildWarScoreAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.GuildWarScore";

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["Hi, I manage Guild War Score. The event runs from 19:30 to 20:00. How can I help?"],
                    LegacyGuildWarScoreOptions,
                    LegacyPoleWarEventAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            if (now.Hour == 19 && now.Minute >= 30 || now.Hour == 20 && now.Minute == 0)
            {
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyGuildWarScoreNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(6_784, 150, 96, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["The Guild War Score event has not started yet."],
                        LegacyPoleWarEventOkayOptions,
                        LegacyPoleWarEventAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return true;
        }

        if (optionId == 2)
        {
            await TryClaimLegacyClanWarScoreRewardAsync(
                    context,
                    session,
                    optionId,
                    sourceMethod,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask SendLegacyPoleWarRewardUnavailableAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        ushort avatarId,
        byte optionId,
        string evidence,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                ["No reward is available from this NPC in the current runtime state."],
                LegacyPoleWarEventOkayOptions,
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc PoleWarEvent reward unavailable " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {evidence}")
            .ConfigureAwait(false);
    }

    private static WorldSession ReviveLegacyPoleWarEventSessionIfNeeded(WorldSession session)
    {
        return session.Character.MinHitPoints >= 1
            ? session
            : session with { Character = session.Character with { MinHitPoints = 1 } };
    }
}

using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyGuild6PoleWarNpcId = 73_135;
    private const ushort LegacyGuild6PoleWarAvatarId = 36;
    private const string LegacyGuild6PoleWarSourceMethod = "NpcHandler.Guild6PoleWar";

    private static readonly LegacyGuild6PoleWarSchedule[] LegacyGuild6PoleWarSchedules =
    [
        new(3, "Guild6PoleWar", 1, new LegacyNpcTeleportDestination(10_070, 431, 398, LegacyGuild6PoleWarSourceMethod)),
        new(4, "Guild6PoleWar2", 2, new LegacyNpcTeleportDestination(10_070, 427, 323, LegacyGuild6PoleWarSourceMethod)),
        new(5, "Guild6PoleWar3", 3, new LegacyNpcTeleportDestination(10_070, 322, 326, LegacyGuild6PoleWarSourceMethod)),
        new(6, "Guild6PoleWar4", 4, new LegacyNpcTeleportDestination(10_070, 343, 328, LegacyGuild6PoleWarSourceMethod)),
        new(7, "Guild6PoleWar5", 5, new LegacyNpcTeleportDestination(10_070, 454, 307, LegacyGuild6PoleWarSourceMethod)),
        new(8, "Guild6PoleWar6", 6, new LegacyNpcTeleportDestination(10_070, 434, 357, LegacyGuild6PoleWarSourceMethod)),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyGuild6PoleWarClaimOptions =
    [
        new("Claim GuildPole[1]War.", 3),
        new("Claim GuildPole[2]War.", 4),
        new("Claim GuildPole[3]War.", 5),
        new("Claim GuildPole[4]War.", 6),
        new("Claim GuildPole[5]War.", 7),
        new("Claim GuildPole[6]War.", 8),
        new("Never mind.", LegacyNpcClosedOption),
    ];

    internal static async ValueTask<bool> TryHandleLegacyGuild6PoleWarAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyGuild6PoleWarIntroAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await TryEnterLegacyGuild6PoleWarAsync(context, session, optionId, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyGuild6PoleWarSourceMethod,
                        ["Which Guild Pole War reward do you want to claim?"],
                        LegacyGuild6PoleWarClaimOptions,
                        LegacyGuild6PoleWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                if (TryGetLegacyGuild6PoleWarScheduleByOption(optionId, out var schedule))
                {
                    await TryClaimLegacyGuild6PoleWarRewardAsync(
                            context,
                            session,
                            schedule,
                            optionId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                return false;
        }
    }

    private static async ValueTask SendLegacyGuild6PoleWarIntroAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var options = TryGetActiveLegacyGuild6PoleWarSchedule(session.Character, out _)
            ? new LegacyNpcDialogOption[]
            {
                new("Enter.", 1),
                new("Claim reward.", 2),
                new("Okay. I see.", LegacyNpcClosedOption),
            }
            : [new("Claim reward.", 2), new("Okay. I see.", LegacyNpcClosedOption)];

        await SendLegacyNpcDialogAsync(
                context,
                LegacyGuild6PoleWarSourceMethod,
                [
                    $"Hi {session.Character.Name}, I am the GuildPoleWar manager. How can I help?",
                    "Tournament Start 00:00 To 00:29.",
                ],
                options,
                LegacyGuild6PoleWarAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask TryEnterLegacyGuild6PoleWarAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveLegacyGuild6PoleWarSchedule(session.Character, out var schedule))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyGuild6PoleWarSourceMethod,
                    ["The Guild Pole War is not started yet."],
                    LegacyPoleWarEventOkayOptions,
                    LegacyGuild6PoleWarAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await SendLegacyNpcTeleportAsync(
                context,
                ReviveLegacyPoleWarEventSessionIfNeeded(session),
                LegacyGuild6PoleWarNpcId,
                optionId,
                schedule.EntryDestination,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc Guild6PoleWar enter " +
                $"{FormatSession(context)} source={LegacyGuild6PoleWarSourceMethod} option={optionId} " +
                $"schedule={schedule.ScheduleName} pole={schedule.PoleNumber.ToString(CultureInfo.InvariantCulture)}.")
            .ConfigureAwait(false);
    }

    private static bool TryGetActiveLegacyGuild6PoleWarSchedule(
        CharacterSnapshot character,
        out LegacyGuild6PoleWarSchedule schedule)
    {
        foreach (var candidate in LegacyGuild6PoleWarSchedules)
        {
            if (IsLegacyGuild6PoleWarProcessAlive(character, candidate.ScheduleName))
            {
                schedule = candidate;
                return true;
            }
        }

        schedule = default;
        return false;
    }

    private static bool TryGetLegacyGuild6PoleWarScheduleByOption(
        byte optionId,
        out LegacyGuild6PoleWarSchedule schedule)
    {
        foreach (var candidate in LegacyGuild6PoleWarSchedules)
        {
            if (candidate.OptionId == optionId)
            {
                schedule = candidate;
                return true;
            }
        }

        schedule = default;
        return false;
    }

    private readonly record struct LegacyGuild6PoleWarSchedule(
        byte OptionId,
        string ScheduleName,
        int PoleNumber,
        LegacyNpcTeleportDestination EntryDestination);
}

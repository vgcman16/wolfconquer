namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyFrozenGrottoGeneralNpcId = 6_144;
    private const string LegacyFrozenGrottoGeneralSource = "NpcHandler.FrozenGrottoGeneral";
    private const ushort LegacyFrozenGrottoGeneralAvatarId = 57;

    private static async ValueTask<bool> TryHandleLegacyLevelHuntExitAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId == LegacyFrozenGrottoGeneralNpcId)
        {
            return await TryHandleLegacyFrozenGrottoGeneralAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!TryGetLegacyLevelHuntExitSource(npcId, out var sourceMethod))
        {
            return false;
        }

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [$"Hey Brothr {session.Character.Name} Do You Want Leve this map to Twin City"],
                        LegacyLevelHuntExitOptions,
                        LegacyLevelHuntExitAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_002, 410, 355, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyFrozenGrottoGeneralAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyFrozenGrottoGeneralSource,
                    GetLegacyFrozenGrottoGeneralDialogTexts(session.Character.Level),
                    GetLegacyFrozenGrottoGeneralOptions(session.Character.Level),
                    LegacyFrozenGrottoGeneralAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (TryGetLegacyFrozenGrottoGeneralTeleport(optionId, out var destination))
        {
            await SendLegacyNpcTeleportAsync(context, session, npcId, optionId, destination, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static IReadOnlyList<LegacyNpcDialogOption> GetLegacyFrozenGrottoGeneralOptions(long level)
    {
        if (level >= 120)
        {
            return
            [
                new("I~see.", LegacyNpcClosedOption),
            ];
        }

        return
        [
            new("Okay,~thanks.", GetLegacyFrozenGrottoGeneralDestinationOption(level)),
            new("I~have~no~interest.", LegacyNpcClosedOption),
        ];
    }

    private static byte GetLegacyFrozenGrottoGeneralDestinationOption(long level)
    {
        return level switch
        {
            < 28 => 1,
            < 48 => 2,
            < 68 => 3,
            < 88 => 4,
            < 103 => 5,
            < 120 => 6,
            _ => LegacyNpcClosedOption,
        };
    }

    private static IReadOnlyList<string> GetLegacyFrozenGrottoGeneralDialogTexts(long level)
    {
        return level switch
        {
            < 28 =>
            [
                "You can get the monster hunter quest from the TC Captain now.",
                "It is a great place to level up, and here is a steed to take you there.",
            ],
            < 48 =>
            [
                "You can take the quest from the PC Captain now.",
                "You can level up more quickly there, and here is a steed to take you there.",
            ],
            < 68 =>
            [
                "You should take the quests from the AC Captain.",
                "It is the right place to level up, and here is a steed to take you there.",
            ],
            < 88 =>
            [
                "You are powerful enough to take the quests from the DC Captain now.",
                "Here is a steed to take you there.",
            ],
            < 103 =>
            [
                "You are able to take quests from the BI Captain now.",
                "He needs your help, and here is a steed to take you there.",
            ],
            < 120 =>
            [
                "You are strong enough to kill monsters in Mystic Castle.",
                "Take a quest from the MC Captain to level up quickly.",
            ],
            _ =>
            [
                "Cloud Saint in Market is looking for heroes to exterminate monsters.",
                "If you can finish the quest, you will receive EXP Balls as rewards.",
            ],
        };
    }

    private static bool TryGetLegacyFrozenGrottoGeneralTeleport(
        byte optionId,
        out LegacyNpcTeleportDestination destination)
    {
        destination = optionId switch
        {
            1 => new LegacyNpcTeleportDestination(1_002, 311, 346, LegacyFrozenGrottoGeneralSource),
            2 => new LegacyNpcTeleportDestination(1_011, 233, 260, LegacyFrozenGrottoGeneralSource),
            3 => new LegacyNpcTeleportDestination(1_020, 571, 621, LegacyFrozenGrottoGeneralSource),
            4 => new LegacyNpcTeleportDestination(1_000, 483, 634, LegacyFrozenGrottoGeneralSource),
            5 => new LegacyNpcTeleportDestination(1_015, 792, 571, LegacyFrozenGrottoGeneralSource),
            6 => new LegacyNpcTeleportDestination(1_000, 85, 321, LegacyFrozenGrottoGeneralSource),
            _ => default,
        };

        return optionId is >= 1 and <= 6;
    }
}

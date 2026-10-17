namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyCupCasinoServicesGameManagerNpcId = 6_297;
    private const uint LegacyCupCasinoServicesPokerCasinoHostessNpcId = 6_298;
    private const uint LegacyCupCasinoServicesPokerCpsCasinoNpcId = 6_299;
    private const uint LegacyCupCasinoServicesCasinoHostessNpcId = 16_915;

    private const ushort LegacyCupCasinoServicesCasinoAvatarId = 175;

    private static readonly LegacyNpcDialogOption[] LegacyCupCasinoServicesPokerCpsCasinoOptions =
    [
        new("I have no interest in it.", 1),
        new("Enter Poker Room.", 2),
        new("I still want to play.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCupCasinoServicesCasinoHostessOptions =
    [
        new("I have no interest in it.", 1),
        new("I still want to play.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCupCasinoServicesGameManagerOptions =
    [
        new("Enter~Poker~Room.", 1),
        new("Not~interested.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> LegacyCupCasinoServicesTryHandleGameManagerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.GameManager";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["I can teleport you to the poker room, where you can enjoy Texas Hold'em Poker."],
                        LegacyCupCasinoServicesGameManagerOptions,
                        LegacyCupCasinoServicesCasinoAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_858, 157, 119, source),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_860, 55, 62, source),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyCupCasinoServicesTryHandlePokerCpsCasinoAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.PokerCpsCasino";

        switch (optionId)
        {
            case 0:
                await LegacyCupCasinoServicesSendCasinoGreetingAsync(
                        context,
                        source,
                        LegacyCupCasinoServicesPokerCpsCasinoOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_002, 410, 354, source),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await LegacyCupCasinoServicesSendUnavailableAsync(
                        context,
                        source,
                        "27424-27427",
                        npcId,
                        optionId,
                        "Poker room entry is unavailable here until casino room state is modeled safely.",
                        "teleport to Poker Room map 1858 and enter casino play state",
                        LegacyCupCasinoServicesCasinoAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyCupCasinoServicesTryHandlePokerCasinoHostessAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.PokerCasinoHostess";

        switch (optionId)
        {
            case 0:
                await LegacyCupCasinoServicesSendCasinoGreetingAsync(
                        context,
                        source,
                        LegacyCupCasinoServicesCasinoHostessOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_002, 410, 354, source),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await LegacyCupCasinoServicesSendUnavailableAsync(
                        context,
                        source,
                        "27446-27449",
                        npcId,
                        optionId,
                        "VIP hall entry is unavailable here until casino room state is modeled safely.",
                        "teleport to VIP Hall map 1860 and enter casino play state",
                        LegacyCupCasinoServicesCasinoAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyCupCasinoServicesTryHandleCasinoHostessAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.CasinoHostess";

        switch (optionId)
        {
            case 0:
                await LegacyCupCasinoServicesSendCasinoGreetingAsync(
                        context,
                        source,
                        LegacyCupCasinoServicesCasinoHostessOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_002, 410, 354, source),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask LegacyCupCasinoServicesSendCasinoGreetingAsync(
        WorldPacketHandlerContext context,
        string source,
        IReadOnlyList<LegacyNpcDialogOption> options,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                source,
                [
                    "Greetings. I am glad to be at your service.",
                    "Winning or losing is not the main point here. We hope you enjoy yourself and stay in good health.",
                ],
                options,
                LegacyCupCasinoServicesCasinoAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }
}

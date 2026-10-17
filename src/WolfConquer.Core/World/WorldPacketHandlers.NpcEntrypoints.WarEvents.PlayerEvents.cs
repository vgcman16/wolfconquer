namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyCouplesPkNpcId = 87_765;
    private const uint LegacyWarOfPlayersNpcId = 6_906;
    private const uint LegacyChampionsOfWarrNpcId = 4_433;
    private const ushort LegacyPlayerWarEventAvatarId = 63;

    private static readonly LegacyNpcDialogOption[] LegacyCouplesPkOptions =
    [
        new("Register me.", 1),
        new("I will come back later.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyPlayerWarEventOptions =
    [
        new("Enter.", 1),
        new("Reward.", 2),
        new("Just passing by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyPlayerWarEventCloseOptions =
    [
        new("Okay.", LegacyNpcClosedOption),
    ];

    internal static async ValueTask<bool> TryHandleLegacyPlayerWarEventNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyCouplesPkNpcId => await TryHandleLegacyCouplesPkAsync(
                    context,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyWarOfPlayersNpcId => await TryHandleLegacyWarOfPlayersAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyChampionsOfWarrNpcId => await TryHandleLegacyChampionsOfWarrAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyCouplesPkAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.CouplesPK";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "Hello, couples! I manage the Couples PK event, held on Saturdays. You have 5 minutes to form a team with your partner and register. Registration is open from 7:00 to 7:15, and the event starts at 7:15.",
                        ],
                        LegacyCouplesPkOptions,
                        LegacyPlayerWarEventAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyPlayerWarUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Couples PK registration is not available here yet. Please come back between 7:00 and 7:15.",
                        "MsgSchedules.CouplesPKWar.Join is not locally represented for this parity slice.",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyWarOfPlayersAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.WarOfPlayers";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Hi, I lead the War of Players event. What can I do for you? The event runs from 14:00 to 14:30."],
                        LegacyPlayerWarEventOptions,
                        LegacyPlayerWarEventAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyWarOfPlayersNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(6_729, 29, 48, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                await LogLegacyPlayerWarGapAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "MsgWarOfPlayers.Proces event-state gating is not locally represented; this parity slice preserves the source-backed entry destination.")
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyPlayerWarSourceAuditedNoRewardAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "No War of Players reward is available here yet.",
                        "NpcHandler.SacredClassEXP lines 108711-108715 add option Claim~reward.[WarOfPlayers] as option 4; source audit of the same switch through line 108834 found no case 4 reward body; rg found no PrizeAllTops.WarOfPlayer runtime usage except Role/PrizesGame.cs line 21 constant; MsgWarOfPlayers only models process, score, and RoundOwner reset.",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyChampionsOfWarrAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.MsgChampionsOfWarr";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Hi, I lead the Champions of War event. What can I do for you? The event runs daily from 18:00 to 18:30."],
                        LegacyPlayerWarEventOptions,
                        LegacyPlayerWarEventAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                var entrySession = session.Character.MinHitPoints >= 1
                    ? session
                    : session with { Character = session.Character with { MinHitPoints = 1 } };
                if (!ReferenceEquals(entrySession, session))
                {
                    await LogLegacyPlayerWarGapAsync(
                            context,
                            sourceMethod,
                            optionId,
                            "Source calls Player.Revive before entry; local revive is limited to restoring MinHitPoints for this entry.")
                        .ConfigureAwait(false);
                }

                await SendLegacyNpcTeleportAsync(
                        context,
                        entrySession,
                        LegacyChampionsOfWarrNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(6_546, 34, 55, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                await LogLegacyPlayerWarGapAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "MsgSchedules.ChampionsOfWarr.Proces event-state gating is not locally represented; this parity slice preserves the source-backed entry destination.")
                    .ConfigureAwait(false);
                return true;

            case 2:
                await TryClaimLegacyChampionsOfWarrRewardAsync(
                        context,
                        session,
                        sourceMethod,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask SendLegacyPlayerWarUnavailableAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string text,
        string gapReason,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [text],
                LegacyPlayerWarEventCloseOptions,
                LegacyPlayerWarEventAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyPlayerWarGapAsync(context, sourceMethod, optionId, gapReason).ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyPlayerWarSourceAuditedNoRewardAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string text,
        string evidence,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [text],
                LegacyPlayerWarEventCloseOptions,
                LegacyPlayerWarEventAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled source-audited LegacyNpc PlayerWarEvents no reward " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {evidence}")
            .ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyPlayerWarGapAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc PlayerWarEvents " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}")
            .ConfigureAwait(false);
    }
}

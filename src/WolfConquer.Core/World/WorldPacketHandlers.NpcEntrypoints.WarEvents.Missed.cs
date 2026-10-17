namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyWarFightersNpcId = 22_213;
    private const uint LegacyEmperorWarNpcId = 43_434;
    private const ushort LegacyMissedWarEventAvatarId = 63;

    private static readonly LegacyNpcDialogOption[] LegacyMissedWarEventOptions =
    [
        new("Enter", 1),
        new("Reward.", 2),
        new("Just passing by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyMissedWarEventUnavailableOptions =
    [
        new("Okay.", LegacyNpcClosedOption),
    ];

    public static async ValueTask<bool> TryHandleLegacyMissedWarEventNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyWarFightersNpcId => await TryHandleLegacyWarFightersAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyEmperorWarNpcId => await TryHandleLegacyEmperorWarAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyWarFightersAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.WarFighters";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Hi, I manage the War Fighters event. How can I help you? It starts at 10:00."],
                        LegacyMissedWarEventOptions,
                        LegacyMissedWarEventAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyMissedWarEventTeleportAsync(
                        context,
                        session,
                        LegacyWarFightersNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(11_073, 170, 222, sourceMethod),
                        "MsgWarFighters.Proces event-state gating is not locally represented; this parity slice preserves the source-backed entry destination.",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await TryClaimLegacyWarFightersRewardAsync(
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

    private static async ValueTask<bool> TryHandleLegacyEmperorWarAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.EmperorWar";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["This is Emperor War. It runs from 16:00 to 16:30."],
                        LegacyMissedWarEventOptions,
                        LegacyMissedWarEventAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyMissedWarEventTeleportAsync(
                        context,
                        session,
                        LegacyEmperorWarNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(10_702, 158, 170, sourceMethod),
                        "MsgEmperorWar.Proces event-state gating is not locally represented; this parity slice preserves the source-backed entry destination.",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await TryClaimLegacyEmperorWarRewardAsync(
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

    private static async ValueTask SendLegacyMissedWarEventTeleportAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        LegacyNpcTeleportDestination destination,
        string gapReason,
        CancellationToken cancellationToken)
    {
        var entrySession = session.Character.MinHitPoints >= 1
            ? session
            : session with { Character = session.Character with { MinHitPoints = 1 } };
        if (!ReferenceEquals(entrySession, session))
        {
            await LogLegacyMissedWarEventGapAsync(
                    context,
                    destination.SourceMethod,
                    optionId,
                    "Source calls Player.Revive before entry; local revive is limited to restoring MinHitPoints for this entry.")
                .ConfigureAwait(false);
        }

        await SendLegacyNpcTeleportAsync(context, entrySession, npcId, optionId, destination, cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyMissedWarEventGapAsync(context, destination.SourceMethod, optionId, gapReason)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyMissedWarEventUnavailableAsync(
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
                LegacyMissedWarEventUnavailableOptions,
                LegacyMissedWarEventAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyMissedWarEventGapAsync(context, sourceMethod, optionId, gapReason).ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyMissedWarEventGapAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc MissedWarEvent " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}")
            .ConfigureAwait(false);
    }
}

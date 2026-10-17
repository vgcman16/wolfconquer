namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyJailGuildServicesDragonBallItemId = 1_088_000;

    private static readonly LegacyNpcDialogOption[] LegacyJailGuildServicesISeeOptions =
    [
        new("I see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyJailGuildServicesOkayOptions =
    [
        new("Okay.", LegacyNpcClosedOption),
    ];

    private readonly record struct LegacyJailGuildServicesSourceInfo(string SourceMethod, string SourceLines);

    public static async ValueTask<bool> TryHandleLegacyJailGuildServicesNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (LegacyJailGuildServicesCaptainSources.TryGetValue(npcId, out var captainSource))
        {
            return await TryHandleLegacyJailGuildServicesCaptainAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    captainSource,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return npcId switch
        {
            LegacyJailGuildServicesNemesysConductortNpcId => await TryHandleLegacyJailGuildServicesNemesysConductortAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyJailGuildServicesWardenZhangNpcId => await TryHandleLegacyJailGuildServicesWardenZhangAsync(
                    context,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyJailGuildServicesJailWardenNpcId => await TryHandleLegacyJailGuildServicesJailWardenAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyJailGuildServicesJailJoinNpcId => await TryHandleLegacyJailGuildServicesJailJoinAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyJailGuildServicesGuildConductorNpcId => await TryHandleLegacyJailGuildServicesGuildConductorAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyJailGuildServicesConfiscatorNpcId => await TryHandleLegacyJailGuildServicesConfiscatorAsync(
                    context,
                    optionId,
                    LegacyJailGuildServicesConfiscatorSource,
                    LegacyJailGuildServicesConfiscatorNpcId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyJailGuildServicesJailConfiscatorNpcId => await TryHandleLegacyJailGuildServicesConfiscatorAsync(
                    context,
                    optionId,
                    LegacyJailGuildServicesJailConfiscatorSource,
                    LegacyJailGuildServicesJailConfiscatorNpcId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyJailGuildServicesMillionaireLeeNpcId => await TryHandleLegacyJailGuildServicesMillionaireLeeAsync(
                    context,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyJailGuildServicesUnknowManNpcId => await TryHandleLegacyJailGuildServicesUnknowManAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyJailGuildServicesLotteryCollectorWongNpcId => await TryHandleLegacyJailGuildServicesLotteryCollectorWongAsync(
                    context,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyJailGuildServicesLotteryLadyLuckNpcId => await TryHandleLegacyJailGuildServicesLotteryLadyLuckAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyJailGuildServicesMarketLadyLuckNpcId => await TryHandleLegacyJailGuildServicesMarketLadyLuckAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyJailGuildServicesRebirthMasterNpcId => await TryHandleLegacyJailGuildServicesRebirthMasterAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyJailGuildServicesMountTrainerNpcId => await TryHandleLegacyJailGuildServicesMountTrainerAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask SendLegacyJailGuildServicesUnavailableAsync(
        WorldPacketHandlerContext context,
        LegacyJailGuildServicesSourceInfo source,
        uint npcId,
        byte optionId,
        string reason,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.ClickedNpc(
                    npcId,
                    optionId: optionId,
                    source: source.SourceMethod,
                    reason: reason))
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc JailGuildServices " +
                $"{FormatSession(context)} source={source.SourceMethod} sourceLines={source.SourceLines} " +
                $"npcId={npcId} optionId={optionId} reason={reason}.")
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                context,
                source.SourceMethod,
                ["That service is not available yet. No items, CPs, silver, or character state were changed."],
                LegacyJailGuildServicesISeeOptions,
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }
}

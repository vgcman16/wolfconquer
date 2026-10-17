namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly LegacyNpcDialogOption[] LegacyCupCasinoServicesCloseOptions =
    [
        new("I see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCupCasinoServicesUnavailableOptions =
    [
        new("I understand.", LegacyNpcClosedOption),
    ];

    public static async ValueTask<bool> TryHandleLegacyCupCasinoServicesNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyCupCasinoServicesCSElitePkManagerNpcId => await LegacyCupCasinoServicesTryHandleCSElitePkManagerAsync(
                    context,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyCupCasinoServicesHolyPhoenixCupNpcId => await LegacyCupCasinoServicesTryHandlePhoenixCupAsync(
                    context,
                    optionId,
                    "NpcHandler.HolyPhoenixCup",
                    LegacyCupCasinoServicesHolyPhoenixCupTexts,
                    318,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyCupCasinoServicesGoldenPhoenixCupNpcId => await LegacyCupCasinoServicesTryHandlePhoenixCupAsync(
                    context,
                    optionId,
                    "NpcHandler.GoldenPhoenixCup",
                    LegacyCupCasinoServicesGoldenPhoenixCupTexts,
                    317,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyCupCasinoServicesSilverPheoenixCupNpcId => await LegacyCupCasinoServicesTryHandlePhoenixCupAsync(
                    context,
                    optionId,
                    "NpcHandler.SilverPheoenixCup",
                    LegacyCupCasinoServicesSilverPhoenixCupTexts,
                    316,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyCupCasinoServicesBronzePhoenixCupNpcId => await LegacyCupCasinoServicesTryHandlePhoenixCupAsync(
                    context,
                    optionId,
                    "NpcHandler.BronzePhoenixCup",
                    LegacyCupCasinoServicesBronzePhoenixCupTexts,
                    315,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyCupCasinoServicesChiMakerNpcId => await LegacyCupCasinoServicesTryHandleChiMakerAsync(
                    context,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyCupCasinoServicesWuxingOvenNpcId => await LegacyCupCasinoServicesTryHandleWuxingOvenAsync(
                    context,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyCupCasinoServicesPokerCpsCasinoNpcId => await LegacyCupCasinoServicesTryHandlePokerCpsCasinoAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyCupCasinoServicesPokerCasinoHostessNpcId => await LegacyCupCasinoServicesTryHandlePokerCasinoHostessAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyCupCasinoServicesCasinoHostessNpcId => await LegacyCupCasinoServicesTryHandleCasinoHostessAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyCupCasinoServicesGameManagerNpcId => await LegacyCupCasinoServicesTryHandleGameManagerAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask LegacyCupCasinoServicesSendUnavailableAsync(
        WorldPacketHandlerContext context,
        string source,
        string sourceLines,
        uint npcId,
        byte optionId,
        string text,
        string effect,
        ushort? avatarId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                source,
                [text],
                LegacyCupCasinoServicesUnavailableOptions,
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc CupCasinoServices " +
                $"{FormatSession(context)} source={source} sourceLines={sourceLines} npcId={npcId} " +
                $"optionId={optionId} effect={effect}.")
            .ConfigureAwait(false);
    }
}

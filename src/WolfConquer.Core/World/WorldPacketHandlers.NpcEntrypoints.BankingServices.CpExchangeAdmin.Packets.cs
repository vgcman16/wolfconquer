namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask SendLegacyCpExchangeInsufficientDialogAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        LegacyCpExchangeMutation mutation,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [mutation.InsufficientText],
                [new LegacyNpcDialogOption(mutation.InsufficientOption, byte.MaxValue)],
                avatarId: 7,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyCpExchangeAdminUnavailableAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string reason,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                ["This exchange is unavailable here until the item and currency transaction path is fully audited."],
                [new LegacyNpcDialogOption("I understand.", byte.MaxValue)],
                avatarId: 7,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc CpExchangeAdmin " +
                $"{FormatSession(context)} source={sourceMethod} optionId={optionId} " +
                "sourceLines=MarketCpAdmin:29816-29994/JailCPAdmin:31321-31475; " +
                $"mutation was not committed atomically reason={reason}.")
            .ConfigureAwait(false);
    }

    private static ValueTask SendLegacyCpExchangeSuccessMessageAsync(
        WorldPacketHandlerContext context,
        string text,
        CancellationToken cancellationToken)
    {
        return context.Connection.SendAsync(
            LegacyNpcReplyPacket.ForMessageBox(text).ToFrameBytes(),
            cancellationToken);
    }

    private static async ValueTask SendLegacyCpExchangeCurrencyUpdatesAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        Data.CharacterSnapshot character,
        CancellationToken cancellationToken)
    {
        if (sourceMethod == LegacyMarketCpAdminSource)
        {
            await SendLegacyCpExchangeAdminUpdateAsync(
                    context,
                    character,
                    LegacyMsgUpdateDataType.ConquerPoints,
                    checked((ulong)character.ConquerPoints),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyCpExchangeAdminUpdateAsync(
                    context,
                    character,
                    LegacyMsgUpdateDataType.Money,
                    checked((ulong)character.Money),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyCpExchangeAdminUpdateAsync(
                    context,
                    character,
                    LegacyMsgUpdateDataType.ConquerPoints,
                    checked((ulong)character.ConquerPoints),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await SendLegacyCpExchangeAdminUpdateAsync(
                context,
                character,
                LegacyMsgUpdateDataType.Money,
                checked((ulong)character.Money),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCpExchangeAdminUpdateAsync(
                context,
                character,
                LegacyMsgUpdateDataType.ConquerPoints,
                checked((ulong)character.ConquerPoints),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCpExchangeAdminUpdateAsync(
                context,
                character,
                LegacyMsgUpdateDataType.Money,
                checked((ulong)character.Money),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ValueTask SendLegacyCpExchangeAdminUpdateAsync(
        WorldPacketHandlerContext context,
        Data.CharacterSnapshot character,
        LegacyMsgUpdateDataType type,
        ulong value,
        CancellationToken cancellationToken)
    {
        return context.Connection.SendAsync(
            new LegacyMsgUpdatePacket(
                CheckedUInt32(character.Uid, nameof(character.Uid)),
                [new LegacyMsgUpdateEntry(type, value)])
            .ToFrameBytes(),
            cancellationToken);
    }
}

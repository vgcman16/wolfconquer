namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyExchangeShopGoodsNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId != LegacyExchangeShopGoodsNpcId)
        {
            return false;
        }

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.Exchange_shop_Goods",
                    [LegacyExchangeShopGoodsDialogText],
                    LegacyExchangeShopGoodsOptions,
                    LegacyExchangeShopGoodsAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var shopId = optionId switch
        {
            1 => LegacyExchangeShopGoodsGemShopId,
            2 => LegacyExchangeShopGoodsOtherShopId,
            _ => 0U,
        };
        if (shopId == 0)
        {
            return false;
        }

        await SendLegacyExchangeShopGoodsAsync(
                context,
                session,
                LegacyExchangeShopGoodsSourceNpcId,
                shopId,
                "NpcHandler.Exchange_shop_Goods",
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyHauKanAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId is not LegacyHauKanPrimaryNpcId and not LegacyHauKanSecondaryNpcId)
        {
            return false;
        }

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.HauKan",
                    LegacyHauKanDialogTexts,
                    LegacyHauKanOptions,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            await SendLegacyExchangeShopGoodsAsync(
                    context,
                    session,
                    LegacyExchangeShopGoodsSourceNpcId,
                    LegacyHauKanSupplyShopId,
                    "NpcHandler.HauKan",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 2)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.HauKan",
                    [LegacyHauKanWinnerDialogText],
                    LegacyHauKanWinnerOptions,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }
}

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyPointsNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId != LegacyPointsNpcId)
        {
            return false;
        }

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.Points",
                    [LegacyPointsDialogText],
                    LegacyPointsOptions,
                    LegacyExchangeShopGoodsAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 6)
        {
            await SendLegacyNpcOpenClientWindowActionAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    LegacyPointsShopWindowId,
                    "NpcHandler.Points",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyMountRetailerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId != LegacyMountRetailerNpcId || optionId is not 0 and not 1)
        {
            return false;
        }

        await SendLegacyNpcOpenGuiActionAsync(
                context,
                session,
                npcId,
                optionId,
                "NpcHandler.MountRetailer",
                useMapIdField: false,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacySteedRaceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId != LegacySteedRaceNpcId)
        {
            return false;
        }

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.SteedRace",
                    [LegacySteedRaceDialogText],
                    LegacySteedRaceOptions,
                    avatarId: 63,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 2)
        {
            await SendLegacyNpcOpenClientWindowActionAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    LegacySteedRaceShopWindowId,
                    "NpcHandler.SteedRace",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyClientWindowNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId == LegacyOpenLotteryNpcId)
        {
            if (optionId == 0)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.openlottery",
                        [LegacyOpenLotteryDialogText],
                        LegacyOpenLotteryOptions,
                        LegacyTeleportallAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            if (optionId == 1)
            {
                await SendLegacyNpcOpenClientWindowActionAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        LegacyOpenLotteryWindowId,
                        "NpcHandler.openlottery",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
        }

        if (npcId == LegacyEquipmentBlacksmithNpcId)
        {
            if (optionId == 0)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.EquipmentBlacksmith",
                        LegacyEquipmentBlacksmithDialogTexts,
                        LegacyEquipmentBlacksmithOptions,
                        avatarId: 63,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            if (optionId == 1)
            {
                await SendLegacyNpcOpenClientWindowActionAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        LegacyEquipmentBlacksmithWindowId,
                        "NpcHandler.EquipmentBlacksmith",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
        }

        if (npcId == LegacyGarmentShopKeeperNpcId && optionId == 0)
        {
            await SendLegacyNpcOpenClientWindowActionAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    LegacyGarmentShopWindowId,
                    "NpcHandler.GarmentShopKeeper",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (npcId == LegacyBeauticianNpcId && optionId == 0)
        {
            await SendLegacyNpcOpenClientWindowActionAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    LegacyBeauticianWindowId,
                    "NpcHandler.Beautician",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }
}

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyTheStorekeeperNpcId = 1;
    private const uint LegacyPharmacistNpcId = 3;
    private const uint LegacyArmorerNpcId = 4;
    private const uint LegacyBarberNpcId = 10_002;
    private const uint LegacyMasterXuanNpcId = 3_085;
    private const uint LegacySurgeonMiracleNpcId = 3_381;
    private const uint LegacyShopboyNpcId = 10_063;
    private const uint LegacyArchveShopNpcId = 2_007;
    private const uint LegacyGarmentsDealerNpcId = 50_029;
    private const uint LegacyMountsDealerNpcId = 50_030;
    private const uint LegacyMarketShopNpcId = 50_031;
    private const uint LegacyAccessoriesDealerNpcId = 50_048;
    private const uint LegacyBeauticianDealerNpcId = 50_049;
    private const uint LegacyFurnitureStoreNpcId = 30_161;
    private const uint LegacyTinterNpcId = 832_662;

    public static async ValueTask<bool> TryHandleLegacyShopServicesNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (TryGetLegacyShopServiceOpenGui(npcId, optionId, out var openGui))
        {
            await SendLegacyNpcOpenGuiActionAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    openGui.SourceMethod,
                    openGui.UseMapIdField,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (TryGetLegacyShopServiceDialog(npcId, optionId, out var dialog))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    dialog.SourceMethod,
                    dialog.Texts,
                    dialog.Options,
                    dialog.AvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (TryGetLegacyShopServiceTeleport(npcId, optionId, out var teleport))
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    teleport.Destination,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (TryGetLegacyShopServiceNoOp(npcId, optionId, out var noOp))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored LegacyNpc shop-service no-op " +
                    $"{FormatSession(context)} npcId={npcId} optionId={optionId} " +
                    $"source={noOp.SourceMethod} sourceLines={noOp.SourceLines}.")
                .ConfigureAwait(false);
            return true;
        }

        if (TryGetLegacyShopServiceGap(npcId, optionId, out var gap))
        {
            await SendLegacyShopServiceUnavailableAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    gap,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static bool ContainsOption(byte[] optionIds, byte optionId)
    {
        return Array.IndexOf(optionIds, optionId) >= 0;
    }
}

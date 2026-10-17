namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesCloseOptions =
    [
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static bool LegacyRealmTransferServicesIsOnOwnServer(WorldSession session)
    {
        if (TryReadAttributeBool(session.Character.Attributes, out var onOwnServer, "OnMyOwnServer", "OnOwnServer"))
        {
            return onOwnServer;
        }

        var serverId = ReadAttributeUInt64(session.Character.Attributes, "ServerID", "ServerId");
        var ownServerId = ReadAttributeUInt64(session.Character.Attributes, "MyServerID", "MyServerId");
        return serverId == 0 || ownServerId == 0 || serverId == ownServerId;
    }

    private static bool LegacyRealmTransferServicesHasInventoryItem(
        WorldSession session,
        uint itemId,
        int requiredCount)
    {
        var count = 0;
        foreach (var item in session.PlayerItems.InventoryItems)
        {
            if (item.ItemId != itemId)
            {
                continue;
            }

            var stackSize = item.StackSize == 0 ? 1 : (int)item.StackSize;
            count += stackSize;
            if (count >= requiredCount)
            {
                return true;
            }
        }

        return false;
    }

    private static async ValueTask SendLegacyRealmTransferServicesNoticeAsync(
        WorldPacketHandlerContext context,
        string source,
        string text,
        ushort? avatarId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                source,
                [text],
                LegacyRealmTransferServicesCloseOptions,
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyRealmTransferServicesGapAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        byte optionId,
        string source,
        string sourceLines,
        string effect,
        string text,
        ushort? avatarId,
        CancellationToken cancellationToken)
    {
        await SendLegacyRealmTransferServicesNoticeAsync(context, source, text, avatarId, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc RealmTransferServices " +
                $"source={source} sourceLines={sourceLines} npcId={npcId} optionId={optionId} " +
                $"{FormatSession(context)} effect={effect}; {text}")
            .ConfigureAwait(false);
    }
}

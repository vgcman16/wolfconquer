using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.LegacyMelterRankList)]
    private static async ValueTask HandleLegacyMelterRankListAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyMelterRankListPacket.TryParse(frame, out var query))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyMelterRankList packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyMelterRankList " +
                    $"{FormatSession(context)} page={query.Page}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var response = LegacyMelterRankListPacket.EmptyResponse(query.Page);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyMelterRankList " +
                $"{FormatSession(context)} page={query.Page} entries={response.Entries.Count}.")
            .ConfigureAwait(false);
    }

    [PacketHandler(WorldPacketOpcodes.LegacyMelterOpt)]
    private static async ValueTask HandleLegacyMelterOptAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyMelterOptPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyMelterOpt packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyMelterOpt " +
                    $"{FormatSession(context)} type={packet.Type} items={packet.Items.Count}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Items.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyMelterOpt " +
                    $"{FormatSession(context)} type={packet.Type} items=0.")
                .ConfigureAwait(false);
            return;
        }

        var requestItem = packet.Items[0];
        if (!TryResolveLegacyMelterPrize(
                context.WorldData,
                requestItem.Id,
                out var prizeItemType,
                out var prizePoolSize,
                out var skipReason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyMelterOpt " +
                    $"{FormatSession(context)} type={packet.Type} items={packet.Items.Count} " +
                    $"firstItemId={requestItem.Id}; {skipReason}.")
                .ConfigureAwait(false);
            return;
        }

        var inventoryItem = session.PlayerItems.InventoryItems.FirstOrDefault(item => item.ItemId == requestItem.Id);
        if (inventoryItem is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyMelterOpt " +
                    $"{FormatSession(context)} type={packet.Type} items={packet.Items.Count} " +
                    $"firstItemId={requestItem.Id}; inventory item was not found.")
                .ConfigureAwait(false);
            return;
        }

        var updatedItems = ConsumeSingleLegacyInventoryItem(session.PlayerItems, inventoryItem, out var consumeFrame);
        var prizeItem = CreatePurchasedItems(updatedItems, prizeItemType, count: 1, isBound: true)[0];
        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems.Concat([prizeItem]).OrderBy(item => item.Uid).ToArray(),
        };
        context.Sessions?.Set(context.Connection, session with { PlayerItems = updatedItems });

        await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(prizeItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var response = new LegacyMelterOptPacket(
            Type: 0,
            EntityId: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            EntityName: session.Character.Name,
            Items:
            [
                new LegacyMelterOptItem(
                    Type: 8,
                    Id: prizeItem.ItemId,
                    Bound: true,
                    AnnounceMessage: false),
            ]);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyMelterOpt " +
                $"{FormatSession(context)} type={packet.Type} items={packet.Items.Count} " +
                $"firstItemId={requestItem.Id} prizeItemId={prizeItem.ItemId} prizePoolSize={prizePoolSize}.")
            .ConfigureAwait(false);
    }

    private static bool TryResolveLegacyMelterPrize(
        WorldDataCatalog? worldData,
        uint meltingItemId,
        out ItemType prizeItemType,
        out int prizePoolSize,
        out string reason)
    {
        prizeItemType = default!;
        prizePoolSize = 0;

        if (worldData is null)
        {
            reason = "melting type data was not loaded";
            return false;
        }

        if (meltingItemId > int.MaxValue ||
            !worldData.MeltingTypeRulesByItemTypeId.TryGetValue((int)meltingItemId, out var meltingRules))
        {
            reason = "melting item rule was not loaded";
            return false;
        }

        var meltingRule = meltingRules.FirstOrDefault(rule => rule.RuleType == 1 && rule.Parameters.Count >= 3);
        if (meltingRule is null)
        {
            reason = "melting item rule was not source-shaped";
            return false;
        }

        var prizeType = meltingRule.Parameters[2];
        if (!worldData.MeltingTypeRulesByRuleType.TryGetValue(2, out var prizePoolRules) ||
            prizePoolRules.FirstOrDefault(rule => rule.ItemTypeId == prizeType && rule.Parameters.Count > 0) is not { } prizePool)
        {
            reason = "melting prize pool was not loaded";
            return false;
        }

        prizePoolSize = prizePool.Parameters.Count;
        var prizeItemId = prizePool.Parameters[Random.Shared.Next(prizePool.Parameters.Count)];
        if (!worldData.ItemsById.TryGetValue(prizeItemId, out var itemTypes) || itemTypes.Count == 0)
        {
            reason = "melting prize itemtype was not loaded";
            return false;
        }

        prizeItemType = itemTypes[0];
        reason = "";
        return true;
    }

}
